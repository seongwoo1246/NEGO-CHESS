using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Debug = DebugM<ChessPieceM>;


[System.Serializable]
public struct MoveData
{
    public Vector2Int From;          // 출발 위치 (x, y)
    public Vector2Int To;            // 도착 위치 (x, y)
    public GameObject MovedPiece;    // 이동한 기물
    public GameObject CapturedPiece; // 잡힌 기물 (없으면 null)

    // 특수 이동 복구용 (필요시 추가)
    public bool IsPromotion; // 프로모션
    public bool IsEnPassant; // 앙상블
    public bool IsCastling; // 캐슬링

    /// <summary>
    /// 기보를 저장하기 위해 움직임 데이터 구조체
    /// </summary>
    /// <param name="from">출발칸</param>
    /// <param name="to">도착칸</param>
    /// <param name="movedPiece">움직인 말</param>
    /// <param name="capturedPiece">잡힌 말</param>
    public MoveData(Vector2Int from, Vector2Int to, GameObject movedPiece, GameObject capturedPiece = null)
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




public class ChessPieceM : MonoBehaviour
{

    [Header("멀티용으로 사용되는 것")]
    [SerializeField] private NetworkRunner runner; // 멀티용

    private List<MoveData> MoveHistory = new List<MoveData>();


    /// <summary>
    /// 현재 게임모드에 맞는 Spawner를 넣어줌
    /// </summary>
    private IPieceSpawner pieceSpawner;


    // 체스판 위에 존재하는 모든 기물 오브젝트를 논리 좌표(x, y)로 추적하기 위한 2차원 배열
    // (x: 0~7, y: 0~7) 위치에 존재하는 기물 GameObject를 저장합니다.
    private GameObject[,] pieceGrid = new GameObject[8, 8];

    private void Awake()
    {
        ScriptM.Register<ChessPieceM>(this, UseSpace.Local);
    }


    // 게임 시작시 싱글/멀티 여부에 따라 주입
    public void Initialize(IPieceSpawner spawner)
    {
        this .pieceSpawner = spawner;
    }

    /// <summary>
    /// 원하는 위치(x, y)에 원하는 기물 프리팹을 생성합니다.
    /// (변형 체스 특성에 맞춰 개수나 종류 제한 없이 호출 가능)
    /// </summary>
    /// <param name="piecePrefab">생성할 기물 프리팹</param>
    /// <param name="x">Target Grid X (0~7)</param>
    /// <param name="y">Target Grid Y (0~7)</param>
    /// <returns>생성된 기물의 GameObject</returns>
    public GameObject SpawnPiece(GameObject piecePrefab, int x, int y)
    {

        if (!ScriptM.TryGet<ChessBoardM>(out var boardM))
        {
            Debug.LogError("ChessPieceM 서비스를 찾을 수 없습니다.");
            return null;
        }

        // 1. 이미 해당 위치에 기물이 있다면 기존 기물 제거 (또는 예외 처리)
        if (pieceGrid[x, y] != null)
        {
            DestroyPiece(x, y);
        }
     

        // 2. 입력받은 논리 좌표(x, y)를 기반으로 월드 좌표 산출
        Vector3 spawnWorldPos = boardM.GetWorldPosition(x, y);

        // 3. 기물 인스턴스화
        // (주의: 추후 퓨전 멀티플레이 적용 시에는 Runner.Spawn()으로 대체됩니다)
        GameObject newPiece = pieceSpawner.Spawn(piecePrefab, spawnWorldPos, Quaternion.identity);
        // 4. 2차원 배열에 기물 등록 (데이터 매핑)
        pieceGrid[x, y] = newPiece;
        return newPiece;


    }

    /// <summary>
    /// 지정한 출발 위치(fromX, fromY)의 기물을 목적지(toX, toY)로 이동시킵니다.
    /// </summary>
    /// <param name="fromX">출발 X (0~7)</param>
    /// <param name="fromY">출발 Y (0~7)</param>
    /// <param name="toX">도착 X (0~7)</param>
    /// <param name="toY">도착 Y (0~7)</param>
    public void MovePiece(int fromX, int fromY, int toX, int toY)
    {
        if (!ScriptM.TryGet<ChessBoardM>(out var boardM))
        {
            Debug.LogError("ChessPieceM 서비스를 찾을 수 없습니다.");
            return;
        }
        // 1. 출발지에 기물이 존재하는지 검증
        GameObject pieceToMove = pieceGrid[fromX, fromY];
        if (pieceToMove == null)
        {
            Debug.LogWarning($"[{fromX}, {fromY}] 위치에 이동시킬 기물이 없습니다!");
            return;
        }

        GameObject capturedPiece = pieceGrid[toX, toY];
        // 2. 도착지에 이미 상대 기물이 존재한다면 포획(잡기) 처리
        if (capturedPiece != null)
        {
            // Undo 기능을 고려한다면 완전 Destroy보다는 SetActive(false) 처리가 용이합니다.
            capturedPiece.SetActive(false);
        }

        // 3. 목적지의 월드 좌표 계산
        Vector3 targetWorldPos = boardM.GetWorldPosition(toX, toY);

        // 4. 실제로 기물의 Transform 위치 이동 (직부여 또는 연출용 트윈 적용)
        pieceToMove.transform.position = targetWorldPos;

        // 5. 논리 배열 데이터 업데이트 (출발지는 비우고, 목적지로 등록)
        pieceGrid[toX, toY] = pieceToMove;
        pieceGrid[fromX, fromY] = null;

        MoveData move = new MoveData(
           new Vector2Int(fromX, fromY),
           new Vector2Int(toX, toY),
           pieceToMove,
           capturedPiece
       );
        MoveHistory.Add(move);
    }

    /// <summary>
    /// 특정 위치의 기물을 제거(파괴/포획)합니다.
    /// </summary>
    public void DestroyPiece(int x, int y)
    {
        if (pieceGrid[x, y] != null)
        {
            // 풀로 반환하거나 Despawn처리
            pieceSpawner.Despawn(pieceGrid[x, y]);
            pieceGrid[x, y] = null;
        }
    }

    /// <summary>
    /// 특정 위치에 어떤 기물이 있는지 조회합니다.
    /// </summary>
    public GameObject GetPieceAt(int x, int y)
    {
        return pieceGrid[x, y];
    }

    /// <summary>
    /// 한 수 무르기
    /// </summary>
    public void UndoLastMove()
    {
        if (MoveHistory.Count == 0) return;

        // 가장 마지막 기보 가져오기 및 리스트에서 제거
        int lastIndex = MoveHistory.Count - 1;
        MoveData lastMove = MoveHistory[lastIndex];
        MoveHistory.RemoveAt(lastIndex);

        // 1. 이동한 기물 제자리로 원복
        pieceGrid[lastMove.From.x, lastMove.From.y] = lastMove.MovedPiece;
        if (ScriptM.TryGet<ChessBoardM>(out var boardM))
        {
            lastMove.MovedPiece.transform.position = boardM.GetWorldPosition(lastMove.From.x, lastMove.From.y);
        }

        // 2. 잡혔던 기물이 있다면 다시 복원
        if (lastMove.CapturedPiece != null)
        {
            pieceGrid[lastMove.To.x, lastMove.To.y] = lastMove.CapturedPiece;
            lastMove.CapturedPiece.SetActive(true);
        }
        else
        {
            pieceGrid[lastMove.To.x, lastMove.To.y] = null;
        }
    }
}