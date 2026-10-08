using UnityEngine;
//카메라에 장착해야 하는 스크립트 게임 오브젝트들이 화면 비율이 바뀌어도 게임오브젝트가 잘 보이게 하기 위함

[RequireComponent(typeof(Camera))]
public class ScreenController : MonoBehaviour
{

    [Header("기준이 되는 디자인 해상도 (예: 16:9 렌더링)")]
    public float targetWidth = 10f;   // 화면 가로에 채우고 싶은 월드 단위 크기 
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



    // 유니티 화면 변경시 유니티 내부의 이벤트를 호출해서 변경
    private void OnRectTransformDimensionsChange()
    {
        // 해상도/창 크기가 변경될 때 카메라 Orthographic Size 재계산
        AdjustCameraSize();
    }





    // 에디터 환경에서 해상도 변경 시 실시간 반영
#if UNITY_EDITOR
    private void Update()
    {
        AdjustCameraSize();
    }
#endif


}
