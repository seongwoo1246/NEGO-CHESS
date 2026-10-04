using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Debug = DebugM<ChessPieceM>;


[System.Serializable]
public struct MoveData
{
    public Vector2Int From;          // 출발 위치 (x, y)
    public Vector2Int To;            // 도착 위치 (x, y)
    public ChessPieceM MovedPiece;    // 이동한 기물
    public ChessPieceM CapturedPiece; // 잡힌 기물 (없으면 null)

    // 특수 이동 복구용 (필요시 추가)
    public bool IsPromotion; // 프로모션
    public bool IsEnPassant; // 앙파상
    public bool IsCastling; // 캐슬링

    /// <summary>
    /// 기보를 저장하기 위해 움직임 데이터 구조체
    /// </summary>
    /// <param name="from">출발칸</param>
    /// <param name="to">도착칸</param>
    /// <param name="movedPiece">움직인 말</param>
    /// <param name="capturedPiece">잡힌 말</param>
    public MoveData(Vector2Int from, Vector2Int to, ChessPieceM movedPiece, ChessPieceM capturedPiece = null)
    {
        From = from;
        To = to;
        MovedPiece = movedPiece;
        CapturedPiece = capturedPiece;
        IsPromotion = false;
        IsEnPassant = false;
        IsCastling = false;
    }
}

[System.Serializable]
public struct PieceData
{
    public pieceType Type; //기물 종류(Enum)
    public PieceColor Color; // 팀 분류
    public int cost; // 기물의 코스트
    public string pieceName; // 기물 이름
    public Sprite icon; // UI용 아이콘

    public PieceData(pieceType type, PieceColor team, int cost, string name = "")
    {
        Type = type;
        Color = team;
        this.cost = cost;
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

    public bool isWhite;

    // 0~7 보드 범위 검사
    protected bool IsValidIndex(int x, int y)
    {
        return x >= 0 && x < 8 && y >= 0 && y < 8;
    }

    /// <summary>
    /// 같은 팀 기물인지 체크
    /// </summary>
    protected bool IsSameTeam(ChessPieceM targetObj)
    {
        if (targetObj == null) return false;

        
        return targetObj != null && targetObj.isWhite == this.isWhite;
    }

    public abstract List<Vector2Int> GetPossibleMoves(Vector2Int currentPos, ChessPieceM[,] board);

   

    private void Awake()
    {
        ScriptM.Register<ChessPieceM>(this, UseSpace.Local);
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<ChessPieceM>();
    }

}