using System.Collections.Generic;
using UnityEngine;

public class Pawn : ChessPieceM, IPoolReset
{
    [Header("폰 전용 상태")]
    public bool isFirstMove = true;              // 첫 이동 여부 (2칸 전진 가능 여부)
    public bool movedTwoSquaresLastTurn = false; // 직전 수에서 2칸 전진했는지 (앙파상 대상)

    /// <summary>협상으로 앙파상이 금지되면 CheseGameM이 true로 바꿉니다. (흑에게만 적용, 백은 항상 가능)</summary>
    public static bool EnPassantBanned;

    public void ResetForPool() { isFirstMove = true; movedTwoSquaresLastTurn = false; }

    private void Reset()
    {
        data = new PieceData(pieceType.Pawn, PieceColor.white, 1, 8, "pawn");
        data_B = new PieceData(pieceType.Pawn, PieceColor.black, 1, 8, "pawn");
    }

    // 이동 후보만 계산합니다. (프로모션 같은 부작용은 여기서 하지 않습니다)
    public override void GetPossibleMoves(ChessPieceM[,] board, List<Vector2Int> possibleMoves)
    {
        int direction = isWhite ? 1 : -1;
        int forwardY = CurrentPos.y + direction;

        // 1. 앞으로 1칸, 첫 이동이면 2칸
        if (IsValidIndex(CurrentPos.x, forwardY) && board[CurrentPos.x, forwardY] == null)
        {
            possibleMoves.Add(new Vector2Int(CurrentPos.x, forwardY));

            int doubleForwardY = CurrentPos.y + direction * 2;
            if (isFirstMove && IsValidIndex(CurrentPos.x, doubleForwardY) && board[CurrentPos.x, doubleForwardY] == null)
                possibleMoves.Add(new Vector2Int(CurrentPos.x, doubleForwardY));
        }

        bool enPassantAllowed = isWhite || !EnPassantBanned;

        // 2. 대각선: 적 기물 잡기 / 앙파상
        for (int dx = -1; dx <= 1; dx += 2)
        {
            int targetX = CurrentPos.x + dx;
            int targetY = CurrentPos.y + direction;
            if (!IsValidIndex(targetX, targetY)) continue;

            ChessPieceM target = board[targetX, targetY];
            if (target != null)
            {
                if (!IsSameTeam(target)) possibleMoves.Add(new Vector2Int(targetX, targetY));
                continue;
            }

            // 앙파상: 도착 칸이 비어 있고, 옆 칸에 "방금 두 칸 전진한" 적 폰이 있을 때
            if (!enPassantAllowed) continue;
            ChessPieceM side = board[targetX, CurrentPos.y];
            if (side is Pawn enemyPawn && !IsSameTeam(side) && enemyPawn.movedTwoSquaresLastTurn)
                possibleMoves.Add(new Vector2Int(targetX, targetY));
        }
    }

    public bool CanPromote(int currentY) => (isWhite && currentY == 7) || (!isWhite && currentY == 0);
}