using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class Pawn : ChessPieceM
{
    [Header("폰 전용 상태")]
    public bool isFirstMove = true;      // 첫 이동 여부 (2칸 전진 가능 여부 판단)
    public bool movedTwoSquaresLastTurn = false; // 직전 턴에 2칸 이동했는지 (상대방이 앙파상할 때 사용)
    

    // 프리팹 생성/ 적용 시 기본값 자동 설정 가능
    private void Reset()
    {
        data = new PieceData(pieceType.Pawn, PieceColor.white, 1,8,"pawn");
    }

    public override void GetPossibleMoves( ChessPieceM[,] board, List<Vector2Int> possibleMoves)
    {
        // 백(White)은 Y+ 방향(위), 흑(Black)은 Y- 방향(아래)으로 전진
        int direction = isWhite ? 1 : -1;

        // 1. 앞으로 1칸 이동
        int forwardY = CurrentPos.y + direction; // 흑이면 direction이 -1이므로 6 -> 5로 정상 감소
       
        if (IsValidIndex(CurrentPos.x, forwardY) && board[CurrentPos.x, forwardY] == null)
        {
            possibleMoves.Add(new Vector2Int(CurrentPos.x, forwardY));

            // 2. 첫 이동 시 앞으로 2칸 이동 (반드시 '첫 칸(forwardY)'이 비어있을 때만 2칸째도 검사해야 함!)
            int doubleForwardY = CurrentPos.y + (direction * 2); // 6에서 시작하면 6 + (-2) = 4가 되어야 함!
            if (isFirstMove && IsValidIndex(CurrentPos.x, doubleForwardY) && board[CurrentPos.x, doubleForwardY] == null)
            {
                possibleMoves.Add(new Vector2Int(CurrentPos.x, doubleForwardY));
            }
        }


        // 3. 대각선 적 기물 잡기 (좌/우 대각선)
        int[] sideX = { -1, 1 };
        foreach (int dx in sideX)
        {
            int targetX = CurrentPos.x + dx;
            int targetY = CurrentPos.y + direction;

            if (IsValidIndex(targetX, targetY))
            {
                
                // 대각선에 적 기물이 있는 경우
                if (board[targetX, targetY] != null && !IsSameTeam(board[targetX, targetY]))
                {
                    possibleMoves.Add(new Vector2Int(targetX, targetY));
                }
            }


            if (ScriptM.TryGet<CheseGameM>(out CheseGameM gameM))
            {
                if (!gameM.canUseEnPassant) return;
                if (!gameM.canUsePromotion) return;



                // 4. 앙파상(En Passant) 검사 예시 수정
                int sidex = targetX; // 옆 칸의 X
                int sideY = CurrentPos.y; // 내 폰과 같은 행(Y)에 있는 적 폰을 검사

                // 검사와 접근하는 좌표를 정확히 통일해야 합니다!
                if (IsValidIndex(sidex, sideY))
                {
                    if (board[sidex, sideY] != null && !IsSameTeam(board[sidex, sideY]))
                    {
                        Pawn enemyPawn = board[sidex, sideY].GetComponent<Pawn>();

                        if (enemyPawn != null && enemyPawn.movedTwoSquaresLastTurn)
                        {
                            // 앙파상으로 이동할 최종 목표 칸 (적 기물 뒤쪽 대각선 칸)
                            int targetCaptureY = CurrentPos.y + direction;

                            if (IsValidIndex(sidex, targetCaptureY))
                            {
                                possibleMoves.Add(new Vector2Int(sidex, targetCaptureY));
                            }
                        }
                    }
                }

                if (CanPromote(CurrentPos.y))
                { 
                    if(isWhite)
                    {
                        gameM.RequestPromotion(this, CurrentPos.x, CurrentPos.y, false);
                    }
                    else
                    {
                        gameM.RequestPromotion(this, CurrentPos.x, CurrentPos.y, true);
                    }
                   
                }
            }
        }
    }

    // 프로모션 조건 체크
    public bool CanPromote(int currentY)
    {
        return (isWhite && currentY == 7) || (!isWhite && currentY == 0);
    }

   

    private new bool IsValidIndex(int x, int y) => x >= 0 && x < 8 && y >= 0 && y < 8;
}

   

