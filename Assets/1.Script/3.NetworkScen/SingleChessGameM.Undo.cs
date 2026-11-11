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
        ExecuteUndoLastMove(MoveHistory[last], last);   // 안에서 MoveHistory.RemoveAt 합니다
    }

    void IUndoBoard.RemovePieceByDeal(int x, int y)
    {
        DestroyPiece(x, y);
    }

    void IUndoBoard.AfterDealApplied()
    {
        DeselectPiece();
        ClearTileHighlights();
        isMovedone = false;
        RefreshButtonUI();
    }

    float IUndoBoard.NegoBonus(PieceColor color)
    {
        return ScriptM.TryGet<NetworkGameM>(out NetworkGameM game) && game != null ? game.GetNegoBonus(color) : 0f;
    }
}