using System;
using System.Diagnostics;

/// <summary>
/// 전문 체스 AI 탐색 엔진 (Unity 오브젝트를 전혀 쓰지 않는 순수 C#).
/// 백그라운드 스레드에서 안전하게 돌릴 수 있고, 탐색 중 메모리를 새로 만들지 않습니다.
///
/// 이 게임의 규칙(왕을 "잡으면" 이김)에 맞춰져 있습니다.
///  - 상대 왕을 잡는 수는 무조건 최우선입니다. (루트에서 즉시 선택)
///  - 왕을 잡는 수열은 "Mate - 수"로 점수를 주기 때문에, 더 빨리 잡는 길을 고릅니다.
///  - 내 왕이 공격받는 상태로 두는 수는 다음 수에 왕을 잡히므로 탐색이 스스로 걸러냅니다.
///
/// 기능: 네가맥스 + 알파베타(PVS) / 반복 심화(시간 제한) / 퀴시전스 / 체크 연장 /
///       MVV-LVA, 킬러, 히스토리 정렬 / LMR / 대칭 평가(재료, 위치표, 폰 구조, 킹 안전, 기동성).
/// </summary>
public sealed class ChessSearchEngine
{
    public string FailReason;   // 디버그용
    // ───────────── 상수 ─────────────
    public const int Mate = 30000;
    private const int Inf = 32000;
    private const int MaxPly = 64;
    private const int MaxMoves = 320;
    private const int MaxQuiet = 8;

    // 기물 코드: 종류 1~6, 색은 8을 더해 구분 (백 0 / 흑 +8)
    public const int P = 1, N = 2, B = 3, R = 4, Q = 5, K = 6;

    // 수 플래그
    private const int FlagPromo = 1, FlagEp = 2, FlagCastle = 4, FlagDouble = 8;

    private static readonly int[] PieceVal = { 0, 100, 320, 330, 500, 900, 20000 };

    private static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
    private static readonly int[] KnX = { 1, 2, 2, 1, -1, -2, -2, -1 };
    private static readonly int[] KnY = { 2, 1, -1, -2, -2, -1, 1, 2 };

    // ───────────── 보드 상태 ─────────────
    private readonly int[] b = new int[64];       // 칸 번호 = x + y*8
    private readonly bool[] moved = new bool[64]; // 이 칸의 기물이 한 번이라도 움직였는가
    private readonly int[] kingSq = { -1, -1 };
    private int side;                             // 0 백, 1 흑 (지금 둘 차례)
    private int ep = -1;                          // 앙파상으로 들어갈 칸 (없으면 -1)

    // 규칙 (색별). 협상으로 금지되면 false
    private readonly bool[] canCastle = { true, true };
    private readonly bool[] canEnPassant = { true, true };
    private readonly bool[] canPromote = { true, true };

    // ───────────── 탐색 버퍼 ─────────────
    private struct Undo
    {
        public int piece, captured, capSq, prevEp;
        public bool movedFrom, capMoved;
        public int rookFrom, rookTo; // 캐슬링일 때만
    }

    private readonly int[] moves = new int[MaxPly * MaxMoves];
    private readonly int[] scores = new int[MaxPly * MaxMoves];
    private readonly Undo[] undos = new Undo[MaxPly];
    private readonly int[] killers = new int[MaxPly * 2];
    private readonly int[] history = new int[2 * 64 * 64];

    // 루트
    private readonly int[] rootMoves = new int[MaxMoves];
    private int rootCount;

    // 평가용 임시 배열
    private readonly int[] pawnCnt = new int[16];
    private readonly int[] whiteMinY = new int[8];
    private readonly int[] blackMaxY = new int[8];
    private readonly int[] bishopCnt = new int[2];
    private readonly int[] evalScore = new int[2];

    // 시간/통계
    private readonly Stopwatch clock = new Stopwatch();
    private long timeLimitMs;
    private bool aborted;
    public long Nodes { get; private set; }
    public int ReachedDepth { get; private set; }

    public struct Result
    {
        public bool found;
        public int fromX, fromY, toX, toY;
        public int score;
        public int depth;
    }

    // ───────────── 보드 설정 (메인 스레드에서 탐색 시작 전에 호출) ─────────────

    public void Clear()
    {
        Array.Clear(b, 0, 64);
        Array.Clear(moved, 0, 64);
        kingSq[0] = kingSq[1] = -1;
        ep = -1;
        side = 0;
    }

    /// <param name="type">1=폰 2=나이트 3=비숍 4=룩 5=퀸 6=킹</param>
    /// <param name="color">0=백 1=흑</param>
    public void SetPiece(int x, int y, int type, int color, bool hasMoved)
    {
        int sq = x + y * 8;
        b[sq] = type | (color << 3);
        moved[sq] = hasMoved;
        if (type == K) kingSq[color] = sq;
    }

    public void SetSideToMove(int color) { side = color; }
    /// <summary>방금 두 칸 전진한 폰이 있으면, 그 폰을 앙파상으로 잡을 때 들어갈 칸을 알려 줍니다.</summary>
    public void SetEnPassant(int x, int y) { ep = x + y * 8; }

    public void SetRules(int color, bool castle, bool enPassant, bool promote)
    {
        canCastle[color] = castle;
        canEnPassant[color] = enPassant;
        canPromote[color] = promote;
    }

    // ───────────── 바깥에서 부르는 탐색 ─────────────

    public Result Think(int maxDepth, int timeMs)
    {
        Result res = default;
        if (maxDepth < 1) maxDepth = 1;
        if (maxDepth > 20) maxDepth = 20;

        Nodes = 0;
        ReachedDepth = 0;
        aborted = false;
        timeLimitMs = timeMs;
        clock.Restart();

        Array.Clear(killers, 0, killers.Length);
        for (int i = 0; i < history.Length; i++) history[i] >>= 2;

       
        if (kingSq[side] < 0) return res; // 내 왕이 없으면 둘 수 없음


        // 루트 수 생성
        int n = Gen(0, false);
        rootCount = n;
        if (n == 0)
        return res; 
        for (int i = 0; i < n; i++) rootMoves[i] = moves[i];

        // ★ 최우선: 상대 왕을 잡을 수 있으면 다른 건 볼 것도 없이 그 수를 둔다.
        int enemyKing = kingSq[side ^ 1];
        for (int i = 0; i < n; i++)
        {
            if (((rootMoves[i] >> 6) & 63) == enemyKing && enemyKing >= 0)
            {
                return MakeResult(rootMoves[i], Mate, 1);
            }
        }

        int bestMove = rootMoves[0];
        int bestScore = -Inf;
        int doneDepth = 0;

        // 첫 수 후보를 잡기 우선으로 정렬해 둔다 (1회차 탐색 품질)
        SortRootByScore(n);

        for (int depth = 1; depth <= maxDepth; depth++)
        {
            int alpha = -Inf;
            int iterBest = -1;
            int iterScore = -Inf;

            for (int i = 0; i < rootCount; i++)
            {
                int mv = rootMoves[i];
                Make(mv, ref undos[0]);
                int s = -Negamax(depth - 1, -Inf, -alpha, 1, depth);
                Unmake(mv, ref undos[0]);

                if (aborted) break;

                if (s > iterScore)
                {
                    iterScore = s;
                    iterBest = i;
                    if (s > alpha) alpha = s;
                }
            }

            if (iterBest >= 0)
            {
                bestMove = rootMoves[iterBest];
                bestScore = iterScore;
                if (!aborted) doneDepth = depth;
                else doneDepth = Math.Max(doneDepth, depth - 1);

                // 이번 최선수를 맨 앞으로 (다음 반복에서 가장 먼저 탐색)
                if (iterBest > 0)
                {
                    int tmp = rootMoves[iterBest];
                    for (int k = iterBest; k > 0; k--) rootMoves[k] = rootMoves[k - 1];
                    rootMoves[0] = tmp;
                }
            }

            if (aborted) break;
            if (bestScore >= Mate - 200) break;       // 이기는 수순을 찾았으면 그만
            if (bestScore <= -Mate + 200 && depth >= 3) break; // 어차피 지는 판이면 더 볼 필요 없음
            if (rootCount == 1) break;                // 둘 수 있는 수가 하나뿐
        }

       

        ReachedDepth = doneDepth;
        return MakeResult(bestMove, bestScore, doneDepth);
    }

    private Result MakeResult(int mv, int score, int depth)
    {
        int from = mv & 63;
        int to = (mv >> 6) & 63;
        return new Result
        {
            found = true,
            fromX = from & 7,
            fromY = from >> 3,
            toX = to & 7,
            toY = to >> 3,
            score = score,
            depth = depth
        };
    }

    private void SortRootByScore(int n)
    {
        // 생성 직후의 점수(잡기 우선)를 그대로 써서 간단히 삽입 정렬
        for (int i = 1; i < n; i++)
        {
            int key = rootMoves[i];
            int keyScore = scores[i];
            int j = i - 1;
            while (j >= 0 && scores[j] < keyScore)
            {
                rootMoves[j + 1] = rootMoves[j];
                scores[j + 1] = scores[j];
                j--;
            }
            rootMoves[j + 1] = key;
            scores[j + 1] = keyScore;
        }
    }

    // ───────────── 탐색 ─────────────

    private bool TimeUp()
    {
        return timeLimitMs > 0 && clock.ElapsedMilliseconds >= timeLimitMs;
    }

    private int Negamax(int depth, int alpha, int beta, int ply, int rootDepth)
    {
        Nodes++;
        if ((Nodes & 1023) == 0 && TimeUp()) aborted = true;
        if (aborted) return 0;

        // 방금 상대가 내 왕을 잡았다 → 내가 졌다. 빠를수록 더 나쁜 점수.
        if (kingSq[side] < 0) return -(Mate - ply);
        if (ply >= MaxPly - 2) return Evaluate();

        bool inCheck = IsAttacked(kingSq[side], side ^ 1);
        if (inCheck && ply < rootDepth * 2) depth++; // 체크 연장

        if (depth <= 0) return Quiesce(alpha, beta, ply, 0);

        int n = Gen(ply, false);
        if (n == 0) return 0; // 둘 수 있는 수가 없음 (스테일메이트 성격)

        int best = -Inf;
        bool first = true;
        int baseIdx = ply * MaxMoves;

        for (int i = 0; i < n; i++)
        {
            Pick(ply, i, n);
            int mv = moves[baseIdx + i];
            int to = (mv >> 6) & 63;
            bool quiet = b[to] == 0 && (mv >> 12 & ~FlagDouble) == 0;

            Make(mv, ref undos[ply]);

            int s;
            if (first)
            {
                s = -Negamax(depth - 1, -beta, -alpha, ply + 1, rootDepth);
                first = false;
            }
            else
            {
                int reduce = (quiet && !inCheck && depth >= 3 && i >= 4) ? 1 : 0;
                s = -Negamax(depth - 1 - reduce, -alpha - 1, -alpha, ply + 1, rootDepth);
                if (s > alpha && reduce > 0)
                    s = -Negamax(depth - 1, -alpha - 1, -alpha, ply + 1, rootDepth);
                if (s > alpha && s < beta)
                    s = -Negamax(depth - 1, -beta, -alpha, ply + 1, rootDepth);
            }

            Unmake(mv, ref undos[ply]);
            if (aborted) return 0;

            if (s > best)
            {
                best = s;
                if (s > alpha) alpha = s;
            }

            if (alpha >= beta)
            {
                if (quiet)
                {
                    int ki = ply * 2;
                    if (killers[ki] != mv) { killers[ki + 1] = killers[ki]; killers[ki] = mv; }
                    int h = (side << 12) | ((mv & 63) << 6) | to;
                    history[h] += depth * depth;
                    if (history[h] > 400000) history[h] = 400000;
                }
                break;
            }
        }

        return best;
    }

    private int Quiesce(int alpha, int beta, int ply, int qd)
    {
        Nodes++;
        if ((Nodes & 1023) == 0 && TimeUp()) aborted = true;
        if (aborted) return 0;

        if (kingSq[side] < 0) return -(Mate - ply);
        if (ply >= MaxPly - 2 || qd >= MaxQuiet) return Evaluate();

        bool inCheck = IsAttacked(kingSq[side], side ^ 1);
        int best;

        if (!inCheck)
        {
            int stand = Evaluate();
            if (stand >= beta) return stand;
            if (stand > alpha) alpha = stand;
            best = stand;
        }
        else
        {
            best = -(Mate - ply - 1); // 막지 못하면 왕을 잡힌다
        }

        int n = Gen(ply, !inCheck);
        int baseIdx = ply * MaxMoves;

        for (int i = 0; i < n; i++)
        {
            Pick(ply, i, n);
            int mv = moves[baseIdx + i];

            Make(mv, ref undos[ply]);
            int s = -Quiesce(-beta, -alpha, ply + 1, qd + 1);
            Unmake(mv, ref undos[ply]);
            if (aborted) return 0;

            if (s > best)
            {
                best = s;
                if (s > alpha) alpha = s;
                if (alpha >= beta) break;
            }
        }

        return best;
    }

    private void Pick(int ply, int i, int n)
    {
        int baseIdx = ply * MaxMoves;
        int bi = i;
        int bs = scores[baseIdx + i];
        for (int j = i + 1; j < n; j++)
        {
            if (scores[baseIdx + j] > bs) { bs = scores[baseIdx + j]; bi = j; }
        }
        if (bi != i)
        {
            int tm = moves[baseIdx + i]; moves[baseIdx + i] = moves[baseIdx + bi]; moves[baseIdx + bi] = tm;
            int ts = scores[baseIdx + i]; scores[baseIdx + i] = scores[baseIdx + bi]; scores[baseIdx + bi] = ts;
        }
    }

    // ───────────── 수 두기 / 되돌리기 ─────────────

    private void Make(int mv, ref Undo u)
    {
        int from = mv & 63;
        int to = (mv >> 6) & 63;
        int fl = mv >> 12;
        int pc = b[from];
        int color = pc >> 3;

        u.piece = pc;
        u.prevEp = ep;
        u.movedFrom = moved[from];
        u.rookFrom = -1;
        int rookFrom = -1, rookTo = -1;
        if ((fl & FlagCastle) != 0)
        {
            int cdir = to > from ? 1 : -1;
            rookTo = from + cdir;                       // 룩은 킹이 넘은 칸에 도착
            rookFrom = rookTo;
            while (b[rookFrom] == 0) rookFrom += cdir;  // 킹에서 가장 가까운 기물 = 캐슬링하는 룩
        }

        int capSq = to;
        if ((fl & FlagEp) != 0) capSq = to + (color == 0 ? -8 : 8);
        u.capSq = capSq;
        u.captured = b[capSq];
        u.capMoved = moved[capSq];

        if (u.captured != 0)
        {
            if ((u.captured & 7) == K) kingSq[u.captured >> 3] = -1;
            b[capSq] = 0;
            moved[capSq] = false;
        }

        b[from] = 0;
        moved[from] = false;
        b[to] = ((fl & FlagPromo) != 0) ? (Q | (color << 3)) : pc;
        moved[to] = true;

        if ((pc & 7) == K) kingSq[color] = to;

        if (rookFrom >= 0)
        {
            if (rookFrom != rookTo) { b[rookTo] = b[rookFrom]; b[rookFrom] = 0; }   // 룩이 킹 바로 옆이면 이동 불필요
            moved[rookFrom] = false;
            moved[rookTo] = true;
            u.rookFrom = rookFrom; u.rookTo = rookTo;
        }

        ep = ((fl & FlagDouble) != 0) ? (from + to) / 2 : -1;
        side ^= 1;
    }

    private void Unmake(int mv, ref Undo u)
    {
        int from = mv & 63;
        int to = (mv >> 6) & 63;
        int color = u.piece >> 3;

        side ^= 1;
        ep = u.prevEp;

        if (u.rookFrom >= 0)
        {
            if (u.rookFrom != u.rookTo) { b[u.rookFrom] = b[u.rookTo]; b[u.rookTo] = 0; }
            moved[u.rookFrom] = false;
            moved[u.rookTo] = false;
        }

        b[to] = 0;
        moved[to] = false;
        b[from] = u.piece;
        moved[from] = u.movedFrom;
        if ((u.piece & 7) == K) kingSq[color] = from;

        if (u.captured != 0)
        {
            b[u.capSq] = u.captured;
            moved[u.capSq] = u.capMoved;
            if ((u.captured & 7) == K) kingSq[u.captured >> 3] = u.capSq;
        }
    }

    // ───────────── 수 생성 ─────────────

    private void Add(int ply, ref int n, int from, int to, int flags)
    {
        int idx = ply * MaxMoves + n;
        int mv = from | (to << 6) | (flags << 12);
        moves[idx] = mv;

        int victim = b[to];
        int s;
        if (victim != 0 || (flags & FlagEp) != 0)
        {
            int vt = (flags & FlagEp) != 0 ? P : (victim & 7);
            int at = b[from] & 7;
            s = 1000000 + PieceVal[vt] * 16 - PieceVal[at] / 16;
            if ((flags & FlagPromo) != 0) s += 5000;
        }
        else if ((flags & FlagPromo) != 0)
        {
            s = 950000;
        }
        else
        {
            if (killers[ply * 2] == mv) s = 900000;
            else if (killers[ply * 2 + 1] == mv) s = 800000;
            else s = history[((b[from] >> 3) << 12) | (from << 6) | to];
        }
        scores[idx] = s;
        n++;
    }

    /// <summary>현재 둘 차례인 쪽의 수를 모두 만든다. capsOnly면 잡기/승진만.</summary>
    private int Gen(int ply, bool capsOnly)
    {
        int n = 0;
        int color = side;
        int enemy = color ^ 1;

        for (int sq = 0; sq < 64; sq++)
        {
            int pc = b[sq];
            if (pc == 0 || (pc >> 3) != color) continue;
            int type = pc & 7;
            int x = sq & 7;
            int y = sq >> 3;

            switch (type)
            {
                case P:
                    {
                        int dir = color == 0 ? 1 : -1;
                        int promoRank = color == 0 ? 7 : 0;
                        int ny = y + dir;
                        if (ny < 0 || ny > 7) break;

                        int fw = ny * 8 + x;
                        if (b[fw] == 0)
                        {
                            bool promo = ny == promoRank && canPromote[color];
                            if (!capsOnly || ny == promoRank)
                                Add(ply, ref n, sq, fw, promo ? FlagPromo : 0);

                            if (!capsOnly && !moved[sq])
                            {
                                int y2 = y + 2 * dir;
                                if (y2 >= 0 && y2 <= 7 && b[y2 * 8 + x] == 0)
                                    Add(ply, ref n, sq, y2 * 8 + x, FlagDouble);
                            }
                        }

                        for (int dx = -1; dx <= 1; dx += 2)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx > 7) continue;
                            int t = ny * 8 + nx;
                            int tp = b[t];
                            if (tp != 0)
                            {
                                if ((tp >> 3) == enemy)
                                {
                                    bool promo = ny == promoRank && canPromote[color];
                                    Add(ply, ref n, sq, t, promo ? FlagPromo : 0);
                                }
                            }
                            else if (ep == t && canEnPassant[color] && b[y * 8 + nx] == (P | (enemy << 3)))
                            {
                                Add(ply, ref n, sq, t, FlagEp);
                            }
                        }
                        break;
                    }

                case N:
                    for (int k = 0; k < 8; k++)
                    {
                        int nx = x + KnX[k], ny2 = y + KnY[k];
                        if (nx < 0 || nx > 7 || ny2 < 0 || ny2 > 7) continue;
                        int t = ny2 * 8 + nx;
                        int tp = b[t];
                        if (tp == 0) { if (!capsOnly) Add(ply, ref n, sq, t, 0); }
                        else if ((tp >> 3) == enemy) Add(ply, ref n, sq, t, 0);
                    }
                    break;

                case B:
                    GenSlide(ply, ref n, sq, x, y, 4, 8, enemy, capsOnly);
                    break;
                case R:
                    GenSlide(ply, ref n, sq, x, y, 0, 4, enemy, capsOnly);
                    break;
                case Q:
                    GenSlide(ply, ref n, sq, x, y, 0, 8, enemy, capsOnly);
                    break;

                case K:
                    for (int k = 0; k < 8; k++)
                    {
                        int nx = x + DX[k], ny2 = y + DY[k];
                        if (nx < 0 || nx > 7 || ny2 < 0 || ny2 > 7) continue;
                        int t = ny2 * 8 + nx;
                        int tp = b[t];
                        if (tp == 0) { if (!capsOnly) Add(ply, ref n, sq, t, 0); }
                        else if ((tp >> 3) == enemy) Add(ply, ref n, sq, t, 0);
                    }

                    if (!capsOnly && !moved[sq] && canCastle[color] && !IsAttacked(sq, enemy))
                    {
                        int row = y * 8;
                        for (int cd = 1; cd >= -1; cd -= 2)           // 오른쪽, 왼쪽
                        {
                            int tx = x + 2 * cd;                      // 킹이 도착할 칸
                            if (tx < 0 || tx > 7) continue;

                            int rx = x + cd;                          // 킹에서 가장 가까운 기물 찾기
                            while (rx >= 0 && rx < 8 && b[row + rx] == 0) rx += cd;
                            if (rx < 0 || rx > 7) continue;

                            if (b[row + rx] != (R | (color << 3)) || moved[row + rx]) continue;   // 내 팀의 안 움직인 룩이어야 함
                            if (b[row + tx] != 0) continue;                                       // 킹 도착 칸은 비어 있어야 함
                            if (IsAttacked(row + x + cd, enemy)) continue;                        // 지나가는 칸이 공격받으면 AI는 피함

                            Add(ply, ref n, sq, row + tx, FlagCastle);
                        }
                    }
                    break;
            }
        }
        return n;
    }

    private void GenSlide(int ply, ref int n, int sq, int x, int y, int d0, int d1, int enemy, bool capsOnly)
    {
        for (int d = d0; d < d1; d++)
        {
            int nx = x + DX[d], ny = y + DY[d];
            while (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
            {
                int t = ny * 8 + nx;
                int tp = b[t];
                if (tp == 0)
                {
                    if (!capsOnly) Add(ply, ref n, sq, t, 0);
                }
                else
                {
                    if ((tp >> 3) == enemy) Add(ply, ref n, sq, t, 0);
                    break;
                }
                nx += DX[d]; ny += DY[d];
            }
        }
    }

    // ───────────── 공격 판정 (메모리 할당 없음) ─────────────

    /// <summary>sq 칸이 byColor 색 기물에게 공격받는가</summary>
    private bool IsAttacked(int sq, int byColor)
    {
        if (sq < 0) return false;
        int x = sq & 7, y = sq >> 3;
        int cb = byColor << 3;

        // 폰: byColor 폰이 sq를 대각선 앞으로 때린다 → 폰은 (x±1, y - dir)에 있다
        int pdir = byColor == 0 ? 1 : -1;
        int py = y - pdir;
        if (py >= 0 && py <= 7)
        {
            if (x > 0 && b[py * 8 + x - 1] == (P | cb)) return true;
            if (x < 7 && b[py * 8 + x + 1] == (P | cb)) return true;
        }

        // 나이트
        for (int k = 0; k < 8; k++)
        {
            int nx = x + KnX[k], ny = y + KnY[k];
            if (nx < 0 || nx > 7 || ny < 0 || ny > 7) continue;
            if (b[ny * 8 + nx] == (N | cb)) return true;
        }

        // 킹
        for (int k = 0; k < 8; k++)
        {
            int nx = x + DX[k], ny = y + DY[k];
            if (nx < 0 || nx > 7 || ny < 0 || ny > 7) continue;
            if (b[ny * 8 + nx] == (K | cb)) return true;
        }

        // 직선 (룩/퀸)
        for (int d = 0; d < 4; d++)
        {
            int nx = x + DX[d], ny = y + DY[d];
            while (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
            {
                int tp = b[ny * 8 + nx];
                if (tp != 0)
                {
                    if (tp == (R | cb) || tp == (Q | cb)) return true;
                    break;
                }
                nx += DX[d]; ny += DY[d];
            }
        }

        // 대각선 (비숍/퀸)
        for (int d = 4; d < 8; d++)
        {
            int nx = x + DX[d], ny = y + DY[d];
            while (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
            {
                int tp = b[ny * 8 + nx];
                if (tp != 0)
                {
                    if (tp == (B | cb) || tp == (Q | cb)) return true;
                    break;
                }
                nx += DX[d]; ny += DY[d];
            }
        }

        return false;
    }

    // ───────────── 평가 (둘 차례인 쪽 기준, 대칭) ─────────────

    private static readonly int[] PstPawn = {
          0,  0,  0,  0,  0,  0,  0,  0,
          5, 10, 10,-20,-20, 10, 10,  5,
          5, -5,-10,  0,  0,-10, -5,  5,
          0,  0,  0, 20, 20,  0,  0,  0,
          5,  5, 10, 25, 25, 10,  5,  5,
         10, 10, 20, 30, 30, 20, 10, 10,
         50, 50, 50, 50, 50, 50, 50, 50,
          0,  0,  0,  0,  0,  0,  0,  0 };

    private static readonly int[] PstKnight = {
        -50,-40,-30,-30,-30,-30,-40,-50,
        -40,-20,  0,  5,  5,  0,-20,-40,
        -30,  5, 10, 15, 15, 10,  5,-30,
        -30,  0, 15, 20, 20, 15,  0,-30,
        -30,  5, 15, 20, 20, 15,  5,-30,
        -30,  0, 10, 15, 15, 10,  0,-30,
        -40,-20,  0,  0,  0,  0,-20,-40,
        -50,-40,-30,-30,-30,-30,-40,-50 };

    private static readonly int[] PstBishop = {
        -20,-10,-10,-10,-10,-10,-10,-20,
        -10,  5,  0,  0,  0,  0,  5,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10,  5,  5, 10, 10,  5,  5,-10,
        -10,  0,  5, 10, 10,  5,  0,-10,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -20,-10,-10,-10,-10,-10,-10,-20 };

    private static readonly int[] PstRook = {
          0,  0,  0,  5,  5,  0,  0,  0,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  0,  0,  0,  0, -5,
          5, 10, 10, 10, 10, 10, 10,  5,
          0,  0,  0,  0,  0,  0,  0,  0 };

    private static readonly int[] PstQueen = {
        -20,-10,-10, -5, -5,-10,-10,-20,
        -10,  0,  5,  0,  0,  0,  0,-10,
        -10,  5,  5,  5,  5,  5,  0,-10,
          0,  0,  5,  5,  5,  5,  0, -5,
         -5,  0,  5,  5,  5,  5,  0, -5,
        -10,  0,  5,  5,  5,  5,  0,-10,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -20,-10,-10, -5, -5,-10,-10,-20 };

    private static readonly int[] PstKingMid = {
         20, 30, 10,  0,  0, 10, 30, 20,
         20, 20,  0,  0,  0,  0, 20, 20,
        -10,-20,-20,-20,-20,-20,-20,-10,
        -20,-30,-30,-40,-40,-30,-30,-20,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30 };

    private static readonly int[] PstKingEnd = {
        -50,-30,-30,-30,-30,-30,-30,-50,
        -30,-30,  0,  0,  0,  0,-30,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-20,-10,  0,  0,-10,-20,-30,
        -50,-40,-30,-20,-20,-30,-40,-50 };

    private static readonly int[] PassedBonus = { 0, 5, 10, 20, 35, 60, 100, 0 };

    private int SlideCount(int x, int y, int d0, int d1, int enemy)
    {
        int cnt = 0;
        for (int d = d0; d < d1; d++)
        {
            int nx = x + DX[d], ny = y + DY[d];
            while (nx >= 0 && nx < 8 && ny >= 0 && ny < 8)
            {
                int tp = b[ny * 8 + nx];
                if (tp == 0) cnt++;
                else { if ((tp >> 3) == enemy) cnt++; break; }
                nx += DX[d]; ny += DY[d];
            }
        }
        return cnt;
    }

    private int Evaluate()
    {
        // 1) 폰 구조 정보 + 비폰 재료량(게임 단계) 수집
        Array.Clear(pawnCnt, 0, 16);
        Array.Clear(bishopCnt, 0, 2);
        for (int f = 0; f < 8; f++) { whiteMinY[f] = 8; blackMaxY[f] = -1; }

        int npm = 0;
        for (int sq = 0; sq < 64; sq++)
        {
            int pc = b[sq];
            if (pc == 0) continue;
            int t = pc & 7;
            int c = pc >> 3;
            int x = sq & 7, y = sq >> 3;
            if (t == P)
            {
                pawnCnt[c * 8 + x]++;
                if (c == 0) { if (y < whiteMinY[x]) whiteMinY[x] = y; }
                else { if (y > blackMaxY[x]) blackMaxY[x] = y; }
            }
            else if (t != K)
            {
                npm += PieceVal[t];
                if (t == B) bishopCnt[c]++;
            }
        }

        int egw = 256 - 256 * Math.Min(npm, 3000) / 3000; // 0(초중반) ~ 256(엔드게임)

        evalScore[0] = 0; evalScore[1] = 0;

        for (int sq = 0; sq < 64; sq++)
        {
            int pc = b[sq];
            if (pc == 0) continue;
            int t = pc & 7;
            int c = pc >> 3;
            int e = c ^ 1;
            int x = sq & 7, y = sq >> 3;
            int r = c == 0 ? y : 7 - y;     // 내 진영 기준 줄 (0 = 내 뒷줄)
            int pst = r * 8 + x;
            int v = 0;

            switch (t)
            {
                case P:
                    {
                        v = 100 + PstPawn[pst];

                        // 통과 폰
                        bool passed = true;
                        for (int f = x - 1; f <= x + 1 && passed; f++)
                        {
                            if (f < 0 || f > 7) continue;
                            if (c == 0) { if (blackMaxY[f] > y) passed = false; }
                            else { if (whiteMinY[f] < y) passed = false; }
                        }
                        if (passed) v += PassedBonus[r] + PassedBonus[r] * egw / 512;

                        // 겹친 폰 / 고립 폰
                        if (pawnCnt[c * 8 + x] > 1) v -= 8;
                        bool isolated = true;
                        if (x > 0 && pawnCnt[c * 8 + x - 1] > 0) isolated = false;
                        if (x < 7 && pawnCnt[c * 8 + x + 1] > 0) isolated = false;
                        if (isolated) v -= 10;
                        break;
                    }
                case N:
                    v = 320 + PstKnight[pst];
                    break;
                case B:
                    v = 330 + PstBishop[pst] + 2 * SlideCount(x, y, 4, 8, e);
                    break;
                case R:
                    {
                        v = 500 + PstRook[pst] + SlideCount(x, y, 0, 4, e);
                        int own = pawnCnt[c * 8 + x];
                        int opp = pawnCnt[e * 8 + x];
                        if (own == 0) v += (opp == 0) ? 20 : 10;
                        break;
                    }
                case Q:
                    v = 900 + PstQueen[pst] + SlideCount(x, y, 0, 8, e) / 2;
                    break;
                case K:
                    {
                        int mid = PstKingMid[pst];
                        int end = PstKingEnd[pst];
                        v = (mid * (256 - egw) + end * egw) / 256;

                        // 킹 앞 폰 방패 (초중반에만 의미)
                        int dir = c == 0 ? 1 : -1;
                        int shield = 0;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx > 7) continue;
                            int y1 = y + dir, y2 = y + 2 * dir;
                            if (y1 >= 0 && y1 <= 7 && b[y1 * 8 + nx] == (P | (c << 3))) shield += 10;
                            else if (y2 >= 0 && y2 <= 7 && b[y2 * 8 + nx] == (P | (c << 3))) shield += 5;
                        }
                        v += shield * (256 - egw) / 256;
                        break;
                    }
            }

            evalScore[c] += v;
        }

        if (bishopCnt[0] >= 2) evalScore[0] += 30;
        if (bishopCnt[1] >= 2) evalScore[1] += 30;

        int diff = evalScore[0] - evalScore[1];
        return (side == 0 ? diff : -diff) + 8; // 둘 차례 보너스
    }
}