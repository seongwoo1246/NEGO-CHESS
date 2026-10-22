using System.Collections.Generic;
using UnityEngine;

public class King : ChessPieceM
{
    public bool hasMoved = false; // 캐슬링 조건용 (한 번이라도 움직였는지)

    // 프리팹 생성/ 적용 시 기본값 자동 설정 가능
    private void Reset()
    {
        data = new PieceData(pieceType.King, PieceColor.white, 0,1, "King");
    }

    public override void GetPossibleMoves( ChessPieceM[,] board, List<Vector2Int> possibleMoves)
    {

        // 상, 하, 좌, 우, 대각선 8방향 (1칸씩)
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
                

                // 빈 칸이거나 적 기물이면 이동 가능
                if (board[targetX, targetY] == null || !IsSameTeam(board[targetX, targetY]))
                {
                    
                    possibleMoves.Add(new Vector2Int(targetX, targetY));
                }
            }
        }

        // 캐슬링(Castling) 조건 검사 (안전성 검사 생략 버전)
        if (!hasMoved)
        {
            int y = CurrentPos.y;

            // 1. 우측 캐슬링 (Kingside) - 5번, 6번 칸이 '완벽히 비어있는지' 확인
            ChessPieceM rookRight = board[7, y];
            if (rookRight != null && rookRight.data.Type == pieceType.Rook)
            {
                Rook rRight = rookRight as Rook;
                if (rRight != null && !rRight.hasMoved)
                {
                    if (board[5, y] == null && board[6, y] == null) // 사이가 비어있음
                    {
                        possibleMoves.Add(new Vector2Int(CurrentPos.x + 2, y));
                    }
                }
            }

            // 2. 좌측 캐슬링 (Queenside) - 1, 2, 3번 칸이 '완벽히 비어있는지' 확인
            ChessPieceM rookLeft = board[0, y];
            if (rookLeft != null && rookLeft.data.Type == pieceType.Rook)
            {
                Rook rLeft = rookLeft as Rook;
                if (rLeft != null && !rLeft.hasMoved)
                {
                    if (board[1, y] == null && board[2, y] == null && board[3, y] == null) // 사이가 비어있음
                    {
                        possibleMoves.Add(new Vector2Int(CurrentPos.x - 2, y));
                    }
                }
            }
        }

    }
}
