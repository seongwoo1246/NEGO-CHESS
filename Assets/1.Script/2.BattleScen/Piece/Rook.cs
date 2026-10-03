using System.Collections.Generic;
using UnityEngine;

public class Rook : ChessPieceM
{
    // 프리팹 생성/ 적용 시 기본값 자동 설정 가능
    private void Reset()
    {
        data = new PieceData(pieceType.Rook, PieceColor.white, 5, "Rook");
    }

    public override List<Vector2Int> GetPossibleMoves(Vector2Int currentPos, GameObject[,] board)
    {
        throw new System.NotImplementedException();
    }
}
