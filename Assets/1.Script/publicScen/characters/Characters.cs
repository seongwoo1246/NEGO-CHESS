using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Threading.Tasks;
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

// 가상 시뮬레이션용 기물 데이터 구조체 (다른 스크립트가 참조할 수 있어 그대로 유지)
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


/// <summary>
/// 적 AI 캐릭터. 실제 두뇌는 ChessSearchEngine(순수 C# 탐색 엔진)이 맡고,
/// 이 클래스는 "현재 판을 엔진에 옮겨 담기 → 백그라운드에서 생각 → 결과를 실제 판에 두기"만 합니다.
/// </summary>
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

    [Header("AI 생각 깊이 (숫자가 높을수록 똑똑하지만 오래 걸립니다. 0이면 4)")]
    public int searchDepth = 6;

    [Header("한 수를 생각하는 최대 시간(초). 깊이에 도달하기 전에 시간이 다 되면 그때까지의 최선수를 둡니다.")]
    public float maxThinkSeconds = 4.5f;

    private CheseGameM gameM;

    private readonly ChessSearchEngine engine = new ChessSearchEngine();
    private bool isThinking;

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
    public virtual void SetupBoard(CheseGameM gameMa)
    {
        gameM = gameMa;

        if (defaultPieceLayout == null || defaultPieceLayout.Count == 0)
        {
            
            return;
        }

        foreach (var spawnInfo in defaultPieceLayout)
        {
            // 1. 피스 타입과 컬러에 맞는 프리팹 가져오기
            GameObject prefabToSpawn = pieceDatabase.GetPrefab(spawnInfo.pieceType, spawnInfo.pieceColor);

            if (prefabToSpawn != null)
            {
                gameM.SpawnPiece(prefabToSpawn, spawnInfo.position.x, spawnInfo.position.y, PieceColor.black);
            }
          
        }

        
    }

    // AI 이동 결정 함수 (가상 함수로 만들어 캐릭터마다 다르게 오버라이드 가능)
    public virtual async UniTask OnAITurnAsync()
    {
       
        if (isThinking) return; // 같은 턴에 두 번 불려도 한 번만 생각
        if (gameM == null && !ScriptM.TryGet<CheseGameM>(out gameM)) return;

        isThinking = true;
        try
        {
            int depth = searchDepth > 0 ? searchDepth : 4;
            int timeMs = Mathf.Max(200, (int)(maxThinkSeconds * 1000f));

            // 1) 메인 스레드에서 실제 판을 엔진에 복사 (유니티 오브젝트는 여기서만 만진다)
            LoadBoardIntoEngine();

            // 2) 백그라운드에서 생각
            ChessSearchEngine.Result res = default;
            var eng = engine;
            await Task.Run(() => { res = eng.Think(depth, timeMs); });

            // 3) 생각하는 동안 오브젝트가 사라졌다면 중단
            if (this == null || gameM == null) return;

            if (res.found)
            {
                ChessPieceM mover = gameM.pieceGrid[res.fromX, res.fromY];
                if (mover == null || mover.data.Color != myColor)
                {
                    return;
                }
                ExecuteMove(new Vector2Int(res.fromX, res.fromY), new Vector2Int(res.toX, res.toY));
                gameM.TurnEnd();
            }
            else
            {
                // 둘 수 있는 수가 하나도 없음 (왕이 사라진 경우는 이미 게임이 끝난 상태)
                gameM.ShowGameResultAsync(GameResultType.Draw, 0, 0).Forget();
            }
        }
        finally
        {
            isThinking = false;
        }
    }

    // ───────────── 실제 판 → 엔진 ─────────────

    protected PieceColor myColor = PieceColor.black;

    private void LoadBoardIntoEngine()
    {
        engine.Clear();

        int width = gameM.pieceGrid.GetLength(0);
        int height = gameM.pieceGrid.GetLength(1);
        int epX = -1, epY = -1;

        for (int x = 0; x < width && x < 8; x++)
        {
            for (int y = 0; y < height && y < 8; y++)
            {
                
                ChessPieceM piece = gameM.pieceGrid[x, y];
                if (piece == null) continue;

                int type = ToEngineType(piece.data.Type);
                if (type == 0) continue;

                int color = piece.data.Color == PieceColor.white ? 0 : 1;

                bool hasMoved = false;
                if (piece is King king) hasMoved = king.hasMoved;
                else if (piece is Rook rook) hasMoved = rook.hasMoved;
                else if (piece is Pawn pawn)
                {
                    hasMoved = !pawn.isFirstMove;
                    if (pawn.movedTwoSquaresLastTurn)                          // ← 추가: 방금 두 칸 전진한 폰
                    {
                        int dir = piece.data.Color == PieceColor.white ? 1 : -1;
                        epX = x; epY = y - dir;                                // 그 폰이 지나간 칸
                    }
                }
            }
        }

        int me = myColor == PieceColor.white ? 0 : 1;
        int opp = me ^ 1;
        if (epX >= 0) engine.SetEnPassant(epX, epY);
        engine.SetSideToMove(me);

        // 협상으로 금지되는 규칙은 AI에게만 적용됩니다. (플레이어는 항상 쓸 수 있다고 보고 대비)
        engine.SetRules(me, gameM.canUseCastling, !Pawn.EnPassantBanned, gameM.canUsePromotion);
        engine.SetRules(opp, true, true, true);
    }

    private static int ToEngineType(pieceType t)
    {
        switch (t)
        {
            case pieceType.Pawn: return ChessSearchEngine.P;
            case pieceType.Knight: return ChessSearchEngine.N;
            case pieceType.Bishop: return ChessSearchEngine.B;
            case pieceType.Rook: return ChessSearchEngine.R;
            case pieceType.Queen: return ChessSearchEngine.Q;
            case pieceType.King: return ChessSearchEngine.K;
            default: return 0;
        }
    }

    private void ExecuteMove(Vector2Int from, Vector2Int to)
    {
        gameM.MovePiece(from.x, from.y, to.x, to.y);
    }
}