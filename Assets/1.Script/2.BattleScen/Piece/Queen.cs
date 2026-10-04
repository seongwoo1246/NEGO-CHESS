using System.Collections.Generic;
using UnityEngine;

public class Queen : ChessPieceM
{
    // 프리팹 생성/ 적용 시 기본값 자동 설정 가능
    private void Reset()
    {
        data = new PieceData(pieceType.Queen, PieceColor.white, 10, "Queen");
    }

    public override List<Vector2Int> GetPossibleMoves(Vector2Int currentPos, ChessPieceM[,] board)
    {
        List<Vector2Int> possibleMoves = new List<Vector2Int>();

        // 룩(4방향) + 비숍(4방향) = 총 8개 방향
        Vector2Int[] directions = new Vector2Int[]
        {
            // 룩 방향 (상, 하, 좌, 우)
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),

            // 비숍 방향 (대각선 4개)
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        foreach (Vector2Int dir in directions)
        {
            int nextX = currentPos.x + dir.x;
            int nextY = currentPos.y + dir.y;

            // 보드 경계를 벗어나지 않을 때까지 직선 및 대각선 탐색
            while (IsValidIndex(nextX, nextY))
            {
                

                // 1. 빈 칸인 경우 -> 이동 가능 추가 후 계속 진행
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
                    // 3. 아군 기물이거나 적 기물을 만나 길 막힘 -> 루프 탈출
                    break;
                }

                // 다음 칸으로 전진
                nextX += dir.x;
                nextY += dir.y;
            }
        }

        return possibleMoves;
    }
}
