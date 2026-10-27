using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SingleChessGameM : MonoBehaviour
{
    private void Start()
    {
        positionHistory.Clear();

        whiteTimer.TimerTime = 300;
        blackTimer.TimerTime = 300;

        if (ScriptM.TryGet<SinglePoolSpawner>(out SinglePoolSpawner service))
        {
            spawner = service;
        }


        if (ResetBtn_W != null) { ResetBtn_W.onClick.AddListener(ResetAllPieces); }
        if (ReadyBtn_B != null) { ReadyBtn_B.onClick.AddListener(SetReady); }
        if (ResetBtn_W != null) { ResetBtn_W.onClick.AddListener(ResetAllPieces); }
        if (ReadyBtn_B != null) { ReadyBtn_B.onClick.AddListener(SetReady_B); }
        if (SettingBtn != null) SettingBtn.onClick.AddListener(ActiveSelfSetting);
        if (QueenBtn_W != null) QueenBtn_W.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase, pieceType.Queen));
        if (KnightBtn_W != null) KnightBtn_W.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase, pieceType.Knight));
        if (BishopBtn_W != null) BishopBtn_W.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase, pieceType.Bishop));
        if (RookBtn_W != null) RookBtn_W.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase, pieceType.Rook));
        if (QueenBtn_B != null) QueenBtn_B.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase, pieceType.Queen));
        if (KnightBtn_B != null) KnightBtn_B.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase, pieceType.Knight));
        if (BishopBtn_B != null) BishopBtn_B.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase, pieceType.Bishop));
        if (RookBtn_B != null) RookBtn_B.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase, pieceType.Rook));
        if (TimerBtn != null) TimerBtn.onClick.AddListener(TurnEnd);

        RefreshButtonUI();
    }
    private void OnEnable()
    {
        if (KeySettingM.Instance != null)
        {
            // 확실하게 이벤트 연결
            KeySettingM.Instance.OnTurnoverTriggered -= HandleTurnover; 
            KeySettingM.Instance.OnTurnoverTriggered += HandleTurnover;

            KeySettingM.Instance.OnGiveUpTriggered -= HandleGiveUp;
            KeySettingM.Instance.OnGiveUpTriggered += HandleGiveUp;
        }
    }

    private void OnDisable()
    {
        if (KeySettingM.Instance != null)
        {
            KeySettingM.Instance.OnTurnoverTriggered -= HandleTurnover;
            KeySettingM.Instance.OnGiveUpTriggered -= HandleGiveUp;
        }
    }

    #region 준비단계에 필요한 것들
    [Header("준비단계에 필요한 것")]
    [SerializeField] private PieceDatabase PieceDatabase;
    public GameObject ReadyPanel_W;
    public GameObject ReadyPanel_B;
    public GameObject gamePanel;
    public Button ResetBtn_W;
    public Button ReadyBtn_W;
    public Button ResetBtn_B;
    public Button ReadyBtn_B;

  

    private PieceData? selectedPieceData = null;
    private bool isSpawnMode = false;
    private SinglePoolSpawner spawner;

    // 체스판 위에 존재하는 모든 기물 오브젝트를 논리 좌표(x, y)로 추적하기 위한 2차원 배열
    // (x: 0~7, y: 0~7) 위치에 존재하는 기물 GameObject를 저장합니다.
    public ChessPieceM[,] pieceGrid = new ChessPieceM[8, 8];

    [SerializeField] private List<PieceButtonUI> pieceButtonUIs;
    private Dictionary<pieceType, int> spawnedPieceCounts = new Dictionary<pieceType, int>(); // 기물을 종류별로 소환해서 실시간으로 개수제한 및 UI 변경용
    #region 타일 관련 함수
    /// <summary>
    /// 선택한 (x, y) 좌표가 해당 팀의 소환 가능 영역(앞쪽 2줄)인지 판별
    /// </summary>
    private bool IsValidSpawnZone(int x, int y, PieceColor pieceColor)
    {
        if (pieceColor == PieceColor.white)
        {
            // 하얀색 팀은 아래쪽 2줄 (y 인덱스 0, 1)만 허용
            return y >= 0 && y <= 1;
        }
        else
        {
            // 검은색 팀은 위쪽 2줄 (y 인덱스 6, 7)만 허용
            return y >= 6 && y <= 7;
        }
    }

    // 타일 색상을 전부 원래대로 돌리는 공통 함수
    private void ClearTileHighlights()
    {
        if (ScriptM.TryGet<ChessBoardM>(out ChessBoardM chessBoardM))
            for (int x = 0; x < chessBoardM.boardSize; x++)
            {
                for (int y = 0; y < chessBoardM.boardSize; y++)
                {
                    BoardTile tileComp = chessBoardM.spawnedTiles[x, y].GetComponent<BoardTile>();
                    if (tileComp != null)
                    {
                        tileComp.ResetColor(); // 본래의 체스판 무늬 색상으로 복구
                    }
                }
            }
    }

    // 타일 선택시 호출하는 함수
    public void OnTileClicked(int x, int y)
    {

        // 💡 1. [소환 모드 우선 처리] 우측 UI에서 기물 소환 버튼을 눌러 소환 모드인 경우
        if (isSpawnMode && selectedPieceData != null)
        {
            if (!IsValidSpawnZone(x, y, selectedPieceData.Value.Color))
            {
                GameObject prefabToSpawn = GetPrefabByData(selectedPieceData.Value);
                SpawnPiece(prefabToSpawn, x, y);

                // 카운트 증가 및 UI 갱신
                AddSpawnedCount(selectedPieceData.Value.Type);
                RefreshButtonUI();

                // 소환 모드 해제
                isSpawnMode = false;
                selectedPieceData = null;
                ClearTileHighlights();
            }
            else
            {
                GameObject prefabToSpawn = GetPrefabByData(selectedPieceData.Value);
                SpawnPiece(prefabToSpawn, x, y);

                // 카운트 증가 및 UI 갱신
                AddSpawnedCount(selectedPieceData.Value.Type);
                RefreshButtonUI();

                // 소환 모드 해제
                isSpawnMode = false;
                selectedPieceData = null;
                ClearTileHighlights();
            }    
                return;
        }

       


            // 💡 2. [기존 이동 모드] 소환 모드가 아닐 때의 체스 기물 선택 및 이동 로직
            if (selectedPos == null)
            {
                // 아직 아무것도 선택하지 않은 상태에서 내 기물을 클릭한 경우 -> 선택
                if (GetPieceAt(x, y) != null && IsMyTurnAndPiece(x, y))
                {
                    SelectPiece(x, y);
                }
            }
            else
            {
                Vector2Int targetPos = new Vector2Int(x, y);

                // 클릭한 칸이 이동 가능한 위치 중 하나라면 -> 네트워크 이동 또는 실제 이동 실행
                if (currentPossibleMoves.Contains(targetPos))
                {
                   MovePiece(selectedPos.Value.x, selectedPos.Value.y, x, y);
                   
                    
                    DeselectPiece();
                }
                // 다른 내 기물을 다시 클릭한 경우 -> 새로운 기물로 선택 교체
                else if (GetPieceAt(x, y) != null && IsMyTurnAndPiece(x, y))
                {
                    SelectPiece(x, y);
                }
                // 그 외 빈 공간이나 잘못된 위치 클릭 시 -> 선택 취소
                else
                {
                    DeselectPiece();
                }


            }
        
    }

    /// <summary>
    /// 보드의 모든 타일 색상을 원래 체스판 무늬(originalColor)로 되돌리는 공통 함수
    /// </summary>
    private void ClearAllTileColors()
    {
        if (ScriptM.TryGet<ChessBoardM>(out ChessBoardM service))
            for (int x = 0; x < service.boardSize; x++)
            {
                for (int y = 0; y < service.boardSize; y++)
                {
                    BoardTile tileComp = service.spawnedTiles[x, y].GetComponent<BoardTile>();
                    if (tileComp != null)
                    {
                        tileComp.ResetColor(); // 각 칸이 들고 있는 본래의 색상으로 복구
                    }
                }
            }
    }

    #endregion
    // 1. [UI 버튼에 연결] 기물 버튼을 누를 때 호출되는 함수
    public void OnClickPieceButton(PieceData pieceData)
    {
        // 남은 개수 제한 체크 (예: maxPiece 이상 소환했다면 리턴)
        int currentCount = GetSpawnedCount(pieceData.Type);
        if (currentCount >= pieceData.maxPiece)
        {
            Debug.Log($"{pieceData.pieceName}은(는) 더 이상 소환할 수 없습니다!");
            return;
        }

        selectedPieceData = pieceData;
        isSpawnMode = true;
        if (ScriptM.TryGet<ChessBoardM>(out ChessBoardM chessBoardM))

            // 💡 모든 타일을 돌며 내 진영 앞쪽 2줄만 초록색으로 변경!
            for (int x = 0; x < chessBoardM.boardSize; x++)
            {
                for (int y = 0; y < chessBoardM.boardSize; y++)
                {
                    bool isValid = (pieceData.Color == PieceColor.white) ? (y <= 1) : (y >= 6);

                    // spawnedTiles 배열에서 BoardTile 컴포넌트를 가져와 색상 변경
                    BoardTile tileComp = chessBoardM.spawnedTiles[x, y].GetComponent<BoardTile>();
                    if (tileComp != null)
                    {
                        if (isValid) tileComp.ChangeColor(Color.green); // 소환 가능 영역
                        else tileComp.ChangeColor(new Color(0.5f, 0.5f, 0.5f, 0.5f)); // 그외 영역 어둡게 처리 등
                    }
                }
            }
       
    }


    // 모든 버튼의 텍스트를 현재 남은 개수에 맞춰 갱신
    private void RefreshButtonUI()
    {
        foreach (var btnUI in pieceButtonUIs)
        {
            int max = btnUI.pieceData.maxPiece;
            int spawned = GetSpawnedCount(btnUI.pieceData.Type);
            int remaining = max - spawned;

            btnUI.UpdateCountUI(remaining);
        }
    }

    private int GetSpawnedCount(pieceType type)
    {
        if (!spawnedPieceCounts.ContainsKey(type)) return 0;
        return spawnedPieceCounts[type];
    }

    private void AddSpawnedCount(pieceType type)
    {
        if (!spawnedPieceCounts.ContainsKey(type))
            spawnedPieceCounts[type] = 0;

        spawnedPieceCounts[type]++;

    }

    private GameObject GetPrefabByData(PieceData data)
    {
        // 1. SO의 메서드 결과를 받아온 뒤 바로 리턴합니다!
        if (PieceDatabase != null)
        {
            return PieceDatabase.GetPrefab(data.Type, data.Color);
        }
        return null;
    }

    /// <summary>
    /// 원하는 위치(x, y)에 원하는 기물 프리팹을 생성합니다.
    /// (변형 체스 특성에 맞춰 개수나 종류 제한 없이 호출 가능)
    /// </summary>
    /// <param name="piecePrefab">생성할 기물 프리팹</param>
    /// <param name="x">Target Grid X (0~7)</param>
    /// <param name="y">Target Grid Y (0~7)</param>
    /// <returns>생성된 기물의 GameObject</returns>
    public ChessPieceM SpawnPiece(GameObject piecePrefab, int x, int y, PieceColor color = PieceColor.white)
    {

        if (!ScriptM.TryGet<ChessBoardM>(out var boardM))
        {
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
        GameObject newPieceObj = spawner.Spawn(piecePrefab, spawnWorldPos, Quaternion.identity);

        // 4. 생성된 오브젝트에서 ChessPieceM 컴포넌트 추출
        ChessPieceM newPiece = newPieceObj.GetComponent<ChessPieceM>();

        if (newPiece != null)
        {
            newPiece.data.Color = color;
            newPiece.isWhite = (color == PieceColor.white);

            newPiece.CurrentPos = new Vector2Int(x, y);
            if (newPiece is King)
            {
                OnSpawnKing(newPiece, newPiece.isWhite);
            }
        } activePieces.Add(newPiece);
        // 5. 2차원 배열에 기물 등록 및 반환
        pieceGrid[x, y] = newPiece;

        return newPiece;
    }

    // 킹 스폰 시점에 미리 할당해둠
    public void OnSpawnKing(ChessPieceM king, bool isWhite)
    {
        if (isWhite) whiteKing = king;
        else blackKing = king;
    }

    private void ReturnSpawnedCount(pieceType type)
    {
        // 딕셔너리에 해당 기물이 없거나 이미 0개 이면 무시
        if (!spawnedPieceCounts.ContainsKey(type)) return;

        spawnedPieceCounts[type]--;

        // 음수가 되지 않도록 방어 코드 추가
        if (spawnedPieceCounts[type] < 0)
        {
            spawnedPieceCounts[type] = 0;
        }
    }

    public void ResetAllPieces()
    {
        // 1. 8x8 보드를 전부 돌면서 기물이 있으면 파괴 함수 호출
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 2; y++)
            {
                
                DestroyPiece(x, y);
            }
        }

        // 2. UI 갱신은 모든 기물이 다 지워진 후 맨 마지막에 딱 한 번만 호출!
        RefreshButtonUI();

        // 3. 선택 상태 초기화
        DeselectPiece();
    }

    private void SetReady()
    {
        if (whiteKing == null)
        {
            Debug.Log("왕을 배치해야 게임을 시작할 수 있습니다!");
            return;
        }

        ReadyPanel_W.SetActive(false);
        ReadyPanel_B.SetActive(true);
    }
    private void SetReady_B()
    {
        if (blackKing == null)
        {
            Debug.Log("왕을 배치해야 게임을 시작할 수 있습니다!");
            return;
        }
        ReadyPanel_B.SetActive(false);
        gamePanel.SetActive(true);
        StartTurnTimer(PieceColor.white);
    }
    #endregion
   
    
    #region 게임중 필요한 것들

    #region 선언부
    [Header("게임 중 필요한 것")]
    //버튼들
    public Button SettingBtn;
    public Button TimerBtn;
    //프로모션 버튼들
    public Button QueenBtn_W;
    public Button KnightBtn_W;
    public Button BishopBtn_W;
    public Button RookBtn_W;
    public Button QueenBtn_B;
    public Button KnightBtn_B;
    public Button BishopBtn_B;
    public Button RookBtn_B;
    // 필요 패널
    public GameObject SettingPanel;
    public GameObject PromotionPanel_W;
    public GameObject PromotionPanel_B;
    [Header("타이머들")]
    [SerializeField] public PlayerTimerData whiteTimer;
    [SerializeField] public PlayerTimerData blackTimer;
    private CancellationTokenSource timerCts;
    private bool isTimerRunning = false;

    [Header("대사를 위한 텍스트")]
    public TextMeshProUGUI MyText;
    [Header("프로모션 관련 대기 상태")]
    private Vector2Int pendingPromotionPos;
    private ChessPieceM pendingPromotionPawn;
    private bool isWaitingForPromotion = false;

    // 왕의 위치를 찾기 위해 미리 변수 캐싱하기
    private ChessPieceM whiteKing;
    private ChessPieceM blackKing;

    //턴 관련 변수
    private bool isWhiteTurn = true;
    private PieceColor currentTurn = PieceColor.white;

    // 기물 움직이면 끝인 변수
    private bool isMovedone_W = false;
    private bool isMovedone_B = false;


    private Vector2Int? selectedPos = null; // 현재 선택된 기물의 좌표

    private List<Vector2Int> currentPossibleMoves = new List<Vector2Int>(); // 현재 이동 가능한 칸 표시
    private List<Vector2Int> attackMoves = new List<Vector2Int>(); // 공격이 가능한 부분 표시
    private List<MoveData> MoveHistory = new List<MoveData>(); //기보를 저장하기 위해서 있는 리스트
    public List<ChessPieceM> activePieces = new List<ChessPieceM>(); // 살아있는 말을 저장해서 찾는 연산량을 줄이기 위한 리스트
   
    

    #endregion

    public void HandleTurnover()
    {
        TurnEnd();
    }
  

    public void ActiveSelfSetting()
    {
        SettingPanel.SetActive(!SettingPanel.activeSelf);
    }
 

   

    private bool IsMyTurnAndPiece(int x, int y)
    {
        // 1. 매니저가 관리하는 2차원 배열(pieceGrid)에서 해당 위치의 기물 오브젝트를 가져옵니다.
        GameObject pieceObj = GetPieceAt(x, y);
        if (pieceObj == null) return false;

        // 2. 기물 오브젝트에서 ChessPieceM 컴포넌트를 가져옵니다.
        ChessPieceM piece = pieceObj.GetComponent<ChessPieceM>();
        if (piece == null) return false;

        // 3. 기물의 팀(색상)과 현재 진행 중인 턴(currentTurn)을 비교합니다.
        // (만약 기물이 isWhite 불리언을 쓴다면 아래와 같이 비교 가능)
        PieceColor pieceColor = piece.isWhite ? PieceColor.white : PieceColor.black;

        return pieceColor == currentTurn;
    }

    private void SelectPiece(int x, int y)
    {
        ChessPieceM pieceObj = pieceGrid[x, y];
        if (pieceObj == null) return;

        // 전에 선택한게 있으면 타일을 되돌리기 위해서 필요
        selectedPos = new Vector2Int(x, y);

        // 1. 이전 기물이 남겨둔 하이라이트/이동 경로 좌표를 깨끗이 비움
        DeselectPiece();

        // 이동을 위한 좌표 입력
        selectedPos = new Vector2Int(x, y);

        // 2. 현재 새로 선택한 pieceObj의 이동 가능 경로만 리스트에 쏙 채워 넣음
        pieceObj.GetPossibleMoves(pieceGrid, currentPossibleMoves);

        // 3. 새로 채워진 currentPossibleMoves 좌표에 하이라이트 켜기
        if (ScriptM.TryGet<ChessBoardM>(out ChessBoardM service))
        {
            // 💡 4. [선택된 기물 타일 하이라이트] 선택된 기물이 있는 칸은 파란색 등으로 표시
            BoardTile selectedTile = service.spawnedTiles[x, y].GetComponent<BoardTile>();
            if (selectedTile != null)
            {
                selectedTile.ChangeColor(Color.blue); // 선택된 자리는 파란색
            }
            foreach (Vector2Int targetPos in currentPossibleMoves)
            {
                BoardTile moveTile = service.spawnedTiles[targetPos.x, targetPos.y].GetComponent<BoardTile>();
                if (moveTile != null)
                {
                    moveTile.ChangeColor(Color.yellow); // 이동 가능 칸은 노란색
                }
            }
        }
    }

    private void DeselectPiece()
    {

        // 만약 선택된 기물이 있었다면, 그 주변에 칠했던 색상들을 원복
        if (selectedPos != null)
        {
            // 💡 1. 이동 가능했던 칸들과 선택된 칸을 포함하여 모든 보드 타일의 색상을 원래대로 원복
            ClearAllTileColors();
        }

        // 선택 상태 초기화
        selectedPos = null;
        currentPossibleMoves.Clear();
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
            DestroyPiece(toX, toY);
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
           1,
           1,
           capturedPiece
       );

        // movedata가 생성 후  기물의 currentpos 갱신
        pieceToMove.CurrentPos = move.To;

        MoveHistory.Add(move);

        // 폰이 실제로 이동을 마쳤을 때 실행되는 곳
        if (pieceToMove is Pawn pawn)
        {
            // 2칸을 이동한 경우 판정 (예: 출발 Y와 도착 Y의 차이가 2인 경우)
            if (Mathf.Abs(fromY - toY) == 2)
            {
                pawn.movedTwoSquaresLastTurn = true;
            }
            else
            {
                pawn.movedTwoSquaresLastTurn = false;
            }

            // 첫 이동이 끝났으므로 이후부터는 2칸 전진 불가 처리
            pawn.isFirstMove = false;
        }

        // 킹이 실제로 이동을 마쳤을 때 (캐슬링 처리 포함)
        if (pieceToMove is King king)
        {
            king.hasMoved = true;

            // 2칸 이동했다면 캐슬링 실행
            if (Mathf.Abs(fromX - toX) == 2)
            {
                int y = fromY;
                if (toX > fromX) // 우측 캐슬링
                {
                    ChessPieceM rook = pieceGrid[7, y];
                    if (rook != null)
                    {
                        pieceGrid[7, y] = null;
                        pieceGrid[5, y] = rook;
                        rook.CurrentPos = new Vector2Int(5, y);
                        rook.transform.position = boardM.GetWorldPosition(5, y);
                        if (rook is Rook r) r.hasMoved = true;
                    }
                }
                else // 좌측 캐슬링
                {
                    ChessPieceM rook = pieceGrid[0, y];
                    if (rook != null)
                    {
                        pieceGrid[0, y] = null;
                        pieceGrid[3, y] = rook;
                        rook.CurrentPos = new Vector2Int(3, y);
                        rook.transform.position = boardM.GetWorldPosition(3, y);
                        if (rook is Rook r) r.hasMoved = true;
                    }
                }
            }
        }
        else if (pieceToMove is Rook rookPiece)
        {
            rookPiece.hasMoved = true;
        }

        // 방금 움직인 기물이 흰색이면 플레이어의 턴(행동)입니다!
        bool isMyTurn = pieceToMove.isWhite;

    }

    /// <summary>
    /// 특정 위치의 기물을 제거(파괴/포획)합니다.
    /// </summary>
    public void DestroyPiece(int x, int y)
    {
        // 💡 해당 좌표에 기물이 실제로 존재할 때만 제거 실행
        if (pieceGrid[x, y] != null)
        {
            ChessPieceM pieceToDestroy = pieceGrid[x, y];

            ReturnSpawnedCount(pieceToDestroy.data.Type);

            activePieces.Remove(pieceGrid[x, y]);

            bool isMyPiece = (pieceToDestroy.data.Color == PieceColor.white); // 플레이어 색상 기준
           

            // spawner로 반환
            spawner.Despawn(pieceGrid[x, y].gameObject);

            // 배열 비워주기
            pieceGrid[x, y] = null;

            RefreshButtonUI();

            if (isSpawnMode == false)
            {
                if (pieceToDestroy.data.Type == pieceType.King)
                {
                    if (pieceToDestroy.data.Color == PieceColor.white)
                    {
                        whiteKing = null;
                        ShowGameResultAsync(GameResultType.Lose).Forget();
                    }
                    else if (pieceToDestroy.data.Color == PieceColor.black)
                    {
                        blackKing = null;
                        ShowGameResultAsync(GameResultType.Win).Forget();
                    }
                }
            }
        }
    }

    /// <summary>
    /// 특정 위치에 어떤 기물이 있는지 조회합니다.
    /// </summary>
    public GameObject GetPieceAt(int x, int y)
    {
        if (pieceGrid[x, y] == null)
        {
            return null;
        }
        return pieceGrid[x, y].gameObject;
    }
    public void TurnEnd()
    {
        isWhiteTurn = !isWhiteTurn;
        currentTurn = isWhiteTurn ? PieceColor.white : PieceColor.black;

        if (isWhiteTurn)
        {
            StartTurnTimer(PieceColor.white);
        }
        else
        {
            StartTurnTimer(PieceColor.black);
        }
    }

    #region 협상관련  함수들

    // 💡 플레이어가 '무르기 협상' 버튼을 눌렀을 때 호출할 함수
    public void TryUndoWithNegotiation()
    {
        if (MoveHistory.Count == 0)
        {
            return;
        }
            
         int lastIndex = MoveHistory.Count - 1;
         MoveData lastMove = MoveHistory[lastIndex];

         ExecuteUndoLastMove(lastMove, lastIndex);
    }
    // 기존에 올리셨던 무르기 코드의 실제 실행부 분리
    private void ExecuteUndoLastMove(MoveData lastMove, int lastIndex)
    {
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

            // 만약 activePieces 리스트 관리 중이라면 다시 추가
            if (!activePieces.Contains(lastMove.CapturedPiece))
            {
                activePieces.Add(lastMove.CapturedPiece);
            }
        }
        else
        {
            pieceGrid[lastMove.To.x, lastMove.To.y] = null;
        }
        if (lastMove.IsPromotion)
        {
            // 1. 프로모션으로 생겼던 새 기물 파괴
            DestroyPiece(lastMove.To.x, lastMove.To.y);

            // 2. 원래 폰 다시 스폰
            GameObject pawnPrefab = GetPrefabByData(lastMove.MovedPiece.data);
            SpawnPiece(pawnPrefab, lastMove.From.x, lastMove.From.y);
           
        }
    }

    // 프로모션 할때 사용
    public void RequestPromotion(ChessPieceM pawn, int x, int y, bool isBlack)
    {
        if (isBlack)
        {
            pendingPromotionPawn = pawn;
            pendingPromotionPos = new Vector2Int(x, y);
            isWaitingForPromotion = true;
            // 승진 기물(여왕, 룩, 비숍, 나이트 등)을 고를 수 있는 UI 팝업 오픈
            if (PromotionPanel_B != null)
            {
                PromotionPanel_B.SetActive(true);
            }
        }
        else
        {
            pendingPromotionPawn = pawn;
            pendingPromotionPos = new Vector2Int(x, y);
            isWaitingForPromotion = true;

            // 승진 기물(여왕, 룩, 비숍, 나이트 등)을 고를 수 있는 UI 팝업 오픈
            if (PromotionPanel_W != null)
            {
                PromotionPanel_W.SetActive(true);
            }
        }
    }
    public void OnPromotionPieceSelected(PieceDatabase piecedatabase, pieceType selectedType)
    {
        if (!isWaitingForPromotion || pendingPromotionPawn == null) return;

        int x = pendingPromotionPos.x;
        int y = pendingPromotionPos.y;

        // 기존 폰의 코스트와 팀 정보 백업
        int oldCost = pendingPromotionPawn.data.cost; // 폰의 기존 코스트
        PieceColor pieceColor = pendingPromotionPawn.data.Color; // 백인지 흑인지 팀 컬러 확인
        bool isMyPiece = (pieceColor == PieceColor.white);

        int pawnX = pendingPromotionPawn.CurrentPos.x;
        int pawnY = pendingPromotionPawn.CurrentPos.y;

        // 기존 폰 제거 (만들어두신 DestroyPiece 재활용)
        DestroyPiece(pawnX, pawnY);

        //PieceDatabase를 이용해 선택한 기물(Queen, Knight 등)의 프리팹 가져오기
        GameObject selectedPrefab = piecedatabase.GetPrefab(selectedType, pieceColor);

        //새 기물 스폰 (이미 만들어두신 SpawnPiece 활용)
        ChessPieceM newPiece = SpawnPiece(selectedPrefab, x, y);
        int newCost = newPiece.data.cost; // 새로 바뀐 기물의 코스트

        //  기보(MoveData)에 프로모션 기록 추가
        MoveData move = new MoveData(
            new Vector2Int(pendingPromotionPawn.CurrentPos.x, pendingPromotionPawn.CurrentPos.y),
            new Vector2Int(x, y),
            pendingPromotionPawn, // 무르기 때 원래 폰을 복원하기 위해 기존 폰 저장
            oldCost,
            newCost,
            null
        );
        move.IsPromotion = true; // 프로모션 플래그 설정
        MoveHistory.Add(move);

        //  UI 닫기 및 상태 초기화
        if (PromotionPanel_W.gameObject == true)
        {
            PromotionPanel_W.SetActive(false);
        }
        if (PromotionPanel_B.gameObject == true)
        {
            PromotionPanel_B.SetActive(false);
        }
        isWaitingForPromotion = false;
        pendingPromotionPawn = null;

       
    }

    #endregion

    #region 타이머 관련 함수들
    // 💡 내 턴이 시작될 때 호출 (이어서 시작)
    public void StartTurnTimer(PieceColor activeTurn)
    {
        currentTurn = activeTurn;

        // 기존에 작동 중인 타이머 정지
        PauseTurnTimer();

        timerCts = new CancellationTokenSource();
        RunTimerAsync(timerCts.Token).Forget();
    }

    // 💡 턴이 끝나거나 일시정지할 때 호출
    public void PauseTurnTimer()
    {
        if (!isTimerRunning) return;

        if (timerCts != null)
        {
            timerCts.Cancel();
            timerCts.Dispose();
            timerCts = null;
        }
        isTimerRunning = false;
    }

    // 비동기 타이머 루프
    private async UniTaskVoid RunTimerAsync(CancellationToken cancellationToken)
    {
        isTimerRunning = true;

        // 현재 턴에 해당하는 타이머 데이터 가져오기
        PlayerTimerData activeTimerData = (currentTurn == PieceColor.white) ? whiteTimer : blackTimer;

        if (activeTimerData.TimerTime <= 5)
        {
            activeTimerData.TimerTime = 5f;
        }

        try
        {
            while (activeTimerData.TimerTime > 0)
            {
                // 1초 대기
                await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: cancellationToken);

                // 프로퍼티 세터를 통해 시간이 감가되고 UI가 즉시 자동 업데이트됨!
                activeTimerData.TimerTime -= 1f;
            }

            OnTimeOut();
        }
        catch (OperationCanceledException)
        {
            // Pause 시 취소 예외 발생하므로 정상 무시
        }
    }

    private void OnTimeOut()
    {
        isTimerRunning = false;
        TurnEnd();

    }
    #endregion
    #endregion

    #region 게임 끝날 때 필요한 것들
    [Header("게임 끝날 때 필요한 것들")]
    public GameObject ResultPanel;
    public TextMeshProUGUI ResultText;
    public TextMeshProUGUI NegoText;
    public TextMeshProUGUI TimeText;
   

    // 보드 상태별 반복 횟수를 저장하는 딕셔너리
    private Dictionary<string, int> positionHistory = new Dictionary<string, int>();

    public void HandleGiveUp()
    {
        ShowGameResultAsync(GameResultType.Giveup).Forget();
    }
    /// <summary>
    /// 게임 종료 결과 패널 출력 및 씬 이동 처리 (UniTask 비동기)
    /// </summary>
    public async UniTaskVoid ShowGameResultAsync(GameResultType resultType)
    {
        if (ResultPanel != null)
        {
            ResultPanel.SetActive(true);
        }

   
        string resultTextStr = "";

        // 결과 유형에 따른 분기 처리
        switch (resultType)
        {
            case GameResultType.Win:
              
                resultTextStr = " 백 승리";

                if (NegoText != null) NegoText.text = $"백 플레이어 승리";
                if (TimeText != null) TimeText.text = $"흑은 노력하세요^^";

                break;

            case GameResultType.Lose:
              
                resultTextStr = "흑 승리";

                if (NegoText != null) NegoText.text = $"흑 플레이어 승리";
                if (TimeText != null) TimeText.text = $"백은 노력하세요^^";
                break;

            case GameResultType.Giveup:
                resultTextStr = "항복";
                
                if (NegoText != null) NegoText.text = "대국을 포기했습니다.";
                if (TimeText != null) TimeText.text = "곧 있다가 로비로 이동합니다.";
                break;

        }

        // UI 텍스트 적용
        if (ResultText != null) ResultText.text = resultTextStr;
       

        // 5초 대기 후 로비 씬으로 이동
        await UniTask.Delay(System.TimeSpan.FromSeconds(5f), ignoreTimeScale: true);
       

        if (ScriptM.TryGet<ScenesM>(out ScenesM service))
        {
            ResultPanel.SetActive(false);
            ScriptM.ClearSceneLocalServices();
            service.LoadScene(SceneNumber.Lobby);
        }
    }
 
    #endregion
}
