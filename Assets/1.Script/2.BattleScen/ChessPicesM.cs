
using System.Collections.Generic;
using UnityEngine;



[System.Serializable]
public struct MoveData
{
    public Vector2Int From;          // 출발 위치 (x, y)
    public Vector2Int To;            // 도착 위치 (x, y)
    public ChessPieceM MovedPiece;    // 이동한 기물
    public ChessPieceM CapturedPiece; // 잡힌 기물 (없으면 null)
    public int oldCost;
    public int newCost;

    // 특수 이동 복구용 (필요시 추가)
    public bool IsPromotion; // 프로모션
    public bool IsEnPassant; // 앙파상
    public bool IsCastling; // 캐슬링

    //  자유 배치 캐슬링을 위해 룩이 어디서 왔는지 기억할 변수
    public Vector2Int RookFrom;



    // ── 무르기 복구용 (잡히기/이동하기 직전 상태) ──
    public bool MovedHadMoved;          // 움직인 기물이 이 수 이전에 이미 움직였는지 (폰: !isFirstMove, 킹/룩: hasMoved)
    public pieceType CapturedType;      // 잡힌 기물의 종류
    public PieceColor CapturedColor;    // 잡힌 기물의 색
    public bool CapturedHadMoved;       // 잡힌 기물이 이미 움직였던 기물인지
    public int CapturedCost;  

    public MoveData(Vector2Int from, Vector2Int to, ChessPieceM movedPiece, int oldcost, int newcost, ChessPieceM capturedPiece = null)
    {
        From = from;
        To = to;
        MovedPiece = movedPiece;
        CapturedPiece = capturedPiece;
        oldCost = oldcost;
        newCost = newcost;
        IsPromotion = false;
        IsEnPassant = false;
        IsCastling = false;
        RookFrom = Vector2Int.zero;

        MovedHadMoved = false;
        CapturedType = default;
        CapturedColor = default;
        CapturedHadMoved = false;
        CapturedCost = 0;
    }
}

[System.Serializable]
public struct PieceData
{
    public pieceType Type; //기물 종류(Enum)
    public PieceColor Color; // 팀 분류
    public int cost; // 기물의 코스트
    public int maxPiece; // 최대 기물 수
    public string pieceName; // 기물 이름
    public Sprite icon; // UI용 아이콘

    public PieceData(pieceType type, PieceColor team, int cost,int maxPiece, string name = "")
    {
        Type = type;
        Color = team;
        this.cost = cost;
        this.maxPiece = maxPiece;
        pieceName = string.IsNullOrEmpty(name) ? type.ToString() : name;
        icon = null;
    }
}

/// <summary>
/// 말들의 움직임을 관리 하는 추상화 클래스 코어
/// </summary>
public abstract class ChessPieceM : MonoBehaviour
{
    [Header("piece Info")]
    public PieceData data; // 기물의 기본 데이터 
    public PieceData data_B; // 기물의 기본 데이터 

    public bool isWhite;
    public Vector2Int CurrentPos {  get;  set; }
    // 0~7 보드 범위 검사
    protected bool IsValidIndex(int x, int y)
    {
        return x >= 0 && x < 8 && y >= 0 && y < 8;
    }

    /// <summary>
    /// 같은 팀 기물인지 체크
    /// </summary>
   public bool IsSameTeam(ChessPieceM targetObj)
    {
        if (targetObj == null) return false;

        
        return  targetObj.isWhite == this.isWhite;
    }


    public abstract void GetPossibleMoves(ChessPieceM[,] grid, List<Vector2Int> outputMoves);

 

}