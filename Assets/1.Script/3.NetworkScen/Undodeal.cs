using System;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  조건부 무르기 협상 (AI 대전 / 멀티 공용)
//
//  [개념]
//   - "내가 둔 수 N개"를 무른다. (무른 뒤에는 항상 내 차례)
//   - 대신 조건(칩)을 건다:  규칙 금지(앙파상/프로모션/캐슬링, N턴 또는 이번 판 내내) / 내 기물 제거(종류 + 개수)
//   - 조건이 환산한 "수"의 합이 N 이상이어야 제안할 수 있다.
//   - 순서: ① 무르기 → ② 기물 제거 → ③ 규칙 금지
//   - 기물 칩은 "종류 + 개수"만 정한다. 실제로 사라질 기물은 규칙으로 자동 결정된다.
//     (킹 제외, 무르는 구간에서 움직인 칸의 기물 제외, 기본: 가장 앞으로 나간 기물부터)
//     → 무르기를 먼저 해도 같은 기물이 지워지고, 무른 뒤에 생긴 기물을 지우는 악용도 막힌다
// ─────────────────────────────────────────────────────────────────────────────



[Serializable]
public struct DealRuleChip
{
    public DealRule rule;
    /// <summary>"내 차례"를 기준으로 몇 번 동안 금지할지. 0 이하 = 이번 판 내내</summary>
    public int turns;
}

[Serializable]
public struct DealPieceChip
{
    public pieceType type;   // 폰/나이트/비숍/룩/퀸 (킹 불가)
    public int count;        // 몇 개 제거할지
}

public struct DealResult
{
    public PieceColor mover;        // 제안한 쪽
    public bool success;            // 무르기가 실행되었나
    public DealResponse response;   // 상대의 응답 (시간 초과 포함)
    public float chance;            // 거절당했을 때의 성공 확률(%) - 수락/시간 초과면 0
    public int undonePlies;         // 실제로 되돌린 수(승급 기록 제외)
}

/// <summary>제안 한 건의 내용</summary>
public class UndoDeal
{
    public int undoCount;                                                   // "내가 둔 수" 기준 몇 수
    public readonly List<DealRuleChip> rules = new List<DealRuleChip>();
    public readonly List<DealPieceChip> pieceChips = new List<DealPieceChip>();   // 제거할 내 기물 (종류 + 개수)

    /// <summary>RPC로 보내기 위한 바이트 묶음: [N][규칙수]([규칙][턴])*[기물칩수]([종류][개수])*</summary>
    public byte[] ToBytes()
    {
        int r = Mathf.Min(rules.Count, 3);
        int p = Mathf.Min(pieceChips.Count, 5);
        var data = new byte[3 + r * 2 + p * 2];
        int o = 0;

        data[o++] = (byte)Mathf.Clamp(undoCount, 0, 255);
        data[o++] = (byte)r;
        for (int i = 0; i < r; i++)
        {
            data[o++] = (byte)rules[i].rule;
            data[o++] = (byte)Mathf.Clamp(rules[i].turns, 0, 255);
        }
        data[o++] = (byte)p;
        for (int i = 0; i < p; i++)
        {
            data[o++] = (byte)pieceChips[i].type;
            data[o++] = (byte)Mathf.Clamp(pieceChips[i].count, 0, 16);
        }
        return data;
    }

    public static bool TryFromBytes(byte[] data, out UndoDeal deal)
    {
        deal = null;
        if (data == null || data.Length < 3) return false;

        int o = 0;
        var d = new UndoDeal { undoCount = data[o++] };

        int r = data[o++];
        if (r > 3 || data.Length < 3 + r * 2) return false;
        for (int i = 0; i < r; i++)
        {
            int rule = data[o++];
            int turns = data[o++];
            if (rule > (int)DealRule.Castling) return false;
            d.rules.Add(new DealRuleChip { rule = (DealRule)rule, turns = turns });
        }

        int p = data[o++];
        if (p > 5 || data.Length != o + p * 2) return false;
        for (int i = 0; i < p; i++)
        {
            int type = data[o++];
            int count = data[o++];
            if (!Enum.IsDefined(typeof(pieceType), type)) return false;
            d.pieceChips.Add(new DealPieceChip { type = (pieceType)type, count = count });
        }

        deal = d;
        return true;
    }
}

/// <summary>조건 → "무를 수" 환산표와 규칙값. 인스펙터에서 조정하세요.</summary>
[Serializable]
public class UndoDealConfig
{
    [Min(1)] public int maxUndoPerDeal = 10;          // 한 번에 최대 몇 수까지
    [Min(1)] public int ruleTurnsPerCredit = 3;       // 규칙 금지 3턴 = 1수 (올림)
    [Min(1)] public int ruleWholeMatchCredit = 4;     // 규칙을 이번 판 내내 금지 = 4수
    [Min(1)] public int pieceCostPerCredit = 3;       // 기물 1개 = 코스트/3 수 (퀸 9 = 3수, 최소 1수)
    public bool removeMostAdvancedFirst = true;       // 기물 칩: 가장 앞으로 나간 기물부터 사라짐 (false면 가장 뒤에 있는 기물부터)
    [Min(0)] public int cooldownTurns = 3;            // 협상 후, 차례가 이 횟수만큼 넘어갈 때까지 다시 못 함
    [Min(5f)] public float responseSeconds = 30f;     // 상대가 답할 시간 (넘기면 협상 실패)
}

/// <summary>
/// 협상으로 걸린 규칙 금지 상태 (색별). 기물 코드(King/Pawn)와 엔진이 여기서 읽습니다.
/// "차례 수(CurrentTurn)"는 차례가 넘어갈 때마다 1씩 늘어나는 값이며, 멀티에서는 방장이 정한 값을 따라갑니다.
/// </summary>
public static class RuleBans
{
    private static readonly int[] until = new int[6];

    /// <summary>지금까지 넘어간 차례 수. (AI 대전: TurnEnd마다 +1 / 멀티: NetworkGameM.TurnSerial)</summary>
    public static int CurrentTurn;

    public static bool IsBanned(PieceColor color, DealRule rule) => until[Index(color, rule)] > CurrentTurn;

    /// <summary>turns &lt;= 0 이면 이번 판 내내. 아니면 "그 색의 차례" turns번 동안 (= 차례 수 turns*2)</summary>
    public static void Ban(PieceColor color, DealRule rule, int turns)
    {
        int i = Index(color, rule);
        int end = turns <= 0 ? int.MaxValue : CurrentTurn + turns * 2;
        if (end > until[i]) until[i] = end;
    }

    /// <summary>그 색의 "남은 금지 차례 수". 0 = 금지 아님, -1 = 이번 판 내내 (UI의 프/앙/캐 숫자용)</summary>
    public static int RemainingTurns(PieceColor color, DealRule rule)
    {
        int end = until[Index(color, rule)];
        if (end <= CurrentTurn) return 0;
        if (end == int.MaxValue) return -1;
        return (end - CurrentTurn + 1) / 2;
    }

    public static void ResetAll()
    {
        Array.Clear(until, 0, until.Length);
        CurrentTurn = 0;
    }

    private static int Index(PieceColor color, DealRule rule) => (color == PieceColor.white ? 0 : 3) + (int)rule;
}

/// <summary>무르기 협상이 판(체스판 매니저)에 요구하는 기능. SingleChessGameM / CheseGameM이 구현합니다.</summary>
public interface IUndoBoard
{
    int HistoryCount { get; }
    MoveData HistoryAt(int index);
    ChessPieceM PieceAt(int x, int y);

    /// <summary>기보의 마지막 기록 하나를 되돌립니다. (호출 후 HistoryCount가 1 줄어야 합니다)</summary>
    void UndoLastEntry();

    /// <summary>협상 조건으로 내 기물을 제거합니다.</summary>
    void RemovePieceByDeal(int x, int y);

    /// <summary>협상 적용이 모두 끝난 뒤 호출 (선택 해제 등 화면 정리)</summary>
    void AfterDealApplied();
}

/// <summary>무르기 협상의 규칙 계산 (판 상태를 읽기만 하는 함수와, 적용하는 함수)</summary>
public static class UndoDealLogic
{
    /// <summary>mover가 지금까지 둔 수의 개수 = 최대로 무를 수 있는 수 (승급 기록은 제외)</summary>
    public static int MaxUndoable(IUndoBoard board, PieceColor mover)
    {
        int n = 0;
        for (int i = 0; i < board.HistoryCount; i++)
        {
            MoveData m = board.HistoryAt(i);
            if (!m.IsPromotion && m.MoverColor == mover) n++;
        }
        return n;
    }

    /// <summary>"내가 둔 수 count개"를 무르려면, 기보의 몇 번째 기록부터 끝까지 되돌려야 하는가 (없으면 -1)</summary>
    public static int FindStartIndex(IUndoBoard board, PieceColor mover, int count)
    {
        if (count < 1) return -1;

        int found = 0;
        for (int i = board.HistoryCount - 1; i >= 0; i--)
        {
            MoveData m = board.HistoryAt(i);
            if (m.IsPromotion || m.MoverColor != mover) continue;
            found++;
            if (found == count) return i;
        }
        return -1;
    }

    /// <summary>무르는 구간에서 기물이 오갔던 칸들. 이 칸의 기물은 칩으로 걸 수 없습니다.</summary>
    public static HashSet<Vector2Int> CollectInvolvedSquares(IUndoBoard board, int startIndex)
    {
        var set = new HashSet<Vector2Int>();
        if (startIndex < 0) return set;

        for (int i = startIndex; i < board.HistoryCount; i++)
        {
            MoveData m = board.HistoryAt(i);
            set.Add(m.From);
            set.Add(m.To);

            if (m.IsEnPassant) set.Add(new Vector2Int(m.To.x, m.From.y));
            if (m.IsCastling)
            {
                int dir = m.To.x > m.From.x ? 1 : -1;
                set.Add(m.RookFrom);
                set.Add(new Vector2Int(m.From.x + dir, m.From.y));
            }
        }
        return set;
    }

    /// <summary>제거 대상이 될 수 있는 기물의 칸 (해당 종류, 내 기물, 킹 아님, involved 제외). 가장 먼저 사라질 순서로 정렬됨</summary>
    public static List<Vector2Int> EligibleSquares(IUndoBoard board, PieceColor mover, pieceType type, HashSet<Vector2Int> involved, UndoDealConfig cfg)
    {
        var list = new List<Vector2Int>();
        if (type == pieceType.King || type == pieceType.none) return list;

        bool white = mover == PieceColor.white;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceM p = board.PieceAt(x, y);
                if (p == null || p.isWhite != white || p.data.Type != type) continue;
                if (involved != null && involved.Contains(new Vector2Int(x, y))) continue;
                list.Add(new Vector2Int(x, y));
            }
        }

        // 앞으로 나간 정도 (내 진영 기준)
        int Adv(Vector2Int v) => white ? v.y : 7 - v.y;
        bool mostFirst = cfg == null || cfg.removeMostAdvancedFirst;
        list.Sort((a, b) =>
        {
            int c = mostFirst ? Adv(b).CompareTo(Adv(a)) : Adv(a).CompareTo(Adv(b));
            return c != 0 ? c : a.x.CompareTo(b.x);
        });
        return list;
    }

    /// <summary>조건들이 환산한 "무를 수"의 합</summary>
    public static int ComputeCredit(IUndoBoard board, PieceColor mover, UndoDeal deal, UndoDealConfig cfg)
    {
        int credit = 0;

        foreach (DealRuleChip chip in deal.rules)
        {
            credit += chip.turns <= 0
                ? cfg.ruleWholeMatchCredit
                : Mathf.CeilToInt(chip.turns / (float)cfg.ruleTurnsPerCredit);
        }

        int start = FindStartIndex(board, mover, deal.undoCount);
        HashSet<Vector2Int> involved = CollectInvolvedSquares(board, start);

        foreach (DealPieceChip chip in deal.pieceChips)
        {
            List<Vector2Int> eligible = EligibleSquares(board, mover, chip.type, involved, cfg);
            if (eligible.Count == 0) continue;

            ChessPieceM sample = board.PieceAt(eligible[0].x, eligible[0].y);
            int perPiece = Mathf.Max(1, sample.data.cost / cfg.pieceCostPerCredit);
            credit += perPiece * Mathf.Min(chip.count, eligible.Count);
        }
        return credit;
    }

    /// <summary>제안이 규칙에 맞는지 검사합니다. (UI의 미리보기와 방장의 심사에서 같이 씁니다)</summary>
    public static bool Validate(IUndoBoard board, PieceColor mover, UndoDeal deal, UndoDealConfig cfg, out string reason)
    {
        reason = null;

        if (deal == null || deal.undoCount < 1) { reason = "무를 수를 정해 주세요."; return false; }
        if (deal.undoCount > cfg.maxUndoPerDeal) { reason = $"한 번에 최대 {cfg.maxUndoPerDeal}수까지 무를 수 있어요."; return false; }

        int max = MaxUndoable(board, mover);
        if (deal.undoCount > max) { reason = $"무를 수 있는 수는 최대 {max}수예요."; return false; }

        var seenRules = new HashSet<DealRule>();
        foreach (DealRuleChip chip in deal.rules)
        {
            if (!seenRules.Add(chip.rule)) { reason = "같은 규칙을 두 번 걸 수 없어요."; return false; }
            if (chip.turns < 0 || chip.turns > 100) { reason = "금지 기간이 올바르지 않아요."; return false; }
            if (RuleBans.IsBanned(mover, chip.rule)) { reason = "이미 금지 중인 규칙이에요."; return false; }
        }

        int start = FindStartIndex(board, mover, deal.undoCount);
        HashSet<Vector2Int> involved = CollectInvolvedSquares(board, start);

        var seenTypes = new HashSet<pieceType>();
        foreach (DealPieceChip chip in deal.pieceChips)
        {
            if (chip.type == pieceType.King || chip.type == pieceType.none) { reason = "그 기물은 걸 수 없어요."; return false; }
            if (!seenTypes.Add(chip.type)) { reason = "같은 종류를 두 번 걸 수 없어요."; return false; }
            if (chip.count < 1) { reason = "기물 개수를 정해 주세요."; return false; }

            int have = EligibleSquares(board, mover, chip.type, involved, cfg).Count;
            if (chip.count > have) { reason = $"걸 수 있는 해당 기물은 {have}개예요. (무르는 구간에서 움직인 기물은 제외)"; return false; }
        }

        int credit = ComputeCredit(board, mover, deal, cfg);
        if (credit < deal.undoCount) { reason = $"조건이 부족해요. ({credit}/{deal.undoCount}수)"; return false; }

        return true;
    }

    /// <summary>
    /// 협상을 실제로 적용합니다. (모든 컴퓨터가 똑같이 실행) 되돌린 수(승급 기록 제외)를 돌려줍니다.
    /// 순서: ① 무르기 → ② 기물 제거 → ③ 규칙 금지
    /// </summary>
    public static int Apply(IUndoBoard board, PieceColor mover, UndoDeal deal, UndoDealConfig cfg)
    {
        int start = FindStartIndex(board, mover, deal.undoCount);
        if (start < 0) return 0;

        HashSet<Vector2Int> involved = CollectInvolvedSquares(board, start);   // 무르기 전에 계산

        int undonePlies = 0;
        while (board.HistoryCount > start)
        {
            int before = board.HistoryCount;
            if (!board.HistoryAt(before - 1).IsPromotion) undonePlies++;

            board.UndoLastEntry();
            if (board.HistoryCount >= before) break;   // 안전장치: 줄지 않으면 무한 반복 방지
        }

        foreach (DealPieceChip chip in deal.pieceChips)
        {
            List<Vector2Int> targets = EligibleSquares(board, mover, chip.type, involved, cfg);
            int n = Mathf.Min(chip.count, targets.Count);
            for (int i = 0; i < n; i++) board.RemovePieceByDeal(targets[i].x, targets[i].y);
        }

        foreach (DealRuleChip chip in deal.rules) RuleBans.Ban(mover, chip.rule, chip.turns);

        board.AfterDealApplied();
        return undonePlies;
    }

    // ───────────── 협상력 / 성공 확률 (AI 대전과 같은 공식) ─────────────

    /// <summary>판 위에 있는 내 기물의 코스트 합 (0~100)</summary>
    public static float Power(IUndoBoard board, PieceColor color)
    {
        bool white = color == PieceColor.white;
        float sum = 0f;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceM p = board.PieceAt(x, y);
                if (p != null && p.isWhite == white) sum += p.data.cost;
            }
        }
        return Mathf.Clamp(sum, 0f, 100f);
    }

    /// <summary>상대가 거절했을 때 그래도 무르기가 성사될 확률(%). 최소 10, 최대 90</summary>
    public static float SuccessChance(float myPower, float opponentPower)
    {
        return Mathf.Clamp(10f + (myPower - opponentPower), 10f, 90f);
    }
}