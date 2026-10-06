using System.Collections.Generic;
using UnityEngine;

public class King : ChessPieceM
{
    public bool hasMoved = false; // 캐슬링 조건용 (한 번이라도 움직였는지)

    // 프리팹 생성/ 적용 시 기본값 자동 설정 가능
    private void Reset()
    {
        data = new PieceData(pieceType.King, PieceColor.white, 0, "King");
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
                    // TODO: 이동하려는 칸이 적의 공격 범위(체크 상태)인지 검사 필요
                    possibleMoves.Add(new Vector2Int(targetX, targetY));
                }
            }
        }

        // TODO: 캐슬링(Castling) 조건 검사 및 좌표 추가

    }
}
