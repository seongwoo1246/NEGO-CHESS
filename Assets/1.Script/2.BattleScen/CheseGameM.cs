using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class CheseGameM : MonoBehaviour
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
    public ChessPieceM[,] pieceGrid = new ChessPieceM[8, 8];

    public PieceColor currentTurn = PieceColor.white;

    private Vector2Int? selectedPos = null; // 현재 선택된 기물의 좌표
    private List<Vector2Int> currentPossibleMoves = new List<Vector2Int>(); // 현재 이동 가능한 칸 표시

    private void Awake()
    {
        ScriptM.Register<CheseGameM>(this, UseSpace.Local);
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<CheseGameM>();
    }


    // 게임 시작시 싱글/멀티 여부에 따라 주입
    public void Initialize(IPieceSpawner spawner)
    {
        this.pieceSpawner = spawner;
    }


    private bool IsMyPiece(ChessPieceM pieceObj)
    {
        if (pieceObj == null) return false;

        
        if (pieceObj == null) return false;

        // 기물의 팀과 현재 턴의 팀이 같으면 내 기물(선택 가능한 기물)로 판정
        return pieceObj.data.Color == currentTurn;
    }

    // 보드의 특정 칸을 클릭했을 때 호출되는 함수
    public void OnTileClicked(int x, int y)
    {
        // 1. 아직 아무것도 선택하지 않은 상태에서 내 기물을 클릭한 경우 -> 선택
        if (selectedPos == null)
        {
            SelectPiece(x, y);
        }
        // 2. 이미 기물을 선택한 상태에서 클릭한 경우
        else
        {
            Vector2Int targetPos = new Vector2Int(x, y);

            // 클릭한 칸이 이동 가능한 위치 중 하나라면 -> 이동 실행!
            if (currentPossibleMoves.Contains(targetPos))
            {
                MovePiece(selectedPos.Value.x, selectedPos.Value.y, x, y); // 실제 이동
                DeselectPiece(); // 선택 해제 및 하이라이트 꺼주기
            }
            // 다른 내 기물을 다시 클릭한 경우 -> 새로운 기물 선택
            else if (pieceGrid[x, y] != null && IsMyPiece(pieceGrid[x, y]))
            {
                SelectPiece(x, y);
            }
            // 그 외 빈 공간이나 잘못된 위치 클릭시 -> 선택 취소
            else
            {
                DeselectPiece();
            }
        }
    }

    private void SelectPiece(int x, int y)
    {
        ChessPieceM pieceObj = pieceGrid[x, y];
        if (pieceObj == null) return;

        selectedPos = new Vector2Int(x, y);

        // 기물에서 이동 가능 경로 받아오기
        currentPossibleMoves = pieceObj.GetPossibleMoves(selectedPos.Value, pieceGrid);

        // TODO: currentPossibleMoves 위치에 타일 하이라이트 이펙트 켜주기
    }

    private void DeselectPiece()
    {
        selectedPos = null;
        currentPossibleMoves.Clear();
        // TODO: 타일 하이라이트 이펙트 모두 끄기
    }


    /// <summary>
    /// 원하는 위치(x, y)에 원하는 기물 프리팹을 생성합니다.
    /// (변형 체스 특성에 맞춰 개수나 종류 제한 없이 호출 가능)
    /// </summary>
    /// <param name="piecePrefab">생성할 기물 프리팹</param>
    /// <param name="x">Target Grid X (0~7)</param>
    /// <param name="y">Target Grid Y (0~7)</param>
    /// <returns>생성된 기물의 GameObject</returns>
    public ChessPieceM SpawnPiece(GameObject piecePrefab, int x, int y)
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

        // 3. 기물 인스턴스화 (GameObject 생성)
        GameObject newPieceObj = pieceSpawner.Spawn(piecePrefab, spawnWorldPos, Quaternion.identity); 

        // 4. 생성된 오브젝트에서 ChessPieceM 컴포넌트 추출
        ChessPieceM newPiece = newPieceObj.GetComponent<ChessPieceM>();

        // 5. 2차원 배열에 기물 등록 및 반환
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
        ChessPieceM pieceToMove = pieceGrid[fromX, fromY];
        if (pieceToMove == null)
        {
            Debug.LogWarning($"[{fromX}, {fromY}] 위치에 이동시킬 기물이 없습니다!");
            return;
        }

        ChessPieceM capturedPiece = pieceGrid[toX, toY];
        // 2. 도착지에 이미 상대 기물이 존재한다면 포획(잡기) 처리
        if (capturedPiece != null)
        {
            // Undo 기능을 고려한다면 완전 Destroy보다는 SetActive(false) 처리가 용이합니다.
            capturedPiece.gameObject.SetActive(false);
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
            pieceSpawner.Despawn(pieceGrid[x, y].gameObject);
            pieceGrid[x, y] = null;
        }
    }

    /// <summary>
    /// 특정 위치에 어떤 기물이 있는지 조회합니다.
    /// </summary>
    public GameObject GetPieceAt(int x, int y)
    {
        return pieceGrid[x, y].gameObject;
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
            lastMove.CapturedPiece.gameObject.SetActive(true);
        }
        else
        {
            pieceGrid[lastMove.To.x, lastMove.To.y] = null;
        }
    }

    // 특정 팀(isWhiteTarget)의 킹이 현재 체크 상태인지 확인하는 함수
    public bool IsKingInCheck(bool isWhiteTarget, ChessPieceM[,] grid)
    {
        // 1. target 팀의 킹 위치 찾기
        Vector2Int kingPos = FindKingPosition(isWhiteTarget, grid);

        // 2. 상대방(적)의 모든 기물이 이동/공격할 수 있는 좌표들을 모음
        List<Vector2Int> enemyAttackMoves = GetAllEnemyAttackMoves(!isWhiteTarget, grid);

        // 3. 적의 공격 가능 범위에 킹의 위치가 포함되어 있다면 체크!
        return enemyAttackMoves.Contains(kingPos);
    }

    // 1. target 팀의 킹 위치 찾기
    private Vector2Int FindKingPosition(bool isWhiteTarget, ChessPieceM[,] grid)
    {
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                
                if (grid[x, y] != null)
                {
                    
                   
                    // 팀이 일치하고, 해당 기물이 King 클래스(또는 킹 타입)인지 확인
                    if (grid[x, y] != null && grid[x, y].isWhite == isWhiteTarget && grid[x, y] is King)
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
        }

        // 킹을 찾지 못한 예외 상황 처리 (기본값)
        return new Vector2Int(-1, -1);
    }

    // 2. 적 팀의 모든 기물이 이동/공격할 수 있는 좌표들을 모음
    private List<Vector2Int> GetAllEnemyAttackMoves(bool isWhiteEnemy, ChessPieceM[,] grid)
    {
        List<Vector2Int> attackMoves = new List<Vector2Int>();

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                
                if (grid[x, y] != null)
                {
                    
                    // 적 팀의 기물인 경우
                    if (grid[x, y] != null && grid[x, y].isWhite == isWhiteEnemy)
                    {
                        // 해당 기물의 이동 가능 경로 받아오기
                        List<Vector2Int> moves = grid[x, y].GetPossibleMoves(new Vector2Int(x, y), grid);

                        // 수집된 모든 공격 경로에 추가
                        attackMoves.AddRange(moves);
                    }
                }
            }
        }

        return attackMoves;
    }
}
