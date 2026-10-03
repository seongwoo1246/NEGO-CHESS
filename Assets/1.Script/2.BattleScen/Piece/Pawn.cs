using UnityEngine;
using System.Collections.Generic;

public class Pawn : ChessPieceM
{

    // 프리팹 생성/ 적용 시 기본값 자동 설정 가능
    private void Reset()
    {
        data = new PieceData(pieceType.Pawn, PieceColor.white, 1,"pawn");
    }

    public override List<Vector2Int> GetPossibleMoves(Vector2Int currentPos, GameObject[,] board)
    {
        throw new System.NotImplementedException();
    }

   
}
