using Cysharp.Threading.Tasks;
using Google.GData.Extensions;
using System.Collections.Generic;
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

    public SimPieceData(pieceType type, PieceColor color, Vector2Int pos, bool hasMoved)
    {
        this.type = type;
        this.color = color;
        this.pos = pos;
        this.hasMoved = hasMoved;
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

    private void Awake()
    {
        ScriptM.Register<Characters>(this,UseSpace.Local);

        if (ScriptM.TryGet<CheseGameM>(out CheseGameM _gameM))
        {
            gameM = _gameM;
        }
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
        await UniTask.Delay(System.TimeSpan.FromSeconds(2f));

        MoveBestMove bestMove = FindBestMove(searchDepth);

        // 연산 후 프레임 안정화를 위해 잠시 대기 (선택사항)
        await UniTask.Yield(PlayerLoopTiming.Update);

        if (bestMove.isValid)
        {
            ExecuteMove(bestMove.from, bestMove.to);

            // 💡 AI 행동 완료 후 안전하게 턴 종료 호출
            if (gameM != null)
            {
                gameM.TurnEnd();
            }
        }
        else
        {
            Debug.Log("움직일 수 있는 수가 없습니다. (체크메이트 혹은 스테일메이트)");
        }
    }

    // 최적의 수를 찾는 탐색 함수 (미니맥스 + 알파-베타 가지치기)
    private MoveBestMove FindBestMove(int depth)
    {
        int bestScore = -99999; // 내가 얻을 수 있는 가장 높은 점수 (초기엔 아주 낮게 설정)
        MoveBestMove selectedMove = new MoveBestMove();

        // [비유] 내 차례: 판 위에 있는 내 모든 말들을 살펴봅니다.
        List<ChessPieceM> myPieces = GetAllMyPieces(); // 내 팀 기물들 모음

        foreach (var piece in myPieces)
        {
            List<Vector2Int> possibleMoves = new List<Vector2Int>();
            piece.GetPossibleMoves(gameM.pieceGrid, possibleMoves); // 이 말이 갈 수 있는 칸들

            foreach (var targetPos in possibleMoves)
            {
                Vector2Int startPos = piece.CurrentPos;

                // --- [시뮬레이션 시작] ---
                // '만약에 내가 이 말을 이 자리로 옮기면 어떻게 될까?' 가상으로 움직여봅니다.
                SimulateMove(startPos, targetPos);

                // 상대방 입장에서 가장 유리한 점수를 예측해봅니다 (재귀 호출)
                // 알파(-100000), 베타(100000)는 가지치기(불필요한 생각 버리기)를 위한 최적화 도구입니다.
                int score = Minimax(depth - 1, false, -100000, 100000);

                // --- [시뮬레이션 취소] ---
                // 가상 실험이 끝났으니 판을 원래대로 되돌려놓습니다.
                UndoMove(startPos, targetPos);

                // 지금 가본 길이 기존에 찾았던 최고 점수보다 더 좋다면? 이 수를 찜합니다!
                if (score > bestScore)
                {
                    bestScore = score;
                    selectedMove.from = startPos;
                    selectedMove.to = targetPos;
                    selectedMove.isValid = true;
                }
            }
        }
        return selectedMove;
    }

    // 미니맥스 알고리즘 (나와 상대방의 수를 번갈아가며 시뮬레이션하는 두뇌)
    private int Minimax(int depth, bool isMaximizing, int alpha, int beta)
    {
        // 목표 깊이(몇 수 앞)에 도달했거나 게임이 끝났다면 현재 판의 점수를 계산해서 반환합니다.
        if (depth == 0)
        {
            return EvaluateBoard(); // 판을 평가하는 점수 판독기
        }
        if (isMaximizing) // 내 차례 (점수를 최대한 높이고 싶어 함)
        {
            int maxEval = -99999;
            List<ChessPieceM> myPieces = GetAllMyPieces();

            foreach (var piece in myPieces)
            {
                List<Vector2Int> moves = new List<Vector2Int>();
                piece.GetPossibleMoves(gameM.pieceGrid, moves);

                foreach (var target in moves)
                {
                    Vector2Int start = piece.CurrentPos;
                    SimulateMove(start, target);
                    int eval = Minimax(depth - 1, false, alpha, beta);
                    UndoMove(start, target);

                    maxEval = Mathf.Max(maxEval, eval);
                    alpha = Mathf.Max(alpha, eval);

                    if (beta <= alpha) break; // [최적화] 상대방이 절대 허락하지 않을 쓸데없는 수는 더 이상 생각 안 함! (알파-베타 가지치기)
                }
                if (beta <= alpha) break;
            }
            return maxEval;
        }
        else // 상대방 차례 (AI 입장에서 내 점수를 최대한 깎아내리려고 함)
        {
            
            int minEval = 99999;
            List<ChessPieceM> enemyPieces = GetAllEnemyPieces();

            foreach (var piece in enemyPieces)
            {
               
                List<Vector2Int> moves = new List<Vector2Int>();
                piece.GetPossibleMoves(gameM.pieceGrid, moves);

                foreach (var target in moves)
                {
                    
                    Vector2Int start = piece.CurrentPos;
                    SimulateMove(start, target);
                    int eval = Minimax(depth - 1, true, alpha, beta);
                    UndoMove(start, target);

                    minEval = Mathf.Min(minEval, eval);
                    beta = Mathf.Min(beta, eval);

                    if (beta <= alpha) break; // ✂️ [최적화] 가지치기
                }
                if (beta <= alpha) break;
            }
            return minEval;
        }
    }

    // ⚖️ 판의 유불리를 점수로 환산해주는 평가 함수 (기물 가치 점수)
    private int EvaluateBoard()
    {
        int score = 0;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceM piece = gameM.pieceGrid[x, y];
                if (piece == null) continue;

                int pieceValue = GetPieceValue(piece.data.Type);
                int positionalBonus = GetBlackPositionBonus(piece);
                int safetyPenalty = EvaluateCurrentPieceSafety(piece, new Vector2Int(x, y));

                int totalscore = pieceValue + positionalBonus + safetyPenalty; 

                if (piece.data.Color == PieceColor.black)
                {
                    score += totalscore;
                }
                else
                {
                    score -= totalscore;
                }
            }
        }

        return score;
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

    private int GetBlackPositionBonus(ChessPieceM piece)
    {
        int y = piece.CurrentPos.y;
        int x = piece.CurrentPos.x;

        // 1. 폰: 아래로 내려갈수록(y=0에 가까울수록) 프로모션에 임박하므로 점수가 급격히 상승
        if (piece.data.Type == pieceType.Pawn)
        {
            int[,] blackPawnTable = {
            {   0,   0,   0,   0,   0,   0,   0,   0 }, // y = 0 (프로모션 목적지! 도달 시 최대 가점)
            { 200, 250, 300, 400, 400, 300, 250, 200 }, // y = 1 (프로모션 직전)
            { 100, 130, 160, 200, 200, 160, 130, 100 }, // y = 2
            {  50,  70, 100, 130, 130, 100,  70,  50 }, // y = 3 (중앙 진입)
            {  20,  30,  50,  70,  70,  50,  30,  20 }, // y = 4
            {  10,  15,  25,  40,  40,  25,  15,  10 }, // y = 5
            {   5,   5,  10,  20,  20,  10,   5,   5 }, // y = 6 (폰 시작 위치)
            {   0,   0,   0,   0,   0,   0,   0,   0 }  // y = 7 (맨 윗줄 백업 진영)
        };
            return blackPawnTable[y, x];
        }

        // 2. 나이트 / 비숍: 중앙(3, 4열 및 2~5행)으로 활발하게 전진하도록 유도
        if (piece.data.Type == pieceType.Knight || piece.data.Type == pieceType.Bishop)
        {
            int[,] blackMinorTable = {
            {-10,-5,-5,-5,-5,-5,-5,-10},
            { -5, 0, 0, 0, 0, 0, 0, -5},
            { -5, 0,15,20,20,15, 0, -5},
            { -5, 5,20,25,25,20, 5, -5},
            { -5, 5,20,25,25,20, 5, -5},
            { -5, 0,15,20,20,15, 0, -5},
            { -5, 0, 5, 5, 5, 5, 0, -5},
            {-10,-5,-5,-5,-5,-5,-5,-10}
        };
            return blackMinorTable[y, x];
        }

        return 0;
    }

    public bool IsSquareAttacked(Vector2Int targetPos, PieceColor attackerColor)
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceM piece = gameM.pieceGrid[x, y];
                // 살아있는 상대 팀 기물인 경우
                if (piece != null && piece.data.Color == attackerColor)
                {
                    List<Vector2Int> enemyMoves = new List<Vector2Int>();
                    piece.GetPossibleMoves(gameM.pieceGrid, enemyMoves);

                    // 상대 기물이 해당 칸으로 갈 수 있다면 공격받고 있는 것임
                    if (enemyMoves.Contains(targetPos))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    // 미니맥스 탐색 중 기물 이동 후 점수를 평가할 때 또는 이동 우선순위를 매길 때 적용
    private int EvaluateCurrentPieceSafety(ChessPieceM piece, Vector2Int currentPos)
    {
        int safetyPenalty = 0;
        PieceColor enemyColor = (piece.data.Color == PieceColor.white) ? PieceColor.black : PieceColor.white;

        // 현재 내 기물이 있는 칸이 상대방에게 공격받고 있는지 확인
        bool isAttacked = IsSquareAttacked(currentPos, enemyColor);

        if (isAttacked)
        {
            // 내 다른 아군이 이 칸을 지켜주고 있는지 확인
            bool isDefended = IsSquareAttacked(currentPos, piece.data.Color);

            // 공격은 받는데 방어는 없다면 (공짜로 죽을 위기에 처함)
            if (!isDefended)
            {
                safetyPenalty -= piece.data.cost; // 기물 가치만큼 페널티
            }
            else
            {
                safetyPenalty -= (piece.data.cost / 2); // 교환 국면이면 절반 페널티
            }
        }

        return safetyPenalty;
    }

    // 내 팀의 모든 살아있는 기물들을 찾아 리스트로 반환하는 함수 override
    protected List<ChessPieceM> GetAllMyPieces()
    {
        if (gameM == null) return new List<ChessPieceM>();

        List<ChessPieceM> myPieces = new List<ChessPieceM>();

        // AI가 흰색이면 기물도 흰색(isWhite == true), AI가 검정색이면 기물도 검정색(isWhite == false)이어야 내 기물!
        bool isMeWhite = (myColor == PieceColor.white);

        foreach (var piece in gameM.activePieces)
        {
            if (piece != null && piece.isWhite == isMeWhite)
            {
                myPieces.Add(piece);
            }
        }
        return myPieces;
    }

    // 상대 팀의 모든 살아있는 기물들을 반환하는 함수 override
    protected List<ChessPieceM> GetAllEnemyPieces()
    {
        if (gameM == null) return new List<ChessPieceM>();

        List<ChessPieceM> enemyPieces = new List<ChessPieceM>();

        bool isMeWhite = (myColor == PieceColor.white);

        foreach (var piece in gameM.activePieces)
        {
            // 내 색상과 반대인 것들이 적 기물!
            if (piece != null && piece.isWhite != isMeWhite)
            {
                enemyPieces.Add(piece);
            }
        }
        return enemyPieces;
    }

    private struct SimpleSimData
    {
        public Vector2Int from;
        public Vector2Int to;
        public ChessPieceM movedPiece;
        public ChessPieceM capturedPiece;

        // 💡 승진 백업용 데이터 추가
        public bool wasPromotion;
        public pieceType originalType;
    }
    private Stack<SimpleSimData> simStack = new Stack<SimpleSimData>();

    private void SimulateMove(Vector2Int from, Vector2Int to)
    {
        ChessPieceM movingPiece = gameM.pieceGrid[from.x, from.y];
        ChessPieceM targetPiece = gameM.pieceGrid[to.x, to.y];

        bool isPromotion = false;
        pieceType originalType = pieceType.Pawn; // Enum 대소문자 프로젝트 기준 통일

        // 1. 폰 프로모션 체크 (가상 승진)
        if (movingPiece != null && movingPiece.data.Type == pieceType.Pawn)
        {
            originalType = pieceType.Pawn;

            // 상대방/나의 끝 행에 도달했을 때
            if (to.y == 0 || to.y == 7)
            {
                isPromotion = true;
                // 시뮬레이션 동안만 퀸으로 가상 변경 (평가 점수 산정용)
                movingPiece.data.Type = pieceType.Queen;
            }
        }

        // 2. 복원용 스택(simStack)에 백업 정보 푸시
        simStack.Push(new SimpleSimData
        {
            from = from,
            to = to,
            movedPiece = movingPiece,
            capturedPiece = targetPiece,
            wasPromotion = isPromotion,
            originalType = originalType
        });

        // 3. 논리적 보드(pieceGrid) 데이터 업데이트
        gameM.pieceGrid[from.x, from.y] = null;
        gameM.pieceGrid[to.x, to.y] = movingPiece;

        if (movingPiece != null)
        {
            movingPiece.CurrentPos = to;
        }
    }
    private void UndoMove(Vector2Int from, Vector2Int to)
    {
        if (simStack.Count == 0) return;

        SimpleSimData last = simStack.Pop();

        // 1. 가상 승진했던 기물이면 다시 원래 타입(폰)으로 복구
        if (last.wasPromotion && last.movedPiece != null)
        {
            last.movedPiece.data.Type = last.originalType;
        }

        // 2. 논리 보드(pieceGrid) 좌표 원상복구
        gameM.pieceGrid[last.from.x, last.from.y] = last.movedPiece;
        gameM.pieceGrid[last.to.x, last.to.y] = last.capturedPiece;

        // 3. 기물 내부 좌표 복구
        if (last.movedPiece != null)
        {
            last.movedPiece.CurrentPos = last.from;
        }
    }
    private void ExecuteMove(Vector2Int from, Vector2Int to)
    { 
        gameM.MovePiece(from.x,from.y,to.x,to.y);
        
    }

    protected PieceColor myColor = PieceColor.black;
}
