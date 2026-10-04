using System.Collections.Generic;
using UnityEngine;

public class Bishop : ChessPieceM
{
    // 프리팹 생성/ 적용 시 기본값 자동 설정 가능
    private void Reset()
    {
        data = new PieceData(pieceType.Bishop, PieceColor.white, 5, "Bishop");
    }

    public override List<Vector2Int> GetPossibleMoves(Vector2Int currentPos, GameObject[,] board)
    {
        List<Vector2Int> possibleMoves = new List<Vector2Int>();

        // 대각선 4방향 정의
        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(1, 1),   // 우상단
            new Vector2Int(1, -1),  // 우하단
            new Vector2Int(-1, 1),  // 좌상단
            new Vector2Int(-1, -1)  // 좌하단
        };

        foreach (Vector2Int dir in directions)
        {
            int nextX = currentPos.x + dir.x;
            int nextY = currentPos.y + dir.y;

            // 보드 경계를 벗어나지 않을 때까지 대각선 탐색
            while (IsValidIndex(nextX, nextY))
            {
                GameObject targetObj = board[nextX, nextY];

                // 1. 빈 칸인 경우 -> 이동 목록에 추가하고 계속 탐색
                if (targetObj == null)
                {
                    possibleMoves.Add(new Vector2Int(nextX, nextY));
                }
                else
                {
                    // 2. 적 기물인 경우 -> 잡기 목록에 추가하고 해당 방향 탐색 종료
                    if (!IsSameTeam(targetObj))
                    {
                        possibleMoves.Add(new Vector2Int(nextX, nextY));
                    }
                    // 3. 아군을 만났거나 적을 잡았으므로 길 막힘 -> 루프 탈출
                    break;
                }

                // 다음 대각선 칸으로 전진
                nextX += dir.x;
                nextY += dir.y;
            }
        }

        return possibleMoves;
    }
}

