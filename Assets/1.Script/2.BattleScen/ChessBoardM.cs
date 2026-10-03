using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;


/// <summary>
/// 체스판을 세팅하기 위해서 만든 스크립트 
/// (월드 좌표 및 그리드 좌표는 여기서 구할 예정)
/// </summary>
public class ChessBoardM : MonoBehaviour
{
    [Header("기준점 및 타일 설정")]
    [SerializeField] private Transform originPoint; 
    [SerializeField] private float tileSize =1f;

    // 멀티시 클라이언트가 흑이면 true 싱글플레이나 백일 경우는 false로 둔다.
    public bool IsFlipped { get; set; } = false;


    [Header("선택 및 입력 상태")]
    private Vector2Int? selectedPos = null;
    private List<Vector2Int> currentPossibleMoves = new List<Vector2Int>();

    private void Awake()
    {
        ScriptM.Register<ChessBoardM>(this, UseSpace.Local);
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

        Debug.Log($"클릭한 좌표 {localX},{localY}");

        return new Vector2Int(localX, localY);
    }


    private void OnMouseDown()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int gridPos = GetGridIndex(mouseWorldPos); // 좌표 변환

        if (gridPos.x >= 0 && gridPos.x < 8 && gridPos.y >= 0 && gridPos.y < 8)
        {
            OnTileClicked(gridPos.x, gridPos.y); // ★ 여기서 호출!
        }
    }


    /// <summary>
    /// 마우스 클릭 등으로 보드의 특정 (x, y) 타일을 클릭했을 때 호출되는 함수
    /// </summary>
    public void OnTileClicked(int x, int y)
    {
        // 체스 게임 매니저 참조 가져오기 (이름은 실제 프로젝트의 매니저 스크립트에 맞게 변경)
        if (!ScriptM.TryGet<CheseGameM>(out var gameM)) return;

        // 1. 아직 기물을 선택하지 않은 상태
        if (selectedPos == null)
        {
            if (gameM.GetPieceAt(x, y) != null && IsMyTurnAndPiece(x, y, gameM))
            {
                SelectPiece(x, y, gameM);
            }
        }
        // 2. 이미 특정 기물을 선택한 상태
        else
        {
            Vector2Int targetPos = new Vector2Int(x, y);

            // ① 클릭한 곳이 이동 가능한 칸일 때 -> 네트워크 RPC 호출 또는 이동 실행
            if (currentPossibleMoves.Contains(targetPos))
            {
                if (ScriptM.TryGet<NetworkChessPieceM>(out var netPieceM))
                {
                    netPieceM.RPC_RequestMove(selectedPos.Value.x, selectedPos.Value.y, x, y);
                }

                DeselectPiece();
            }
            // ② 다른 내 기물을 클릭했을 때 -> 선택 교체
            else if (gameM.GetPieceAt(x, y) != null && IsMyTurnAndPiece(x, y, gameM))
            {
                SelectPiece(x, y, gameM);
            }
            // ③ 그 외 (빈 공간이나 이동 불가능한 칸) 클릭시 -> 선택 취소
            else
            {
                DeselectPiece();
            }
        }
    }

    private void SelectPiece(int x, int y, CheseGameM gameM)
    {
        GameObject pieceObj = gameM.GetPieceAt(x, y);
        if (pieceObj == null) return;

        selectedPos = new Vector2Int(x, y);

        // 선택한 기물로부터 이동 가능한 경로 목록 받아오기
        var piece = pieceObj.GetComponent<ChessPieceM>();
        if (piece != null)
        {
            currentPossibleMoves = piece.GetPossibleMoves(selectedPos.Value, gameM.pieceGrid);
        }

        // TODO: currentPossibleMoves 위치에 타일 하이라이트 켜기
    }

    private void DeselectPiece()
    {
        selectedPos = null;
        currentPossibleMoves.Clear();

        // TODO: 타일 하이라이트 끄기
    }

    private bool IsMyTurnAndPiece(int x, int y, CheseGameM gameM)
    {
        // 턴 및 팀 색상 체크 로직 구현
        return true;
    }
}
