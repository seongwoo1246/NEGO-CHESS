using UnityEngine;

/// <summary>
/// 무르기 협상이 판에게 요구하는 기능 (IUndoBoard) 구현. 멀티(NetworkUndoM)에서 사용합니다.
/// 기존 함수(ExecuteUndoLastMove, DestroyPiece ...)를 그대로 불러 쓰므로 새 규칙은 없습니다.
/// ※ SingleChessGameM.ExecuteUndoLastMove 가 CheseGameM의 최신 버전(승급/캐슬링/앙파상/상태 복구 포함)과 같아야 합니다.
/// </summary>
public partial class SingleChessGameM : IUndoBoard
{
    int IUndoBoard.HistoryCount => MoveHistory.Count;

    MoveData IUndoBoard.HistoryAt(int index) => MoveHistory[index];

    ChessPieceM IUndoBoard.PieceAt(int x, int y)
    {
        if (x < 0 || x > 7 || y < 0 || y > 7) return null;
        return pieceGrid[x, y];
    }

    void IUndoBoard.UndoLastEntry()
    {
        int last = MoveHistory.Count - 1;
        if (last < 0) return;
        _undoInstant = true;
        try { ExecuteUndoLastMove(MoveHistory[last], last); }
        finally { _undoInstant = false; }
    }

    void IUndoBoard.RemovePieceByDeal(int x, int y)
    {
        DestroyPiece(x, y);
    }

    void IUndoBoard.AfterDealApplied()
    {
      
        SnapAllPiecesToGrid();
        ReconcilePieces();
        DeselectPiece();
        ClearTileHighlights();
        isMovedone = false;
        RefreshButtonUI();
    }

    float IUndoBoard.NegoBonus(PieceColor color)
    {
        return ScriptM.TryGet<NetworkGameM>(out NetworkGameM game) && game != null ? game.GetNegoBonus(color) : 0f;
    }

    private void ReconcilePieces()
    {
        var inGrid = new System.Collections.Generic.HashSet<ChessPieceM>();
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                if (pieceGrid[x, y] != null) inGrid.Add(pieceGrid[x, y]);

        foreach (ChessPieceM p in FindObjectsByType<ChessPieceM>(FindObjectsSortMode.None))
        {
            if (p == null || !p.gameObject.activeInHierarchy || inGrid.Contains(p)) continue;

            Debug.LogWarning($"[무르기] 고아 기물 정리: {p.data.Type} {p.data.Color} pos={p.CurrentPos}");
            activePieces.Remove(p);
            spawner.Despawn(p.gameObject);
        }
    }
}