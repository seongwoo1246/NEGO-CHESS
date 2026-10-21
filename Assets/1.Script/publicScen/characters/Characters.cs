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
        ScriptM.Register<Characters>(this, UseSpace.Local);

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
    public virtual void SetupBoard(CheseGameM gameM) 
    {
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
                gameM.SpawnPiece(prefabToSpawn, spawnInfo.position.x, spawnInfo.position.y);
            }
            else
            {
                Debug.LogError($"해당 타입({spawnInfo.pieceType})과 색상({spawnInfo.pieceColor})의 프리팹을 찾을 수 없습니다.");
            }
        }

        Debug.Log("AI 기물 동적 배치 완료!");
    }

    // AI 이동 결정 함수 (가상 함수로 만들어 캐릭터마다 다르게 오버라이드 가능)
    public virtual void OnAITurn()
    {
        Debug.Log(" 그랜드마스터 AI가 최적의 수를 계산하기 시작합니다...");

        // 1. 가장 좋은 이동 경로를 담을 변수 초기화
        MoveBestMove bestMove = FindBestMove(searchDepth);

        // 2. 계산된 가장 좋은 수가 있다면 실제로 체스판에서 기물을 움직임
        if (bestMove.isValid)
        {
            ExecuteMove(bestMove.from, bestMove.to);
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

    // 🧠 미니맥스 알고리즘 (나와 상대방의 수를 번갈아가며 시뮬레이션하는 두뇌)
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
        // 체스 말마다 가치를 매겨둡니다 (폰=1점, 나이트/비숍=3점, 룩=5점, 퀸=9점, 킹=1000점)
        foreach (var piece in gameM.activePieces)
        {
            int pieceValue = GetPieceValue(piece.data.Type);

            // 내 기물이면 점수를 더하고, 적 기물이면 점수를 뺍니다.
            if (piece.data.Color == myColor)
            {
                score += pieceValue;
            }
            else
            {
                score -= pieceValue;
            }
        }
        return score;
    }

    // 기물 종류별 점수표
    private int GetPieceValue(pieceType type)
    {
        switch (type)
        {
            case pieceType.Pawn: return 10;
            case pieceType.Knight: return 30;
            case pieceType.Bishop: return 30;
            case pieceType.Rook: return 50;
            case pieceType.Queen: return 90;
            case pieceType.King: return 10000;
            default: return 0;
        }
    }



    // [가상 함수 껍데기들 - 본인의 게임 매니저 시스템에 맞춰 연결해 주세요]
    private List<ChessPieceM> GetAllMyPieces() { return null; }
    private List<ChessPieceM> GetAllEnemyPieces() { return null; }
    private void SimulateMove(Vector2Int from, Vector2Int to) { }
    private void UndoMove(Vector2Int from, Vector2Int to) { }
    private void ExecuteMove(Vector2Int from, Vector2Int to) { }

    protected PieceColor myColor;
}
