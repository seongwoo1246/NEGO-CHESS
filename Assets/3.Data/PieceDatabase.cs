using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct PiecePrefabMapping
{
    public pieceType type;
    public PieceColor color;
    public GameObject prefab;
}
[CreateAssetMenu(fileName = "PicecDatabase", menuName = "Piece Data/Piece Database")]
public class PieceDatabase : ScriptableObject
{
    public List<PiecePrefabMapping> mappings;

    public GameObject GetPrefab(pieceType type, PieceColor color)
    {
        foreach (var mapping in mappings)
        {
            if (mapping.type == type && mapping.color == color)
            {
                return mapping.prefab;
            }
        }
        return null;
    }



}
