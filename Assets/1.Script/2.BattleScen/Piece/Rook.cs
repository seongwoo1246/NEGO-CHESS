using System.Collections.Generic;
using UnityEngine;

public class Rook : ChessPieceM
{
    public bool hasMoved = false; // 캐슬링 조건용 (한 번이라도 움직였는지)

    // 프리팹 생성/ 적용 시 기본값 자동 설정 가능
    private void Reset()
    {
        data = new PieceData(pieceType.Rook, PieceColor.white, 5, "Rook");
    }
    
    public override void  GetPossibleMoves(ChessPieceM[,] board,List<Vector2Int> possibleMoves)
    {
        

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int dir in directions)
        {
            int nextX = CurrentPos.x + dir.x;
            int nextY = CurrentPos.y + dir.y;

            // 판 범위를 벗어나지 않을 때까지 직선 탐색
            while (IsValidIndex(nextX, nextY))
            {
                // 1. 빈 칸인 경우 -> 이동 가능 추가 후 다음 칸 계속 탐색
                if (board[nextX, nextY] == null)
                {
                    possibleMoves.Add(new Vector2Int(nextX, nextY));
                }
                else
                {
                    // 2. 적 기물인 경우 -> 잡기(이동 가능) 추가 후 이 방향 탐색 종료
                    if (!IsSameTeam(board[nextX, nextY]))
                    {
                        possibleMoves.Add(new Vector2Int(nextX, nextY));
                    }
                    // 3. 아군 기물이거나 적 기물을 만났으므로 막힘 -> 루프 탈출
                    break;
                }

                // 다음 칸으로 이동
                nextX += dir.x;
                nextY += dir.y;
            }
        }

       
    }
}
