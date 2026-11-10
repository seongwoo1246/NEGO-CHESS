using System.Collections.Generic;
using UnityEngine;

public class King : ChessPieceM, IPoolReset
{
    public bool hasMoved = false; // 캐슬링 조건용 (한 번이라도 움직였는지)

    /// <summary>협상으로 캐슬링이 금지되면 CheseGameM이 true로 바꿉니다. (싱글/멀티는 항상 false)</summary>
    public static bool CastlingBanned;

    public void ResetForPool() { hasMoved = false; }

    private void Reset()
    {
        data = new PieceData(pieceType.King, PieceColor.white, 0, 1, "King");
        data_B = new PieceData(pieceType.King, PieceColor.black, 0, 1, "King");
    }

    public override void GetPossibleMoves(ChessPieceM[,] board, List<Vector2Int> possibleMoves)
    {
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),  new Vector2Int(0, -1),
            new Vector2Int(-1, 0), new Vector2Int(1, 0),
            new Vector2Int(1, 1),  new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(-1, -1)
        };

        foreach (Vector2Int dir in directions)
        {
            int targetX = CurrentPos.x + dir.x;
            int targetY = CurrentPos.y + dir.y;

            if (IsValidIndex(targetX, targetY))
            {
                if (board[targetX, targetY] == null || !IsSameTeam(board[targetX, targetY]))
                    possibleMoves.Add(new Vector2Int(targetX, targetY));
            }
        }

        if (hasMoved || CastlingBanned) return;

        int y = CurrentPos.y;
        int kingX = CurrentPos.x;

        // 우측: 가장 가까운 기물이 "내 팀의 안 움직인 룩"이고, 킹이 도착할 칸이 비어 있어야 함
        for (int x = kingX + 1; x < 8; x++)
        {
            ChessPieceM piece = board[x, y];
            if (piece == null) continue;

            if (piece is Rook rr && !rr.hasMoved && rr.isWhite == isWhite
                && kingX + 2 < 8 && board[kingX + 2, y] == null)
            {
                possibleMoves.Add(new Vector2Int(kingX + 2, y));
            }
            break;
        }

        // 좌측
        for (int x = kingX - 1; x >= 0; x--)
        {
            ChessPieceM piece = board[x, y];
            if (piece == null) continue;

            if (piece is Rook rl && !rl.hasMoved && rl.isWhite == isWhite
                && kingX - 2 >= 0 && board[kingX - 2, y] == null)
            {
                possibleMoves.Add(new Vector2Int(kingX - 2, y));
            }
            break;
        }
    }
}