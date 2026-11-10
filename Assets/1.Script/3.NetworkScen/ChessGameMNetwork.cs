using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SingleChessGameM의 "멀티플레이 연결부" (partial 클래스로 따로 분리한 파일).
///
/// 네트워크 코드(NetworkGameM)가 내 체스판에게 "기물 놓아", "기물 옮겨", "게임 끝났어?" 같은 일을 시킬 때
/// 이 파일의 함수들이 기존 함수(SpawnPiece, MovePiece, DestroyPiece ...)를 불러서 처리합니다.
/// 싱글(1인 2역) 코드는 건드리지 않고, 멀티일 때만 이 파일의 분기가 작동합니다.
///
/// [멀티 판단] isNetworkScene(인스펙터 체크) 이거나 NetworkGameM이 있으면 멀티입니다.
/// [규칙] 싱글과 똑같이 "기물이 갈 수 있는 칸(GetPossibleMoves)"만 따르고, 왕이 잡히면 게임이 끝납니다.
/// </summary>
public partial class SingleChessGameM : IChessBoardAdapter
{
    // ───────────── 멀티플레이 상태 ─────────────

    [Header("멀티플레이")]
    [Tooltip("멀티 씬(NetworkGame)에서만 체크하세요. Start/Awake 때처럼 네트워크가 아직 준비되기 전에도 멀티로 취급합니다.")]
    [SerializeField] private bool isNetworkScene;

    private PieceColor _netLocalColor = PieceColor.white;   // 내 색 (NetworkGameM이 알려 줌)
    private bool _netSetupEnabled;                           // 배치 입력이 켜져 있나
    private bool _netPlayEnabled;                            // 기물 선택/이동 입력이 켜져 있나

    /// <summary>지금 멀티 판인가요? </summary>
    private bool IsNetworkMatch => isNetworkScene;

    private bool NetIsMyPiece(int x, int y)
    {
        if (x < 0 || x >= 8 || y < 0 || y >= 8) return false;
        ChessPieceM piece = pieceGrid[x, y];
        return piece != null && piece.isWhite == (_netLocalColor == PieceColor.white);
    }

    /// <summary>기물 버튼에서 고른 기물을 "내 색"으로 바꿔 줍니다. (두 사람이 같은 UI를 쓰므로)</summary>
    private PieceData NetAsLocalColor(PieceData data)
    {
        data.Color = _netLocalColor;
        return data;
    }

    // ───────────── 클릭 처리 (OnTileClicked 맨 위에서 호출) ─────────────

    /// <summary>
    /// 멀티 대국 중의 칸 클릭을 처리합니다.
    /// true = 멀티 규칙이 처리했으니 기존 로직은 건너뛰기, false = 멀티가 아니니 기존 로직 그대로.
    /// </summary>
    private bool NetHandleBoardClick(int x, int y)
    {
        if (!IsNetworkMatch) return false;

        // 1) 배치 단계: 기물 버튼을 골랐다면 놓기 요청, 아니면 내 기물을 눌러 치우기 요청
        if (_netSetupEnabled)
        {
            if (isSpawnMode && selectedPieceData != null)
            {
                if (!IsValidSpawnZone(x, y, _netLocalColor)) return true;

                NetRequestPlace(selectedPieceData.Value.Type, x, y);   // 실제 생성은 방장이 허락한 뒤에!
                selectedPieceData = null;
                ClearTileHighlights();
                return true;
            }

            if (NetIsMyPiece(x, y)) NetRequestRemove(x, y);
            return true;
        }

        // 2) 대국 단계: 내 차례일 때만 선택/이동 (이동은 요청만 보내고, 허락이 오면 모두의 판에서 움직임)
        if (_netPlayEnabled)
        {
            if (selectedPos == null)
            {
                if (NetIsMyPiece(x, y)) SelectPiece(x, y);
                return true;
            }

            var target = new Vector2Int(x, y);
            if (currentPossibleMoves.Contains(target))
            {
                Vector2Int from = selectedPos.Value;
                DeselectPiece();
                NetRequestMove(from.x, from.y, x, y);
            }
            else if (NetIsMyPiece(x, y)) SelectPiece(x, y);   // 다른 내 기물로 선택 교체
            else DeselectPiece();
            return true;
        }

        return true;   // 상대 차례, 공개 중, 게임 종료 등: 클릭 무시
    }

    private static void NetRequestPlace(pieceType type, int x, int y)
    {
        if (ScriptM.TryGet<NetworkChessPieceM>(out NetworkChessPieceM net)) net.RequestPlace(type, x, y);
    }

    private static void NetRequestRemove(int x, int y)
    {
        if (ScriptM.TryGet<NetworkChessPieceM>(out NetworkChessPieceM net)) net.RequestRemove(x, y);
    }

    private static void NetRequestPromotion(pieceType type)
    {
        if (ScriptM.TryGet<NetworkChessPieceM>(out NetworkChessPieceM net)) net.RequestPromotion(type);
    }

    private static void NetRequestMove(int fromX, int fromY, int toX, int toY)
    {
        if (ScriptM.TryGet<NetworkChessPieceM>(out NetworkChessPieceM net)) net.RPC_RequestMove(fromX, fromY, toX, toY);
    }

    // ───────────── 규칙 정보 ─────────────

    int IChessBoardAdapter.GetMaxPieceCount(pieceType type)
    {
        // 기물 버튼에 적힌 maxPiece 가 곧 "최대 몇 개"입니다. (RefreshButtonUI 와 같은 출처)
        foreach (PieceButtonUI button in pieceButtonUIs)
        {
            if (button.pieceData.Type == type) return button.pieceData.maxPiece;
        }
        return 0;
    }

    // ───────────── 배치 단계 ─────────────

    void IChessBoardAdapter.PlacePiece(pieceType type, PieceColor color, int x, int y)
    {
        SoundService.Current.playSFX("버튼");
        GameObject prefab = PieceDatabase != null ? PieceDatabase.GetPrefab(type, color) : null;
        if (prefab == null)
        {
            return;
        }

        SpawnPiece(prefab, x, y, color);

        // 남은 개수는 "내 기물"만 셉니다. (상대 기물은 게임 시작 때 공개되며 개수에 영향 없음)
        if (color == _netLocalColor)
        {
            AddSpawnedCount(type);
            RefreshButtonUI();
        }
    }

    /// <summary>배치 중에 기물을 치웁니다. 왕이면 왕 기억(whiteKing/blackKing)도 같이 비웁니다.</summary>
    private void NetDestroyDuringSetup(int x, int y)
    {
        ChessPieceM piece = pieceGrid[x, y];
        if (piece == null) return;

        if (piece.data.Type == pieceType.King)
        {
            if (piece.isWhite) whiteKing = null;
            else blackKing = null;
        }

        DestroyPiece(x, y, false);    // 개수 복구와 제거는 기존 함수가 처리
    }

    void IChessBoardAdapter.RemovePiece(int x, int y)
    {
        SoundService.Current.playSFX("버튼");
        NetDestroyDuringSetup(x, y);
        RefreshButtonUI();
    }

    void IChessBoardAdapter.ClearSide(PieceColor color)
    {
        SoundService.Current.playSFX("되감기");
        bool targetIsWhite = color == PieceColor.white;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceM piece = pieceGrid[x, y];
                if (piece != null && piece.isWhite == targetIsWhite) NetDestroyDuringSetup(x, y);
            }
        }

        RefreshButtonUI();
    }

    void IChessBoardAdapter.ClearBoard()
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (pieceGrid[x, y] != null) NetDestroyDuringSetup(x, y);
            }
        }

        activePieces.Clear();
        spawnedPieceCounts.Clear();
        whiteKing = null;
        blackKing = null;
        MoveHistory.Clear();
        positionHistory.Clear();

        selectedPieceData = null;
        isSpawnMode = false;
        isMovedone = false;
        isWhiteTurn = true;
        currentTurn = PieceColor.white;

        DeselectPiece();
        ClearTileHighlights();
        RefreshButtonUI();
    }

    void IChessBoardAdapter.SetSetupInputEnabled(bool enabled, PieceColor localColor)
    {
        _netLocalColor = localColor;
        _netSetupEnabled = enabled;

        // 입력이 꺼지면 들고 있던 기물 선택과 초록색 칸 표시를 걷어 냅니다.
        if (!enabled && selectedPieceData != null)
        {
            selectedPieceData = null;
            ClearTileHighlights();
        }
    }

    // ───────────── 대국 단계 ─────────────

    void IChessBoardAdapter.SetPlayInputEnabled(bool enabled, PieceColor localColor)
    {
        _netLocalColor = localColor;
        _netPlayEnabled = enabled;

        if (!enabled && selectedPos != null) DeselectPiece();   // 내 차례가 아니면 선택 해제
    }

    void IChessBoardAdapter.SetSideToMove(PieceColor color)
    {
        // 대국이 시작됐으니 배치 모드를 끕니다. (DestroyPiece가 왕이 잡혔는지 알아채려면 꺼져 있어야 해요)
        isSpawnMode = false;
        selectedPieceData = null;

        currentTurn = color;
        isWhiteTurn = color == PieceColor.white;
        isMovedone = false;
    }

    bool IChessBoardAdapter.IsLegalMove(PieceColor mover, int fromX, int fromY, int toX, int toY)
    {
        
        if (fromX < 0 || fromX >= 8 || fromY < 0 || fromY >= 8) return false;
        if (toX < 0 || toX >= 8 || toY < 0 || toY >= 8) return false;

        ChessPieceM piece = pieceGrid[fromX, fromY];
        if (piece == null || piece.isWhite != (mover == PieceColor.white)) return false;

        // 싱글과 똑같이: 그 기물이 갈 수 있는 칸 목록에 도착 칸이 있으면 합법
        var moves = new List<Vector2Int>(32);
        piece.GetPossibleMoves(pieceGrid, moves);
        return moves.Contains(new Vector2Int(toX, toY));
    }

    bool IChessBoardAdapter.ApplyMove(int fromX, int fromY, int toX, int toY)
    {
        bool captured = pieceGrid[toX, toY] != null;
        MovePiece(fromX, fromY, toX, toY);   // 폰이 끝줄에 닿아도 싱글용 RequestPromotion은 멀티에선 건너뜀 (아래 승급 흐름 사용)
        return captured;
    }

    // ───────────── 프로모션 (폰이 끝줄에 닿았을 때) ─────────────

    bool IChessBoardAdapter.TryFindPromotion(PieceColor mover, out int x, out int y)
    {
        bool isWhite = mover == PieceColor.white;
        int lastRank = isWhite ? 7 : 0;   // 싱글(MovePiece)과 같은 기준

        for (x = 0; x < 8; x++)
        {
            ChessPieceM piece = pieceGrid[x, lastRank];
            if (piece != null && piece is Pawn && piece.isWhite == isWhite)
            {
                y = lastRank;
                return true;
            }
        }

        x = -1;
        y = -1;
        return false;
    }

    void IChessBoardAdapter.ApplyPromotion(int x, int y, pieceType newType, PieceColor color)
    {
        SoundService.Current.playSFX("버튼");
        ChessPieceM pawn = pieceGrid[x, y];
        if (pawn == null) return;

        GameObject prefab = PieceDatabase != null ? PieceDatabase.GetPrefab(newType, color) : null;
        if (prefab == null)
        {
            
            return;
        }

        int oldCost = pawn.data.cost;

        DestroyPiece(x, y);                                        // 폰 제거
        ChessPieceM promoted = SpawnPiece(prefab, x, y, color);    // 새 기물 생성 (색을 꼭 넘겨 줍니다)

        var move = new MoveData(new Vector2Int(x, y), new Vector2Int(x, y), pawn, oldCost, promoted != null ? promoted.data.cost : oldCost, null);
        move.IsPromotion = true;
        MoveHistory.Add(move);
    }

    void IChessBoardAdapter.SetPromotionChoiceVisible(bool visible, PieceColor localColor)
    {
        if (PromotionPanel_W != null) PromotionPanel_W.SetActive(visible && localColor == PieceColor.white);
        if (PromotionPanel_B != null) PromotionPanel_B.SetActive(visible && localColor == PieceColor.black);
    }

    ChessOutcome IChessBoardAdapter.EvaluateOutcome(PieceColor sideToMove)
    {
        SoundService.Current.playSFX("체크");
        // 싱글 규칙 그대로: 왕이 잡히면 끝. 차례가 온 쪽의 왕이 판에 없으면 방금 둔 쪽의 승리입니다.
        bool sideIsWhite = sideToMove == PieceColor.white;

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                ChessPieceM piece = pieceGrid[x, y];
                if (piece != null && piece.isWhite == sideIsWhite && piece.data.Type == pieceType.King)
                    return ChessOutcome.Ongoing;
            }
        }

        return ChessOutcome.Checkmate;
    }
}