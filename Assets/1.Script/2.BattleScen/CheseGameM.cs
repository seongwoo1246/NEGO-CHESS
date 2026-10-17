
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Debug = DebugM<CheseGameM>;

public class CheseGameM : MonoBehaviour
{
    [SerializeField] private PieceDatabase PieceDatabase;
    public GameObject ReadyPanel;
    public GameObject CharacterPanel;
    public GameObject gamePanel;
    [Header("버튼들")]
    public Button ResetBtn;
    public Button ReadyBtn;


    private int currentCost;

    private PieceData? selectedPieceData = null;
    private bool isSpawnMode = false;


    private SinglePoolSpawner spawner;

    // 왕의 위치를 찾기 위해 미리 변수 캐싱하기
    private ChessPieceM whiteKing;
    private ChessPieceM blackKing;

    // 체스판 위에 존재하는 모든 기물 오브젝트를 논리 좌표(x, y)로 추적하기 위한 2차원 배열
    // (x: 0~7, y: 0~7) 위치에 존재하는 기물 GameObject를 저장합니다.
    public ChessPieceM[,] pieceGrid = new ChessPieceM[8, 8];

    public PieceColor currentTurn = PieceColor.white;

    private Vector2Int? selectedPos = null; // 현재 선택된 기물의 좌표

    private List<Vector2Int> currentPossibleMoves = new List<Vector2Int>(); // 현재 이동 가능한 칸 표시
    private List<Vector2Int> attackMoves = new List<Vector2Int>(); // 공격이 가능한 부분 표시
    private List<MoveData> MoveHistory = new List<MoveData>(); //기보를 저장하기 위해서 있는 리스트
    private List<ChessPieceM> activePieces = new List<ChessPieceM>(); // 살아있는 말을 저장해서 찾는 연산량을 줄이기 위한 리스트
    [SerializeField] private List<PieceButtonUI> pieceButtonUIs;
    private Dictionary<pieceType, int> spawnedPieceCounts = new Dictionary<pieceType, int>(); // 기물을 종류별로 소환해서 실시간으로 개수제한 및 UI 변경용

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
        if(ResetBtn !=  null) { ResetBtn.onClick.AddListener(ResetAllPieces); }
        if(ReadyBtn !=  null) { ReadyBtn.onClick.AddListener(SetReady); }

        RefreshButtonUI();
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<CheseGameM>();
    }

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
      if(ScriptM.TryGet<ChessBoardM>(out ChessBoardM chessBoardM))

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


    private void SelectPiece(int x, int y)
    {
        Debug.Log("말 선택함");

       
        ChessPieceM pieceObj = pieceGrid[x, y];
        if (pieceObj == null) return;

        // 1. 이전 기물이 남겨둔 하이라이트/이동 경로 좌표를 깨끗이 비움
        DeselectPiece();

        // 2. 현재 새로 선택한 pieceObj의 이동 가능 경로만 리스트에 쏙 채워 넣음
        pieceObj.GetPossibleMoves(pieceGrid, currentPossibleMoves);

        // 3. 새로 채워진 currentPossibleMoves 좌표에 하이라이트 켜기
        if(ScriptM.TryGet<ChessBoardM>(out ChessBoardM service))
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

        if(singlePlayer.maxCost>=currentCost)
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
            DestroyPiece(toX,toY);
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

        // movedata가 생성 후  기물의 currentpos 갱신
        pieceToMove.CurrentPos = move.To; 

        MoveHistory.Add(move);
    }

    // 킹 스폰 시점에 미리 할당해둠
    public void OnSpawnKing(ChessPieceM king, bool isWhite)
    {
        if (isWhite) whiteKing = king;
        else blackKing = king;
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
        }
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

    /// <summary>
    /// 특정 위치에 어떤 기물이 있는지 조회합니다.
    /// </summary>
    public GameObject GetPieceAt(int x, int y)
    {
        if (pieceGrid[x, y] ==null)
        {
            return null;
        }
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
    }
}
