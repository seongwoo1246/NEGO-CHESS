using UnityEngine;
//

[RequireComponent(typeof(Camera))]
public class ScreenController : MonoBehaviour
{

    [Header("기준이 되는 디자인 해상도 (예: 16:9 렌더링)")]
    public float targetWidth = 8f;   // 화면 가로에 채우고 싶은 월드 단위 크기 (예: 체스판 8칸)
    public float targetAspect = 16f / 9f; // 개발 기준 화면 비율

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        AdjustCameraSize();
    }

    public void AdjustCameraSize()
    {
        // 현재 기기의 화면 비율 (Width / Height)
        float currentAspect = (float)Screen.width / Screen.height;

        if (cam.orthographic)
        {
            // 1. 세로가 길어지는 비율 (예: 스마트폰 세로 모드) -> 가로 폭을 보장하도록 Size 조절
            if (currentAspect < targetAspect)
            {
                cam.orthographicSize = (targetWidth / 2f) / currentAspect;
            }
            // 2. 가로가 더 넓은 비율 -> 기준 Orthographic Size 적용
            else
            {
                cam.orthographicSize = targetWidth / 2f;
            }
        }
    }

    // 에디터 환경에서 해상도 변경 시 실시간 반영
#if UNITY_EDITOR
    private void Update()
    {
        AdjustCameraSize();
    }
#endif


}
