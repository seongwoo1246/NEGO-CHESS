using System.Collections.Generic;
using UnityEngine;

public class Knight : ChessPieceM
{
    // 프리팹 생성/ 적용 시 기본값 자동 설정 가능
    private void Reset()
    {
        data = new PieceData(pieceType.Knight, PieceColor.white, 5, "Knight");
    }

    public override List<Vector2Int> GetPossibleMoves(Vector2Int currentPos, GameObject[,] board)
    {
        List<Vector2Int> possibleMoves = new List<Vector2Int>();

        // 나이트가 이동할 수 있는 L자 상대 좌표 8개
        Vector2Int[] knightMoves = new Vector2Int[]
        {
            new Vector2Int(-1, 2),  new Vector2Int(1, 2),   // 위쪽 L자
            new Vector2Int(-1, -2), new Vector2Int(1, -2),  // 아래쪽 L자
            new Vector2Int(2, 1),   new Vector2Int(2, -1),  // 오른쪽 L자
            new Vector2Int(-2, 1),  new Vector2Int(-2, -1)  // 왼쪽 L자
        };

        foreach (Vector2Int move in knightMoves)
        {
            int targetX = currentPos.x + move.x;
            int targetY = currentPos.y + move.y;

            // 1. 체스판 범위를 벗어나는지 검사
            if (IsValidIndex(targetX, targetY))
            {
                GameObject targetObj = board[targetX, targetY];

                // 2. 빈 칸이거나 적 기물이 있는 경우 이동 가능
                if (targetObj == null || !IsSameTeam(targetObj))
                {
                    possibleMoves.Add(new Vector2Int(targetX, targetY));
                }
            }
        }

        return possibleMoves;
    }
}
