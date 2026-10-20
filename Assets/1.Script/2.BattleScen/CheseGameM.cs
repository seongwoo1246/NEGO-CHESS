using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CancellationToken = System.Threading.CancellationToken;
using Debug = DebugM<CheseGameM>;


[System.Serializable]
public class PlayerTimerData
{
    [SerializeField] private TextMeshProUGUI timerText;
    private float timerTime = 300f; // 기본 5분(300초)

    // 프로퍼티를 통해 값이 변경될 때 UI도 자동으로 갱신하도록 구성
    public float TimerTime
    {
        get => timerTime;
        set
        {
            timerTime = Mathf.Max(0, value); // 음수 방지
            UpdateUI();
        }
    }

    public void UpdateUI()
    {
        if (timerText != null)
        {
            // 분:초 (MM:SS) 포맷으로 변환해 표시
            int minutes = Mathf.FloorToInt(timerTime / 60f);
            int seconds = Mathf.FloorToInt(timerTime % 60f);
            timerText.text = $"{minutes:D2}:{seconds:D2}";
        }
    }
}


public class CheseGameM : MonoBehaviour
{
    private void Awake()
    {
        ScriptM.Register<CheseGameM>(this, UseSpace.Local);
    }

    private void Start()
    {
        if (ScriptM.TryGet<SinglePoolSpawner>(out SinglePoolSpawner service))
        {
            spawner = service;
        }
        if (ResetBtn != null) { ResetBtn.onClick.AddListener(ResetAllPieces); }
        if (ReadyBtn != null) { ReadyBtn.onClick.AddListener(SetReady); }
        if (SettingBtn != null) SettingBtn.onClick.AddListener(ActiveSelfSetting);
        if (NegoBtn != null) NegoBtn.onClick.AddListener(ActiveSelfNegoPanel);
        if (enPassantBtn != null) enPassantBtn.onClick.AddListener(()=> TryNegotiateRule(0));
        if (CastlingBtn != null) CastlingBtn.onClick.AddListener(()=> TryNegotiateRule(1));
        if (PromotionBtn != null) PromotionBtn.onClick.AddListener(()=> TryNegotiateRule(2));
        if (UndoBtn != null) UndoBtn.onClick.AddListener(() => TryNegotiateRule(3));
        if (QueenBtn != null) QueenBtn.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase,pieceType.Queen));
        if (KnightBtn != null) KnightBtn.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase,pieceType.Knight));
        if (BishopBtn != null) BishopBtn.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase,pieceType.Bishop));
        if (RookBtn != null) RookBtn.onClick.AddListener(() => OnPromotionPieceSelected(PieceDatabase,pieceType.Rook));

        RefreshButtonUI();
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<CheseGameM>();
    }

    #region 준비단계에 필요한 것들
    [Header("준비단계에 필요한 것")]
    [SerializeField] private PieceDatabase PieceDatabase;
    public GameObject ReadyPanel;
    public GameObject CharacterPanel;
    public GameObject gamePanel;
    public Button ResetBtn;
    public Button ReadyBtn;

    private int currentCost;

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
        Debug.Log("보드 클릭 함수 호출");

        // 💡 1. [소환 모드 우선 처리] 우측 UI에서 기물 소환 버튼을 눌러 소환 모드인 경우
        if (isSpawnMode && selectedPieceData != null)
        {
            if (!IsValidSpawnZone(x, y, selectedPieceData.Value.Color))
            {
                Debug.Log("자신의 진영(앞쪽 2줄)에만 기물을 소환할 수 있습니다!");
                return;
            }
            // 소환 실행
            GameObject prefabToSpawn = GetPrefabByData(selectedPieceData.Value);
            SpawnPiece(prefabToSpawn, x, y);

            // 카운트 증가 및 UI 갱신
            AddSpawnedCount(selectedPieceData.Value.Type);
            RefreshButtonUI();

            // 소환 모드 해제
            isSpawnMode = false;
            selectedPieceData = null;
            ClearTileHighlights();
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
                if (ScriptM.TryGet<NetworkChessPieceM>(out var netPieceM))
                {
                    netPieceM.RPC_RequestMove(selectedPos.Value.x, selectedPos.Value.y, x, y);
                }
                else
                {
                    MovePiece(selectedPos.Value.x, selectedPos.Value.y, x, y);
                }
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
        Debug.Log($"{pieceData.pieceName} 선택됨! 타일을 누르세요.");
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

        Debug.LogError("PieceDatabase가 연결되지 않았습니다!");
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
    public ChessPieceM SpawnPiece(GameObject piecePrefab, int x, int y)
    {

        if (!ScriptM.TryGet<ChessBoardM>(out var boardM))
        {
            Debug.LogError("ChessBoardM 서비스를 찾을 수 없습니다.");
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

        if (singlePlayer.maxCost >= currentCost)
        {
            currentCost += newPiece.data.cost;
        }

        // 4.5 기물의 위치를 스스로 저장하게 한다.
        if (newPiece != null)
        {
            newPiece.CurrentPos = new Vector2Int(x, y);
            if (newPiece is King)
            {
                OnSpawnKing(newPiece, newPiece.isWhite);
            }
        }



        activePieces.Add(newPiece);


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
                // 내부에서 이미 null 체크와 카운트 복구, despan이 다 처리됨
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

        ReadyPanel.SetActive(false);
        CharacterPanel.SetActive(true);
        gamePanel.SetActive(true);

        StartTurnTimer(PieceColor.white);

    }
    #endregion

    #region 게임중 필요한 것들
    [Header("게임 중 필요한 것")]
    public Button SettingBtn;
    public Button ProvokeBtn;
    public Button NegoBtn;
    //협상 버튼들
    public Button enPassantBtn;
    public Button CastlingBtn;
    public Button PromotionBtn;
    public Button UndoBtn;
    //프로모션 버튼들
    public Button QueenBtn;
    public Button KnightBtn;
    public Button BishopBtn;
    public Button RookBtn;

    public TextMeshProUGUI enPassantText;
    public TextMeshProUGUI CastlingText;
    public TextMeshProUGUI PromotionText;
    public TextMeshProUGUI UndoText;


    public GameObject SettingPanel;
    public GameObject NegoPanel;
    public GameObject PromotionPanel;

    [Header("협상으로 금지된 특수 룰 상태")]
    public bool canUseEnPassant = true;   // 앙파상 가능 여부 (협상 성공 시 false로 변경)
    public bool canUseCastling = true;    // 캐슬링 가능 여부 (협상 성공 시 false로 변경)
    public bool canUsePromotion = true;   // 프로모션 가능 여부 (협상 성공 시 false로 변경)

    [Header("타이머들")]
    [SerializeField] private PlayerTimerData whiteTimer;
    [SerializeField] private PlayerTimerData blackTimer;
    private CancellationTokenSource timerCts;
    private bool isTimerRunning = false;

    [Header("대사를 위한 텍스트")]
    public TextMeshProUGUI CharacterText;
    public TextMeshProUGUI MyText;

    [Header("프로모션 관련 대기 상태")]
    private Vector2Int pendingPromotionPos;
    private ChessPieceM pendingPromotionPawn;
    private bool isWaitingForPromotion = false;

    // 협상과 도발 관련 변수
    private float characterNegoPower = 50f;
    private float myNegoPower = 50f;


    // 왕의 위치를 찾기 위해 미리 변수 캐싱하기
    private ChessPieceM whiteKing;
    private ChessPieceM blackKing;
    

    public PieceColor currentTurn = PieceColor.white;

    private Vector2Int? selectedPos = null; // 현재 선택된 기물의 좌표

    private List<Vector2Int> currentPossibleMoves = new List<Vector2Int>(); // 현재 이동 가능한 칸 표시
    private List<Vector2Int> attackMoves = new List<Vector2Int>(); // 공격이 가능한 부분 표시
    private List<MoveData> MoveHistory = new List<MoveData>(); //기보를 저장하기 위해서 있는 리스트
    public List<ChessPieceM> activePieces = new List<ChessPieceM>(); // 살아있는 말을 저장해서 찾는 연산량을 줄이기 위한 리스트

    public void ActiveSelfSetting()
    {
        SettingPanel.SetActive(!SettingPanel.activeSelf);
    }
    public void ActiveSelfNegoPanel()
    {
      float successChance = NegoSuccessChance();

        NegoPanel.SetActive(!NegoPanel.activeSelf);
        enPassantText.text = $"앙파상 제한 = {successChance:F0}%";
        CastlingText.text = $"캐슬링 제한 = {successChance:F0}%";
        PromotionText.text = $"프로모션 제한 = {successChance:F0}%";
        UndoText.text = $"한 수 무르기 = {successChance:F0}%";
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
        Debug.Log("말 선택함");


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
        Debug.Log("이동 완료");
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

            currentCost -= pieceToDestroy.data.cost;

            ReturnSpawnedCount(pieceToDestroy.data.Type);

            activePieces.Remove(pieceGrid[x, y]);

            // spawner로 반환
            spawner.Despawn(pieceGrid[x, y].gameObject);

            // 배열 비워주기
            pieceGrid[x, y] = null;

            RefreshButtonUI();

            if (pieceToDestroy.data.Type == pieceType.King)
            {
                if (pieceToDestroy.data.Color == PieceColor.white)
                {
                    whiteKing = null;
                }
                else if (pieceToDestroy.data.Color == PieceColor.black)
                {
                    blackKing = null;
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

    // 특정 팀(isWhiteTarget)의 킹이 현재 체크 상태인지 확인하는 함수
    public bool IsKingInCheck(bool isWhiteTarget, ChessPieceM[,] grid)
    {
        // 1. target 팀의 킹 위치 찾기
        Vector2Int kingPos = FindKingPosition(isWhiteTarget);

        // 2. 상대방(적)의 모든 기물이 이동/공격할 수 있는 좌표들을 모음
        List<Vector2Int> enemyAttackMoves = GetAllEnemyAttackMoves(!isWhiteTarget, grid);

        // 3. 적의 공격 가능 범위에 킹의 위치가 포함되어 있다면 체크!
        return enemyAttackMoves.Contains(kingPos);
    }

    // 1. target 팀의 킹 위치 찾기
    private Vector2Int FindKingPosition(bool isWhiteTarget)
    {
        ChessPieceM targetKing = isWhiteTarget ? whiteKing : blackKing;

        if (targetKing != null)
        {
            return targetKing.CurrentPos; // 즉시 위치 반환
        }

        return new Vector2Int(-1, -1);
    }

    // 2. 적 팀의 모든 기물이 이동/공격할 수 있는 좌표들을 모음
    private List<Vector2Int> GetAllEnemyAttackMoves(bool isWhiteEnemy, ChessPieceM[,] grid)
    {
        attackMoves.Clear(); // 매번 new 하지 않고 기존 리스트 재활용

        for (int i = 0; i < activePieces.Count; i++)
        {
            var piece = activePieces[i];
            if (piece != null && piece.isWhite == isWhiteEnemy)
            {
                // 리스트 생성 없이, attackMoves에 직접 경로를 추가하도록 인자로 전달
                piece.GetPossibleMoves(grid, attackMoves);
            }
        }

        return attackMoves;
    }

    #region 협상관련  함수들
    // UI 버튼에 각각 연결할 함수
    public void TryNegotiateRule(int ruleTypeInt)
    {
        NegotiationType ruleType = (NegotiationType)ruleTypeInt;

        bool isNegotiationSuccess = TryNegotiate();

        if (isNegotiationSuccess)
        {
            Debug.Log($"협상 성공! [{ruleType}] 규칙이 금지(봉쇄)됩니다.");

            // 2. 성공 시 해당 룰을 비활성화(막아버림)
            ApplyRuleBan(ruleType);

           
        }
        else
        {
            // 3. 실패 시 페널티 (예: 협상력 10% 감소)
            myNegoPower -= 10f;
            ClampNegoPower();
            Debug.Log($"협상 실패... 페널티로 협상력이 10% 감소했습니다. (현재: {myNegoPower}%)");
        }
    }
    // 규칙을 실제로 차단하는 함수
    private void ApplyRuleBan(NegotiationType ruleType)
    {
        switch (ruleType)
        {
            case NegotiationType.EnPassant:
                canUseEnPassant = false;
                break;
            case NegotiationType.Castling:
                canUseCastling = false;
                break;
            case NegotiationType.Promotion:
                canUsePromotion = false;
                break;
            case NegotiationType.Undo:
                TryUndoWithNegotiation();
                break;
        }
    }

    // 💡 플레이어가 '무르기 협상' 버튼을 눌렀을 때 호출할 함수
    public void TryUndoWithNegotiation()
    {
        // 1. 무를 기보가 아예 없다면 중단
        if (MoveHistory.Count == 0)
        {
            Debug.Log("무를 수 있는 기보가 없습니다.");
            return;
        }

        // 2. 협상 성공 확률 계산 (예시: 내 협상력 vs 상대 협상력)
        bool isNegotiationSuccess = TryNegotiate();

        if (isNegotiationSuccess)
        {
            Debug.Log("무르기 협상 성공! 기물을 되돌리고 협상력을 원상복귀합니다.");

            // 3. 가장 마지막 기보 가져오기 (실제 되돌리기 전 데이터 확인용)
            int lastIndex = MoveHistory.Count - 1;
            MoveData lastMove = MoveHistory[lastIndex];

            // 4. 협상력 원상복귀 처리 (잡힌 기물이나 프로모션으로 변동된 수치 되돌리기)
            RevertNegotiationPowerForMove(lastMove, isMyMove: true); // 내 턴의 무르기라 가정

            // 5. 기존에 만들어두신 완벽한 무르기 실행
            ExecuteUndoLastMove(lastMove, lastIndex);
        }
      
    }

   

    // 💡 해당 수가 진행될 때 변동되었던 협상력을 '원상복귀' 시키는 함수
    private void RevertNegotiationPowerForMove(MoveData move, bool isMyMove)
    {
        // A. 만약 이 수에서 적의 기물이 잡혔었다면? 
        // (당시에 상대 협상력이 잡힌 기물 코스트만큼 깎였었으므로 다시 돌려줌)
        if (move.CapturedPiece != null)
        {
            int capturedCost = move.CapturedPiece.data.cost;
            if (isMyMove)
            {
                // 내가 상대 말을 잡았어서 상대 협상력이 깎였던 걸 복구 (상대 협상력 증가)
                characterNegoPower += capturedCost;
            }
            else
            {
                myNegoPower += capturedCost;
            }
        }

        // B. 만약 이 수에서 프로모션(승진)이 일어났었다면?
        // (만약 MoveData에 프로모션 전후 코스트나 플래그가 기록되어 있다면 차이만큼 되돌림)
        
        if (move.IsPromotion)
        {
            int costDiff = move.newCost - move.oldCost;
            if (isMyMove) myNegoPower -= costDiff; // 승진으로 올랐던 협상력을 다시 깎음
            else characterNegoPower -= costDiff;
        }


        // 범위 제한 (0 ~ 100)
        ClampNegoPower();
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

            // 3. 프로모션으로 얻었던 협상력 원상복귀 (증가했던 차이만큼 차감)
            int costDiff = lastMove.newCost - lastMove.oldCost; 
            myNegoPower -= costDiff;
            ClampNegoPower();
        }
    }

    // 1. 도발 성공 시 호출 (도발한 대상의 협상력 5% 감소)
    public void OnTauntSuccess(bool isMyTeam)
    {
        if (isMyTeam)
        {
            myNegoPower -= 5f;
        }
        else
        {
            characterNegoPower -= 5f;
        }

        // 협상력이 0 미만이나 100을 넘지 않도록 안전하게 조절 (선택사항)
        ClampNegoPower();
    }

    // 2. 말이 잡혔을 때 호출 (잡힌 말의 코스트만큼 협상력 감소)
    public void OnPieceDestroyed(bool isMyPiece, int pieceCost)
    {
        if (isMyPiece)
        {
            myNegoPower -= pieceCost; // 내 말이 잡히면 내 협상력 감소
        }
        else
        {
            characterNegoPower -= pieceCost; // 상대 말이 잡히면 상대 협상력 감소
        }

        ClampNegoPower();
    }

    // 3. 프로모션(승진) 시 호출 (기존 코스트와 새 코스트의 차이만큼 증가)
    public void OnPiecePromoted(bool isMyPiece, int oldCost, int newCost)
    {
        int costDifference = newCost - oldCost; // 예: 10 - 1 = 9

        if (isMyPiece)
        {
            myNegoPower += costDifference; // 승진하면 협상력 회복/증가 (+9%)
        }
        else
        {
            characterNegoPower += costDifference;
        }

        ClampNegoPower();
    }
    public void RequestPromotion(ChessPieceM pawn, int x, int y)
    {
        pendingPromotionPawn = pawn;
        pendingPromotionPos = new Vector2Int(x, y);
        isWaitingForPromotion = true;

        // 승진 기물(여왕, 룩, 비숍, 나이트 등)을 고를 수 있는 UI 팝업 오픈
        if (PromotionPanel != null)
        {
            PromotionPanel.SetActive(true);
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

        // 협상력 변동 처리 (코스트 차이만큼 반영)
        OnPiecePromoted(isMyPiece, oldCost, newCost);

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
        if (PromotionPanel != null)
        {
            PromotionPanel.SetActive(false);
        }
        isWaitingForPromotion = false;
        pendingPromotionPawn = null;

        Debug.Log($"프로모션 성공! 코스트 차이({newCost - oldCost})만큼 협상력이 반영되었습니다.");
    }

    // 4. 협상력 범위를 0 ~ 100 사이로 제한하는 방어 코드
    private void ClampNegoPower()
    {
        myNegoPower = Mathf.Clamp(myNegoPower, 0f, 100f);
        characterNegoPower = Mathf.Clamp(characterNegoPower, 0f, 100f);
    }

    // 5. 협상 시도 시 성공 확률 계산 
    public bool TryNegotiate()
    {
        // 예: 내 협상력과 상대 협상력의 차이를 기반으로 성공 확률 계산
        // 내 협상력이 높을수록 확률이 높아지도록 설정 가능
        float successChance = NegoSuccessChance();

        float randomValue = UnityEngine.Random.Range(0f, 100f);

        if (randomValue <= successChance)
        {
            Debug.Log($"협상 성공! (성공 확률: {successChance}%, 나온 값: {randomValue:F1})");
            return true;
        }
        else
        {
            Debug.Log($"협상 실패... (성공 확률: {successChance}%, 나온 값: {randomValue:F1})");
            return false;
        }
    }
    private float NegoSuccessChance()
    {
        float successChance = 10f + (myNegoPower - characterNegoPower);
        return Mathf.Clamp(successChance, 10f, 90f); // 최소 10%, 최대 90% 보장
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

        try
        {
            while (activeTimerData.TimerTime > 0)
            {
                // 1초 대기
                await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: cancellationToken);

                // 프로퍼티 세터를 통해 시간이 감가되고 UI가 즉시 자동 업데이트됨!
                activeTimerData.TimerTime -= 1f;
            }

            // 시간이 다 되었을 때
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
        Debug.Log("제한 시간 초과! 자동으로 턴을 넘깁니다.");
        // TODO: 턴 강제 종료 및 상대방에게 턴 넘기는 로직 호출 후 5초로 재 설정
    }
    #endregion

    #region 대사 관련 함수
    public void TriggerCharacterInteraction(CharacterType characterType, bool isBattle)
    {
        if (ScriptM.TryGet<DialogueController>(out DialogueController service))
        {
            // 1. 매니저에게 현재 플레이어 정보와 캐릭터에 맞는 대사를 달라고 요청
            string dialogue = service.GetMatchingDialogue(characterType, isBattle);

            // 2. DOTween을 이용해 화면에 부드럽게 출력
            service.ShowDialogue(dialogue);
        }
    }

    #endregion

    #endregion






}
