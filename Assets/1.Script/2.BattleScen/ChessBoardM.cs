using UnityEngine;


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
}
