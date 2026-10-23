using System.Collections.Generic;
using UnityEngine;

public static class singlePlayer 
{
     public static string playerName;
     public static int birthMonth;
     public static int birthDay;
     public static bool isGirl = true;
     public static string gender = "여";
    public static int maxCost = 49;
     public static int Gold = 0;
    public static Sprite icon;
    public static float bonusNegoPower;
    public static int selectedCharacterId;


    #region 강화를 위한 데이터와 함수
    public static List<PieceData> pieceDataList = new List<PieceData>()
    {
        new PieceData(pieceType.Pawn, PieceColor.white, 1, 8, "pawn"),
        new PieceData(pieceType.Bishop, PieceColor.white, 5, 2, "bishop"),
        new PieceData(pieceType.Knight, PieceColor.white, 5, 2, "knight"),
        new PieceData(pieceType.Rook, PieceColor.white, 5, 2, "rook"),
        new PieceData(pieceType.Queen, PieceColor.white, 10, 1, "queen")
    };
    public static void AddMaxPiece(pieceType type, int amount)
    {
        for (int i = 0; i < pieceDataList.Count; i++)
        {
            if (pieceDataList[i].Type == type)
            {
                PieceData data = pieceDataList[i];
                data.maxPiece += amount;
                pieceDataList[i] = data; // 구조체 값 갱신

                Debug.Log($"{type} 최대 개수가 {amount}만큼 증가했습니다! 현재 최대치: {data.maxPiece}");
                return;
            }
        }
        Debug.LogWarning($"해당 기물 데이터를 찾지 못했습니다: {type}");
    }

    #endregion
}


