using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct PieceSpawnInfo
{
    public Vector2Int position; // 배치할 체스판 좌표 (예: x: 0, y: 1)
    public pieceType pieceType; // 기물 종류 (폰, 룩, 나이트 등)
    public PieceColor pieceColor; // 팀 색상
}

// 임시 이동 구조체
public struct MoveBestMove
{
    public Vector2Int from;
    public Vector2Int to;
    public bool isValid;
}

// 가상 시뮬레이션용 기물 데이터 구조체
public struct SimPieceData
{
    public pieceType type;
    public PieceColor color;
    public Vector2Int pos;
    public bool hasMoved;
    public bool movedTwoSquaresLastTurn;

    public SimPieceData(pieceType type, PieceColor color, Vector2Int pos, bool hasMoved, bool movedTwoSquaresLastTurn)
    {
        this.type = type;
        this.color = color;
        this.pos = pos;
        this.hasMoved = hasMoved;
        this.movedTwoSquaresLastTurn = movedTwoSquaresLastTurn;
    }
}

public class Characters : MonoBehaviour
{
    [Header("1. 캐릭터 기본 데이터")]
    public int characterId;
    public Image icon;
    public characterData characterData;
    public CharacterType characterType;
    public bool isUnlocked;


    [Header("2. 기물 초기 배치 정보")]
    public PieceDatabase pieceDatabase;
    public List<PieceSpawnInfo> defaultPieceLayout;

    [Header("AI 생각 깊이 (숫자가 높을수록 똑똑하지만 연산이 많아집니다)")]
    public int searchDepth;

    private CheseGameM gameM;

    private bool allowCastling;
    private bool allowEnPassant;
    private bool allowPromotion;

    private void Awake()
    {
        ScriptM.Register<Characters>(this,UseSpace.Local);

        if (ScriptM.TryGet<CheseGameM>(out CheseGameM _gameM))
        {
            gameM = _gameM;
        }

        allowCastling = gameM.canUseCastling;
        allowEnPassant = gameM.canUseEnPassant;
        allowPromotion = gameM.canUsePromotion;

    }

    private void OnDestroy()
    {
        ScriptM.Unregister<Characters>();  
    }

    /// <summary>
    /// 설정된 배치 정보(defaultPieceLayout)를 바탕으로 체스판에 기물을 동적 생성합니다.
    /// </summary>
    public virtual void SetupBoard(CheseGameM gameMa) 
    {
        gameM = gameMa;

        if (defaultPieceLayout == null || defaultPieceLayout.Count == 0)
        {
            Debug.LogWarning("배치할 기물 데이터가 없습니다!");
            return;
        }

        foreach (var spawnInfo in defaultPieceLayout)
        {
            // 1. 피스 타입과 컬러에 맞는 프리팹 가져오기 
            GameObject prefabToSpawn = pieceDatabase.GetPrefab(spawnInfo.pieceType, spawnInfo.pieceColor);

            if (prefabToSpawn != null)
            {
                gameM.SpawnPiece(prefabToSpawn, spawnInfo.position.x, spawnInfo.position.y,PieceColor.black);
            }
            else
            {
                Debug.LogError($"해당 타입({spawnInfo.pieceType})과 색상({spawnInfo.pieceColor})의 프리팹을 찾을 수 없습니다.");
            }
        }

        Debug.Log("AI 기물 동적 배치 완료!");
    }

    // AI 이동 결정 함수 (가상 함수로 만들어 캐릭터마다 다르게 오버라이드 가능)
    public virtual async UniTask OnAITurnAsync()
    {
        SimPieceData[,] boardCopy = CreateBoardDataCopy();

        Vector2Int bestStart = default;
        Vector2Int bestTarget = default;
        int bestEval = -9999;
        bool foundMove = false;

        // 🔥 핵심: 최대 탐색 깊이(예: 5단계)만큼 버퍼를 미리 딱 1번만 생성합니다. (Zero-GC!)
        const int maxDepth = 5;
        List<Vector2Int>[] depthBuffers = new List<Vector2Int>[maxDepth + 1];
        for (int i = 0; i <= maxDepth; i++)
        {
            depthBuffers[i] = new List<Vector2Int>(32);
        }

        await Task.Run(() =>
        {
            Stack<SimUndoData> localUndoStack = new Stack<SimUndoData>();
            int width = boardCopy.GetLength(0);
            int height = boardCopy.GetLength(1);

            // 내 차례(Maximizing) 탐색
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    SimPieceData piece = boardCopy[x, y];
                    if (piece.type == pieceType.none || piece.color != myColor) continue;

                    // 현재 depth(예: 3)에 해당하는 버퍼 가져오기
                    List<Vector2Int> moves = depthBuffers[3];
                    moves.Clear();
                    GetPossibleMovesDataOnly(boardCopy, piece.pos, moves);

                    foreach (var target in moves)
                    {
                        SimulateMoveDataOnly(boardCopy, piece.pos, target, localUndoStack);

                        if (IsKingInCheckDataOnly(boardCopy, myColor))
                        {
                            UndoMoveDataOnly(boardCopy, localUndoStack.Pop());
                            continue;
                        }

                        // 재귀 호출 시 다음 depth(2)의 버퍼를 전달!
                        int eval = Minimax(boardCopy, 2, false, -9999, 9999, localUndoStack, depthBuffers);

                        UndoMoveDataOnly(boardCopy, localUndoStack.Pop());

                        if (eval > bestEval)
                        {
                            bestEval = eval;
                            bestStart = piece.pos;
                            bestTarget = target;
                            foundMove = true;
                        }
                    }
                }
            }
        });

        if (foundMove)
        {
            ExecuteMove(bestStart, bestTarget);
            gameM.TurnEnd();
        }
    }



    // 미니맥스 알고리즘 (나와 상대방의 수를 번갈아가며 시뮬레이션하는 두뇌)
    private int Minimax(SimPieceData[,] boardState, int depth, bool isMaximizing, int alpha, int beta, Stack<SimUndoData> undoStack, List<Vector2Int>[] depthBuffers)
    {
        int width = boardState.GetLength(0);
        int height = boardState.GetLength(1);

        if (depth == 0)
        {
            return EvaluateBoardDataOnly(boardState);
        }

        // 적 AI는 항상 검정색(Black)이라고 고정 가정
        PieceColor enemyColor = PieceColor.black;

        // 현재 내 색상 판별 (Maximizing 여부에 따라)
        PieceColor turnColor = isMaximizing ? myColor : enemyColor;

        // 🔥 핵심: 현재 내 재귀 깊이(depth) 전용 버퍼를 가져옴 (부모와 자식이 절대 충돌 안 함!)
        List<Vector2Int> moves = depthBuffers[depth];

        if (isMaximizing)
        {
            int maxEval = -9999;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    SimPieceData piece = boardState[x, y];
                    if (piece.type == pieceType.none || piece.color != turnColor) continue;

                    moves.Clear();
                    GetPossibleMovesDataOnly(boardState, piece.pos, moves);

                    foreach (var target in moves)
                    {
                        SimulateMoveDataOnly(boardState, piece.pos, target, undoStack);

                        if (IsKingInCheckDataOnly(boardState, turnColor))
                        {
                            UndoMoveDataOnly(boardState, undoStack.Pop());
                            continue;
                        }

                        int eval = Minimax(boardState, depth - 1, false, alpha, beta, undoStack, depthBuffers);
                        UndoMoveDataOnly(boardState, undoStack.Pop());

                        maxEval = Mathf.Max(maxEval, eval);
                        alpha = Mathf.Max(alpha, eval);
                        if (beta <= alpha) break;
                    }
                    if (beta <= alpha) break;
                }
            }
            return maxEval;
        }
        else
        {
            int minEval = 9999;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    SimPieceData piece = boardState[x, y];
                    if (piece.type == pieceType.none || piece.color != turnColor) continue;

                    moves.Clear();
                    GetPossibleMovesDataOnly(boardState, piece.pos, moves);

                    foreach (var target in moves)
                    {
                        SimulateMoveDataOnly(boardState, piece.pos, target, undoStack);

                        if (IsKingInCheckDataOnly(boardState, turnColor))
                        {
                            UndoMoveDataOnly(boardState, undoStack.Pop());
                            continue;
                        }

                        int eval = Minimax(boardState, depth - 1, true, alpha, beta, undoStack, depthBuffers);
                        UndoMoveDataOnly(boardState, undoStack.Pop());

                        minEval = Mathf.Min(minEval, eval);
                        beta = Mathf.Min(beta, eval);
                        if (beta <= alpha) break;
                    }
                    if (beta <= alpha) break;
                }
            }
            return minEval;
        }
    }

    private int EvaluateBoardDataOnly(SimPieceData[,] boardState)
    {
        int totalEvaluation = 0;
        int width = boardState.GetLength(0);
        int height = boardState.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SimPieceData piece = boardState[x, y];
                if (piece.type == pieceType.none) continue;

                // 1. 기본 기물 가치 합산
                int pieceValue = GetPieceValue(piece.type);

                // 2. 위치 보너스 합산 (색상별 분기 처리 추가)
                int positionBonus = 0;
                if (piece.color == PieceColor.black)
                {
                    positionBonus = GetBlackPositionBonusDataOnly(piece);
                }
                else
                {
                    // 백 기물인 경우 테이블 대칭 적용 (예: y축 반전)
                    SimPieceData flippedPiece = piece;
                    flippedPiece.pos = new Vector2Int(piece.pos.x, 7 - piece.pos.y);
                    positionBonus = GetBlackPositionBonusDataOnly(flippedPiece);
                }

                // 3. 안전도 페널티 적용
                int safetyPenalty = EvaluateCurrentPiecesSafetyDataOnly(boardState, piece, piece.pos);

                // 최종 기물 점수 계산
                int finalPieceScore = pieceValue + positionBonus + safetyPenalty;

                // 내 팀이면 플러스, 적 팀이면 마이너스 (Maximizing 기준)
                if (piece.color == myColor)
                {
                    totalEvaluation += finalPieceScore;
                }
                else
                {
                    totalEvaluation -= finalPieceScore;
                }
            }
        }

        return totalEvaluation;
    }

    // 기물 종류별 점수표
    private int GetPieceValue(pieceType type)
    {
        switch (type)
        {
            case pieceType.Pawn: return 100;
            case pieceType.Knight: return 300;
            case pieceType.Bishop: return 300;
            case pieceType.Rook: return 500;
            case pieceType.Queen: return 900;
            case pieceType.King: return 10000;
            default: return 0;
        }
    }

    // 5. [공격 받는지 체크] 순수 데이터 배열을 순회하며 적 기물이 해당 칸을 타격할 수 있는지 검사
    private bool IsSquareAttackedDataOnly(SimPieceData[,] boardState, Vector2Int targetPos, PieceColor attackerColor)
    {
        bool isWhiteEnemy = (attackerColor == PieceColor.white);

        // 적 진영의 모든 공격 가능 좌표를 한 번에 싹 모음
        List<Vector2Int> enemyMoves = GetAllEnemyAttackMovesDataOnly(isWhiteEnemy, boardState);

        // 타겟 칸이 그 공격 범위 안에 포함되어 있는지 확인 후 바로 반환
        return enemyMoves.Contains(targetPos);
    }

    private struct SimUndoData
    {
        public Vector2Int from;
        public Vector2Int to;

        // 💡 모두 순수 데이터 형식(SimPieceData)으로 통일합니다!
        public SimPieceData movedPiece;
        public SimPieceData capturedPiece;

        public bool wasPromotion;
        public pieceType originalType;
    }

    private void SimulateMoveDataOnly(SimPieceData[,] boardState, Vector2Int from, Vector2Int to, Stack<SimUndoData> undoStack)
    {
        SimPieceData movingPiece = boardState[from.x, from.y];

        // 대상 칸에 기물이 있는지 확인 (null 체크 대신 커스텀 구조체 방식 활용 가능)
        SimPieceData targetPiece = boardState[to.x, to.y];
        bool hasTarget = (targetPiece.type != pieceType.none); // 빈 칸 판별용 타입이 있다면 활용

        bool isPromotion = false;
        pieceType originalType = movingPiece.type;

        movingPiece.hasMoved = true;

        // 폰 프로모션 체크 (가상 승진)
        if (movingPiece.type == pieceType.Pawn&&(allowPromotion|| movingPiece.color != PieceColor.black))
        {
            if (to.y == 0 || to.y == 7)
            {
                isPromotion = true;
                movingPiece.type = pieceType.Queen; // 시뮬레이션 상에서만 퀸으로 변경
            }
        }

        // 🏰 캐슬링 특수 이동 처리 (킹이 2칸 이동한 경우)
        if (movingPiece.type == pieceType.King && math.abs(from.x - to.x) == 2&& !movingPiece.hasMoved && (allowCastling || movingPiece.color != PieceColor.black))
        {
            int row = to.y;
            if (from.x > to.x) // 킹사이드 캐슬링 (오른쪽)
            {
                SimPieceData rook = boardState[7, row];
                boardState[7, row] = new SimPieceData { type = pieceType.none };
                rook.hasMoved = true;
                boardState[5, row] = rook;
                boardState[5, row].pos = new Vector2Int(5, row);
            }
            else // 퀸사이드 캐슬링 (왼쪽)
            {
                SimPieceData rook = boardState[0, row];
                boardState[0, row] = new SimPieceData { type = pieceType.none };
                rook.hasMoved = true;
                boardState[3, row] = rook;
                boardState[3, row].pos = new Vector2Int(3, row);
            }
        }

        //  복원용 스택에 백업 정보 푸시 (순수 데이터만 저장하므로 백그라운드 안전!)
        undoStack.Push(new SimUndoData
        {
            from = from,
            to = to,
            movedPiece = movingPiece,
            capturedPiece = targetPiece,
            wasPromotion = isPromotion,
            originalType = originalType
        });

        // 3. 가상 보드 배열 데이터만 갱신
        boardState[from.x, from.y] = default; // 빈 칸 처리 (기본값)
        boardState[to.x, to.y] = movingPiece;  // 이동할 자리에 배치
        boardState[to.x, to.y].pos = to;      // 위치 갱신
    }
    private void UndoMoveDataOnly(SimPieceData[,] boardState, SimUndoData undoData)
    {
        // 1. 이동했던 기물을 원래 출발 자리로 복구
        boardState[undoData.from.x, undoData.from.y] = undoData.movedPiece;
        boardState[undoData.from.x, undoData.from.y].pos = undoData.from;

        // 2. 프로모션 상태였다면 원래 타입(폰 등)으로 원복
        if (undoData.wasPromotion)
        {
            boardState[undoData.from.x, undoData.from.y].type = undoData.originalType;
        }

        // 3. 원래 목적지에 있던 기물(잡혔던 기물 혹은 빈 칸) 복구
        boardState[undoData.to.x, undoData.to.y] = undoData.capturedPiece;

        // 4. 🏰 캐슬링이었던 경우, 함께 움직였던 룩도 원래 자리로 되돌리기
        // (킹이 2칸 이동했던 수였다면 룩의 위치를 원상복구합니다)
        if (undoData.movedPiece.type == pieceType.King && math.abs(undoData.to.x - undoData.from.x) == 2)
        {
            int row = undoData.from.y;
            if (undoData.to.x > undoData.from.x) // 킹사이드 캐슬링이었던 경우
            {
                // f열(5)에 있던 룩을 다시 원래 구석(7)으로 이동
                SimPieceData rook = boardState[5, row];
                boardState[5, row] = new SimPieceData { type = pieceType.none };
                rook.hasMoved = false; // 캐슬링 전 상태로 권한 복구 (선택적)
                boardState[7, row] = rook;
                boardState[7, row].pos = new Vector2Int(7, row);
            }
            else // 퀸사이드 캐슬링이었던 경우
            {
                // d열(3)에 있던 룩을 다시 원래 구석(0)으로 이동
                SimPieceData rook = boardState[3, row];
                boardState[3, row] = new SimPieceData { type = pieceType.none };
                rook.hasMoved = false; // 캐슬링 전 상태로 권한 복구 (선택적)
                boardState[0, row] = rook;
                boardState[0, row].pos = new Vector2Int(0, row);
            }
        }
    }

    private SimPieceData[,] CreateBoardDataCopy()
    {
        // 보드 크기가 8x8이라고 가정 (프로젝트 규격에 맞게 조절)
        int width = gameM.pieceGrid.GetLength(0);
        int height = gameM.pieceGrid.GetLength(1);
        SimPieceData[,] boardCopy = new SimPieceData[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                ChessPieceM piece = gameM.pieceGrid[x, y];
                if (piece != null)
                {
                    // 실제 기물의 데이터를 복사해서 순수 데이터 구조체로 생성
                    boardCopy[x, y] = new SimPieceData(piece.data.Type, piece.data.Color, new Vector2Int(x, y), true ,false);
                }
                else
                {
                    // 빈 칸은 기본값으로 처리 (type이 None이거나 비워두기)
                    boardCopy[x, y] = new SimPieceData(pieceType.none, PieceColor.none, new Vector2Int(x, y), false, false);
                }
            }
        }
        return boardCopy;
    }

    private List<Vector2Int> GetAllEnemyAttackMovesDataOnly(bool isWhiteEnemy, SimPieceData[,] boardState)
    {
        List<Vector2Int> attackMoves = new List<Vector2Int>();
        if (boardState == null) return attackMoves;

        int width = boardState.GetLength(0);
        int height = boardState.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SimPieceData piece = boardState[x, y];

                // 빈 칸이 아니고, 적 팀 기물인 경우
                if (piece.type != pieceType.none && (piece.color == PieceColor.white) == isWhiteEnemy)
                {
                    // 해당 기물의 가상 이동 가능 경로를 구해서 리스트에 추가
                    List<Vector2Int> pieceMoves = new List<Vector2Int>();
                    GetPossibleMovesDataOnly(boardState, new Vector2Int(x, y), pieceMoves);

                    attackMoves.AddRange(pieceMoves);
                }
            }
        }

        return attackMoves;
    }

    private bool IsKingInCheckDataOnly(SimPieceData[,] boardState, PieceColor kingColor)
    {
        // 1. 가상 보드판에서 해당 색상의 킹 위치를 찾습니다.
        Vector2Int kingPos = FindKingPositionDataOnly(boardState, kingColor);

        // 2. 적의 색상을 구합니다.
        PieceColor enemyColor = (kingColor == PieceColor.white) ? PieceColor.black : PieceColor.white;

        // 3. 앞서 최적화해 둔 IsSquareAttackedDataOnly를 활용해 킹의 위치가 공격받고 있는지 검사!
        return IsSquareAttackedDataOnly(boardState, kingPos, enemyColor);
    }

    // 4. 가상 보드판에서 특정 색상의 킹 위치(좌표)를 찾아주는 보조 함수
    private Vector2Int FindKingPositionDataOnly(SimPieceData[,] boardState, PieceColor kingColor)
    {
        int width = boardState.GetLength(0);
        int height = boardState.GetLength(1);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                SimPieceData piece = boardState[x, y];
                if (piece.type == pieceType.King && piece.color == kingColor)
                {
                    return new Vector2Int(x, y);
                }
            }
        }
        return new Vector2Int(-1, -1); // 못 찾은 경우 (이럴 일은 없지만 안전장치)
    }
    protected PieceColor myColor = PieceColor.black;

    private void GetPossibleMovesDataOnly(SimPieceData[,] boardState, Vector2Int fromPos, List<Vector2Int> outMoves)
    {
        SimPieceData piece = boardState[fromPos.x, fromPos.y];
        if (piece.type == pieceType.none) return;

        // 💡 gameM.pieceGrid를 절대 보지 않고, 오직 순수 데이터 배열인 boardState만 이용!
        switch (piece.type)
        {
            case pieceType.Pawn:

                if (piece.type == pieceType.none) return;

                // 백은 위로(+1), 흑은 아래로(-1) 전진 (보드 좌표 방향에 맞게 조절)
                int direction = (piece.color == PieceColor.white) ? 1 : -1;

                // 1. 앞으로 1칸 전진
                int forwardY = fromPos.y + direction;
                if (IsValidIndex(fromPos.x, forwardY) && boardState[fromPos.x, forwardY].type == pieceType.none)
                {
                    outMoves.Add(new Vector2Int(fromPos.x, forwardY));

                    // 2. 처음 움직일 때 2칸 전진 (백은 y=1, 흑은 y=6 시작점 기준)
                    bool isFirstMove = (piece.color == PieceColor.white && fromPos.y == 1) ||
                                       (piece.color == PieceColor.black && fromPos.y == 6);

                    int doubleForwardY = fromPos.y + (direction * 2);
                    if (isFirstMove && IsValidIndex(fromPos.x, doubleForwardY) && boardState[fromPos.x, doubleForwardY].type == pieceType.none)
                    {
                        outMoves.Add(new Vector2Int(fromPos.x, doubleForwardY));
                    }
                }

                // 3. 대각선 적 기물 포획
                int[] dx = { -1, 1 };
                foreach (int d in dx)
                {
                    int targetX = fromPos.x + d;
                    int targetY = fromPos.y + direction;

                    if (IsValidIndex(targetX, targetY))
                    {
                        SimPieceData targetPiece = boardState[targetX, targetY];
                        // 빈 칸이 아니고, 색상이 다른 적 기물인 경우
                        if (targetPiece.type != pieceType.none && targetPiece.color != piece.color)
                        {
                            outMoves.Add(new Vector2Int(targetX, targetY));
                        }
                    }

                    // 4. 앙파상 (En Passant) 데이터 검사
                    // 옆 칸에 적 폰이 있고, 직전에 2칸 움직인 기록(movedTwoSquaresLastTurn)이 있는 경우
                    int sideX = targetX;
                    int sideY = fromPos.y;
                    if (IsValidIndex(sideX, sideY)&& allowEnPassant)
                    {
                        SimPieceData sidePiece = boardState[sideX, sideY];
                        if (sidePiece.type == pieceType.Pawn && sidePiece.color != piece.color && sidePiece.movedTwoSquaresLastTurn)
                        {
                            int targetCaptureY = fromPos.y + direction;
                            if (IsValidIndex(sideX, targetCaptureY))
                            {
                                outMoves.Add(new Vector2Int(sideX, targetCaptureY));
                            }
                        }
                    }
                }

                // 💡 5. 프로모션(Promotion) 안내
                // 순수 데이터 시뮬레이션에서는 도착 좌표 자체는 일반 전진/대각선과 똑같이 추가됩니다.
                // 대신 가상 이동을 실행하는 `SimulateMoveDataOnly` 안에서 
                // 폰이 맨 끝 행(백 y=7, 흑 y=0)에 도달했을 때 타입을 자동으로 `pieceType.Queen`으로 승격시켜 주면 됩니다!
                break;

            case pieceType.Knight:
                Vector2Int[] knightMoves = new Vector2Int[]
    {
                    new Vector2Int(-1, 2), new Vector2Int(1, 2),   // 위쪽 L자
                    new Vector2Int(-1, -2), new Vector2Int(1, -2), // 아래쪽 L자
                    new Vector2Int(2, 1), new Vector2Int(2, -1),   // 오른쪽 L자
                    new Vector2Int(-2, 1), new Vector2Int(-2, -1)  // 왼쪽 L자
    };

                foreach (Vector2Int move in knightMoves)
                {
                    int targetX = fromPos.x + move.x;
                    int targetY = fromPos.y + move.y;

                    // 1. 체스판 범위를 벗어나는지 검사
                    if (IsValidIndex(targetX, targetY))
                    {
                        SimPieceData targetPiece = boardState[targetX, targetY];

                        // 2. 빈 칸이거나(type == none) 색상이 다른 적 기물인 경우 이동 가능
                        if (targetPiece.type == pieceType.none || targetPiece.color != piece.color)
                        {
                            outMoves.Add(new Vector2Int(targetX, targetY));
                        }
                    }
                }
                break;

            case pieceType.Bishop:
                {
                    // 대각선 4방향 정의
                    Vector2Int[] directions = new Vector2Int[]
                    {
                        new Vector2Int(1, 1),   // 우상단
                        new Vector2Int(1, -1),  // 우하단
                        new Vector2Int(-1, 1),  // 좌상단
                        new Vector2Int(-1, -1)  // 좌하단
                    };

                    foreach (Vector2Int dir in directions)
                    {
                        int nextX = fromPos.x + dir.x;
                        int nextY = fromPos.y + dir.y;

                        // 보드 경계를 벗어나지 않을 때까지 대각선 탐색
                        while (IsValidIndex(nextX, nextY))
                        {
                            SimPieceData targetPiece = boardState[nextX, nextY];

                            // 1. 빈 칸인 경우 -> 이동 목록에 추가하고 계속 탐색
                            if (targetPiece.type == pieceType.none)
                            {
                                outMoves.Add(new Vector2Int(nextX, nextY));
                            }
                            else
                            {
                                // 2. 적 기수인 경우 (색상이 다른 경우) -> 잡기 목록에 추가하고 해당 방향 탐색 종료
                                if (targetPiece.color != piece.color)
                                {
                                    outMoves.Add(new Vector2Int(nextX, nextY));
                                }
                                // 3. 아군을 만났거나 적을 잡았으므로 길 막힘 -> 루프 탈출
                                break;
                            }

                            // 다음 대각선 칸으로 전진
                            nextX += dir.x;
                            nextY += dir.y;
                        }
                    }
                    break;
                }

            case pieceType.Rook:
                {
                    // 직선 4방향 정의 (상, 하, 우, 좌)
                    Vector2Int[] directions = new Vector2Int[]
                    {
                        new Vector2Int(0, 1),   // 위
                        new Vector2Int(0, -1),  // 아래
                        new Vector2Int(1, 0),   // 오른쪽
                        new Vector2Int(-1, 0)   // 왼쪽
                    };

                    foreach (Vector2Int dir in directions)
                    {
                        int nextX = fromPos.x + dir.x;
                        int nextY = fromPos.y + dir.y;

                        // 보드 경계를 벗어나지 않을 때까지 직선 탐색
                        while (IsValidIndex(nextX, nextY))
                        {
                            SimPieceData targetPiece = boardState[nextX, nextY];

                            // 1. 빈 칸인 경우 -> 이동 목록에 추가하고 계속 탐색
                            if (targetPiece.type == pieceType.none)
                            {
                                outMoves.Add(new Vector2Int(nextX, nextY));
                            }
                            else
                            {
                                // 2. 적 기물인 경우 (색상이 다른 경우) -> 잡기 목록에 추가하고 해당 방향 탐색 종료
                                if (targetPiece.color != piece.color)
                                {
                                    outMoves.Add(new Vector2Int(nextX, nextY));
                                }
                                // 3. 아군을 만났거나 적을 잡았으므로 길 막힘 -> 루프 탈출
                                break;
                            }

                            // 다음 직선 칸으로 전진
                            nextX += dir.x;
                            nextY += dir.y;
                        }
                    }
                    break;
                }
        

            case pieceType.Queen:
                {
                    // 8방향 정의 (직선 4방향 + 대각선 4방향)
                    Vector2Int[] directions = new Vector2Int[]
                    {
                        new Vector2Int(0, 1),   // 위
                        new Vector2Int(0, -1),  // 아래
                        new Vector2Int(1, 0),   // 오른쪽
                        new Vector2Int(-1, 0),  // 왼쪽
                        new Vector2Int(1, 1),   // 우상단
                        new Vector2Int(1, -1),  // 우하단
                        new Vector2Int(-1, 1),  // 좌상단
                        new Vector2Int(-1, -1)  // 좌하단
                    };

                    foreach (Vector2Int dir in directions)
                    {
                        int nextX = fromPos.x + dir.x;
                        int nextY = fromPos.y + dir.y;

                        // 보드 경계를 벗어나지 않을 때까지 8방향 탐색
                        while (IsValidIndex(nextX, nextY))
                        {
                            SimPieceData targetPiece = boardState[nextX, nextY];

                            // 1. 빈 칸인 경우 -> 이동 목록에 추가하고 계속 탐색
                            if (targetPiece.type == pieceType.none)
                            {
                                outMoves.Add(new Vector2Int(nextX, nextY));
                            }
                            else
                            {
                                // 2. 적 기물인 경우 (색상이 다른 경우) -> 잡기 목록에 추가하고 해당 방향 탐색 종료
                                if (targetPiece.color != piece.color)
                                {
                                    outMoves.Add(new Vector2Int(nextX, nextY));
                                }
                                // 3. 아군을 만났거나 적을 잡았으므로 길 막힘 -> 루프 탈출
                                break;
                            }

                            // 다음 칸으로 전진
                            nextX += dir.x;
                            nextY += dir.y;
                        }
                    }
                    break;
                }

            case pieceType.King:

                {
                    // 8방향 1칸씩 이동
                    Vector2Int[] directions = new Vector2Int[]
                    {
                        new Vector2Int(0, 1),   // 위
                        new Vector2Int(0, -1),  // 아래
                        new Vector2Int(1, 0),   // 오른쪽
                        new Vector2Int(-1, 0),  // 왼쪽
                        new Vector2Int(1, 1),   // 우상단
                        new Vector2Int(1, -1),  // 우하단
                        new Vector2Int(-1, 1),  // 좌상단
                        new Vector2Int(-1, -1)  // 좌하단
                    };

                    foreach (Vector2Int dir in directions)
                    {
                        int targetX = fromPos.x + dir.x;
                        int targetY = fromPos.y + dir.y;

                        if (IsValidIndex(targetX, targetY))
                        {
                            SimPieceData targetPiece = boardState[targetX, targetY];
                            // 빈 칸이거나 적 기물인 경우 이동 가능
                            if (targetPiece.type == pieceType.none || targetPiece.color != piece.color)
                            {
                                outMoves.Add(new Vector2Int(targetX, targetY));
                            }
                        }
                    }

                    // 🏰 캐슬링 (Castling) 데이터 검사
                    // 💡 주의: SimPieceData 구조체에 `bool hasMoved` 필드가 정의되어 있어야 합니다!
                    if (!piece.hasMoved)
                    {
                        int row = fromPos.y; // 백은 보통 y=0, 흑은 y=7

                        // 1. 킹사이드 캐슬링 (오른쪽 룩 방향)
                        // 룩이 존재하고 아직 안 움직였으며, 킹과 룩 사이의 칸(5, 6번 열)이 비어있는지 확인
                        SimPieceData kingsideRook = boardState[7, row];
                        if (kingsideRook.type == pieceType.Rook && !kingsideRook.hasMoved)
                        {
                            if (boardState[5, row].type == pieceType.none && boardState[6, row].type == pieceType.none)
                            {
                                // 캐슬링 시 킹이 이동할 최종 목표 칸 (x = 6)
                                outMoves.Add(new Vector2Int(6, row));
                            }
                        }

                        // 2. 퀸사이드 캐슬링 (왼쪽 룩 방향)
                        // 룩이 존재하고 아직 안 움직였으며, 사이의 칸(1, 2, 3번 열)이 비어있는지 확인
                        SimPieceData queensideRook = boardState[0, row];
                        if (queensideRook.type == pieceType.Rook && !queensideRook.hasMoved)
                        {
                            if (boardState[1, row].type == pieceType.none &&
                                boardState[2, row].type == pieceType.none &&
                                boardState[3, row].type == pieceType.none)
                            {
                                // 캐슬링 시 킹이 이동할 최종 목표 칸 (x = 2)
                                outMoves.Add(new Vector2Int(2, row));
                            }
                        }
                    }
                    break;
                }


        }
    
    }
    private bool IsValidIndex(int x, int y)
    {
        
        return x >= 0 && x < 8 && y >= 0 && y < 8;
    }


    // 3. [위치 보너스] 순수 데이터(SimPieceData)를 받도록 수정
    private int GetBlackPositionBonusDataOnly(SimPieceData piece)
    {
        int y = piece.pos.y;
        int x = piece.pos.x;

        // 1. 폰 (Pawn) 위치 보너스 (Y=7에서 시작해 Y=0으로 전진할수록 높은 점수)
        if (piece.type == pieceType.Pawn)
        {
            int[,] blackPawnTable = {
            {   0,   0,   0,   0,   0,   0,   0,   0 }, // y = 0 (상대 진영 끝/승진 칸) -> 가장 높은 가치
            { 200, 250, 300, 400, 400, 300, 250, 200 }, // y = 1
            { 100, 130, 160, 200, 200, 160, 130, 100 }, // y = 2
            {  50,  70, 100, 130, 130, 100,  70,  50 }, // y = 3
            {  20,  30,  50,  70,  70,  50,  30,  20 }, // y = 4
            {  10,  15,  25,  40,  40,  25,  15,  10 }, // y = 5
            {   5,   5,  10,  20,  20,  10,   5,   5 }, // y = 6 (시작 위치 근처)
            {   0,   0,   0,   0,   0,   0,   0,   0 }  // y = 7 (검정색 맨 처음 시작 줄)
        };
            return blackPawnTable[y, x];
        }

        // 2. 나이트 & 비숍 (Minor Pieces) 위치 보너스 (중앙 장악 지향)
        if (piece.type == pieceType.Knight || piece.type == pieceType.Bishop)
        {
            int[,] blackMinorTable = {
            { -10,  -5,  -5,  -5,  -5,  -5,  -5, -10 }, // y = 0
            {  -5,   0,   0,   0,   0,   0,   0,  -5 }, // y = 1
            {  -5,   0,  15,  20,  20,  15,   0,  -5 }, // y = 2
            {  -5,   5,  20,  25,  25,  20,   5,  -5 }, // y = 3
            {  -5,   5,  20,  25,  25,  20,   5,  -5 }, // y = 4
            {  -5,   0,  15,  20,  20,  15,   0,  -5 }, // y = 5
            {  -5,   0,   5,   5,   5,   5,   0,  -5 }, // y = 6
            { -10,  -5,  -5,  -5,  -5,  -5,  -5, -10 }  // y = 7
        };
            return blackMinorTable[y, x];
        }

        // 3. 룩 (Rook) 위치 보너스
        if (piece.type == pieceType.Rook)
        {
            int[,] blackRookTable = {
            {   0,   0,   0,   5,   5,   0,   0,   0 }, // y = 0
            {  -5,   0,   0,   0,   0,   0,   0,  -5 }, // y = 1
            {  -5,   0,   0,   0,   0,   0,   0,  -5 }, // y = 2
            {  -5,   0,   0,   0,   0,   0,   0,  -5 }, // y = 3
            {  -5,   0,   0,   0,   0,   0,   0,  -5 }, // y = 4
            {  -5,   0,   0,   0,   0,   0,   0,  -5 }, // y = 5
            {   5,  10,  10,  10,  10,  10,  10,   5 }, // y = 6
            {   0,   0,   0,   0,   0,   0,   0,   0 }  // y = 7
        };
            return blackRookTable[y, x];
        }

        // 4. 여왕 / 퀸 (Queen) 위치 보너스
        if (piece.type == pieceType.Queen)
        {
            int[,] blackQueenTable = {
            { -20, -10, -10,  -5,  -5, -10, -10, -20 }, // y = 0
            { -10,   0,   5,   0,   0,   0,   0, -10 }, // y = 1
            { -10,   5,   5,   5,   5,   5,   0, -10 }, // y = 2
            {   0,   0,   5,   5,   5,   5,   0,  -5 }, // y = 3
            {  -5,   0,   5,   5,   5,   5,   0,  -5 }, // y = 4
            { -10,   0,   5,   5,   5,   5,   0, -10 }, // y = 5
            { -10,   0,   0,   0,   0,   0,   0, -10 }, // y = 6
            { -20, -10, -10,  -5,  -5, -10, -10, -20 }  // y = 7
        };
            return blackQueenTable[y, x]; 
        }

        // 5. 킹 (King) 위치 보너스 (초중반에는 안전한 자기 진영 $Y=7$ 근처 선호)
        if (piece.type == pieceType.King)
        {
            int[,] blackKingTable = {
            { -30, -40, -40, -50, -50, -40, -40, -30 }, // y = 0 (적진 쪽으로 나가면 위험)
            { -30, -40, -40, -50, -50, -40, -40, -30 }, // y = 1
            { -30, -40, -40, -50, -50, -40, -40, -30 }, // y = 2
            { -20, -30, -30, -40, -40, -30, -30, -20 }, // y = 3
            { -10, -20, -20, -20, -20, -20, -20, -10 }, // y = 4
            {  20,  20,   0,   0,   0,   0,  20,  20 }, // y = 5
            {  20,  30,  10,   0,   0,  10,  30,  20 }, // y = 6
            {  20,  30,  10,   0,   0,  10,  30,  20 }  // y = 7 (시작 위치 백랭크 쪽 안전지대)
        };
            return blackKingTable[y, x];
        }

        return 0;
    }

    // 미니맥스 탐색 중 기물 이동 후 점수를 평가할 때 또는 이동 우선순위를 매길 때 적용
    // 4. [기물 안전도 평가] 순수 데이터 버전
    private int EvaluateCurrentPiecesSafetyDataOnly(SimPieceData[,] boardState, SimPieceData piece, Vector2Int currentPos)
    {
        int safetyPenalty = 0;
        PieceColor enemyColor = (piece.color == PieceColor.white) ? PieceColor.black : PieceColor.white;

        // 해당 칸이 적에게 공격받고 있는지 확인
        bool isAttacked = IsSquareAttackedDataOnly(boardState, currentPos, enemyColor);

        if (isAttacked)
        {
            // 아군이 이 칸을 지켜주고 있는지 확인
            bool isDefended = IsSquareAttackedDataOnly(boardState, currentPos, piece.color);

            int pieceCost = GetPieceValue(piece.type);

            if (!isDefended)
            {
                safetyPenalty -= pieceCost; // 방어가 없으면 기물 가치만큼 큰 페널티
            }
            else
            {
                safetyPenalty -= (pieceCost / 2); // 교환 국면이면 절반 페널티
            }
        }

        return safetyPenalty;
    }

    private void ExecuteMove(Vector2Int from, Vector2Int to)
    {
        gameM.MovePiece(from.x, from.y, to.x, to.y);

    }

   
}

