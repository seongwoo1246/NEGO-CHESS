
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;


/// <summary>
/// 체스판을 세팅하기 위해서 만든 스크립트 
/// (월드 좌표 및 그리드 좌표는 여기서 구할 예정)
/// </summary>
public class ChessBoardM : MonoBehaviour
{
    [Header("기준점 및 타일 설정")]
    [SerializeField] private GameObject BoardSlot;
    [SerializeField] private Transform BoardPosition;
    [SerializeField] private Transform originPoint; 
    [SerializeField] private float tileSize =1f;

  private SinglePoolSpawner spawner;

    public int boardSize = 8;

    public GameObject[,] spawnedTiles;

    // 멀티시 클라이언트가 흑이면 true 싱글플레이나 백일 경우는 false로 둔다.
    public bool IsFlipped { get; set; } = false;


    [Header("선택 및 입력 상태")]
    private Vector2Int? selectedPos = null;
    private List<Vector2Int> currentPossibleMoves = new List<Vector2Int>();

    private void Awake()
    {
        ScriptM.Register<ChessBoardM>(this, UseSpace.Local);
       

    }
    private void Start()
    {
        if (ScriptM.TryGet<SinglePoolSpawner>(out SinglePoolSpawner service))
        {
            spawner = service;
        }
        InitializeBoard();
    }

    public void InitializeBoard()
    {
        spawnedTiles = new GameObject[boardSize, boardSize];
        if (ScriptM.TryGet<CheseGameM>(out CheseGameM game))
    {
            for (int x = 0; x < boardSize; x++)
        {
            for (int y = 0; y < boardSize; y++)
            {
                // 1. 앞서 만드신 GetworldPosition 함수를 통해 해당 그리드의 월드 좌표를 가져옴
                Vector3 worldPos = GetWorldPosition(x, y);

                // 2. 오브젝트 풀러(`SinglePoolSpawner`)를 통해 타일 소환
               
                    GameObject tile = spawner.Spawn(BoardSlot, worldPos, Quaternion.identity);
                    SpriteRenderer sr = tile.GetComponent<SpriteRenderer>();

                    bool isEven = (x + y) % 2 == 0;
                    Color baseColor = isEven ? new Color(0.9f, 0.9f, 0.8f) : new Color(0.4f, 0.3f, 0.2f); // 밝은 칸 / 어두운 칸

                    // 💡 타일에 컴포넌트를 가져와서 색상 초기화 위임
                    BoardTile tileComponent = tile.GetComponent<BoardTile>();
                    if (tileComponent != null)
                    {
                        tileComponent.SetBaseColor(baseColor);
                    }

                    // 3. 부모 오브젝트가 있다면 하이라키 정리
                    if (BoardPosition != null)
                    {
                        tile.transform.SetParent(BoardPosition);
                    }

                    // 4. 나중을 위해 배열에 저장
                    spawnedTiles[x, y] = tile;

                }
            }
        }

        
    }

    /// <summary>
    /// 논리적 그리드 좌표를 받아와서 실제 클라이언트 화면에 월드 좌표로 바꿔서 보여준다.
    /// 멀티시에는 흑백을 시점 반전으로 보요줄 예정
    /// </summary>
    /// <param name="x">열 0~7 = a~h</param>
    /// <param name="y">행 0~7 = 1~8</param>
    /// <returns>v3에 사용될 원드좌표</returns>
    public Vector3 GetWorldPosition(int x,int y)
    {
        // 내가 흑일 경우 시점이 180도 돌아가서 계산
        //(7-x,7-y)로 하여 상대방 관점에도 자신의 기물이 아래쪽에 배치되게 보이게 함
        int viewX = IsFlipped?(7-x) : x;
        int viewY = IsFlipped?(7-y) : y;
        

        Vector3 startPos = originPoint.position;

        // 기준점에 타일 사이즈만큼 곱해서 월드 좌표 계산
        float targetX = startPos.x + (x * tileSize);
        float targetY = startPos.y + (y * tileSize);

        // 기물이 판에 가려지지 않게 조금 당겨두기
        return new Vector3(targetX, targetY, -0.1f);

    }

    /// <summary>
    /// 마우스로 클릭한 월드 좌표를 받아와서 논리적 그리드 좌표로 변환한다.
    /// </summary>
    /// <param name="mouseWorldPos">마우스 클릭 위치 (Camera.main.ScreenToWorldPoint)</param>
    /// <returns>논리적 그리드 좌표 (0-7,0-7)</returns>
    public Vector2Int GetGridIndex(Vector3 mouseWorldPos)
    {
        Vector3 startPos = originPoint.position;

        // A1 칸 중앙 기준 반 칸(tileSize * 0.5f)만큼 좌하단으로 보정하여 칸 영역 계산
        int viewX = Mathf.FloorToInt((mouseWorldPos.x - startPos.x + (tileSize * 0.5f)) / tileSize);
        int viewY = Mathf.FloorToInt((mouseWorldPos.y - startPos.y + (tileSize * 0.5f)) / tileSize);

        int localX = IsFlipped ? (7-viewX) : viewX;
        int localY = IsFlipped ? (7-viewY) : viewY;

        return new Vector2Int(localX, localY);
    }


    private void Update()
    {
        // 1. New Input System 방식으로 마우스 왼쪽 버튼이 '방금 눌린 순간'을 감지
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {

            // 3. New Input System에서 마우스 스크린 좌표 가져오기
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mouseworldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);

            // 4. 매니저에서 마우스 클릭 후 레이캐스트 쏘기
            RaycastHit2D hit = Physics2D.Raycast(mouseworldPos, Vector2.zero);
            if (hit.collider != null)
            {

                //  계산된 그리드 좌표가 유효한지 체크
                Vector2Int gridPos = GetGridIndex(mouseworldPos);

                if (gridPos.x >= 0 && gridPos.x < 8 && gridPos.y >= 0 && gridPos.y < 8)
                {
                    if (ScriptM.TryGet<CheseGameM>(out CheseGameM gameM))
                    {
                        gameM.OnTileClicked(gridPos.x, gridPos.y);
                        Debug.Log($"{gridPos.x}{gridPos.y}");
                    }

                }
            }
        }
    }

   
}
