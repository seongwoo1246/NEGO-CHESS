using UnityEngine;
using Debug = DebugM<DisplayM>;

public class DisplayM : MonoBehaviour
{


    private void Start()
    {
        // 저장된 설정 데이터가 존재하는지 확인
        if (PlayerPrefs.HasKey("ScreenWidth"))
        {
            int savedWidth = PlayerPrefs.GetInt("ScreenWidth");
            int savedHeight = PlayerPrefs.GetInt("ScreenHeight");
            WindowMode savedMode = (WindowMode)PlayerPrefs.GetInt("WindowMode");

            ChangeResolution(savedWidth, savedHeight, savedMode);
        }
        else
        {
            // 최초 실행 시에만 기본 해상도(예: 모니터 현재 해상도)로 초기화
            ChangeResolution(Screen.currentResolution.width, Screen.currentResolution.height, WindowMode.FullScreenWindow);
        }
    }

    /// <summary>
    /// 해상도 및 화면 모드를 변경하는 함수
    /// </summary>
    /// <param name="width">가로 해상도 (예: 1920)</param>
    /// <param name="height">세로 해상도 (예: 1080)</param>
    /// <param name="mode">화면 모드 Enum</param>
    public void ChangeResolution(int width, int height, WindowMode mode)
    {
        FullScreenMode unityMode = FullScreenMode.Windowed;
        
        switch (mode)
        {
            case WindowMode.ExclusiveFullScreen:
                unityMode = FullScreenMode.ExclusiveFullScreen;
                break;
            case WindowMode.FullScreenWindow:
                unityMode = FullScreenMode.FullScreenWindow;
                break;
            case WindowMode.Windowed:
                unityMode = FullScreenMode.Windowed;
                break;
        }

        // 유니티 공식 해상도 및 화면 모드 변경 함수
        Screen.SetResolution(width, height, unityMode);

        // 2. PlayerPrefs에 설정값 즉시 저장
        PlayerPrefs.SetInt("ScreenWidth", width);
        PlayerPrefs.SetInt("ScreenHeight", height);
        PlayerPrefs.SetInt("WindowMode", (int)mode);
        PlayerPrefs.Save(); // 디스크에 동기화 저장

        Debug.Log($"화면 설정 변경 완료: {width}x{height} / 모드: {unityMode}");
    }


}




/*
HasKey() - 키에 해당하는 부분이 있는지 확인 하는 부분 
GetKey() - 키에 해당하는 게 없으면 가져오는 부분 Set이 없으면 Get으로 가져와서 사용
SetKey() - 키에 값을 세팅해두는 부분

 FullScreenMode - 유니티에서 전체모드 창모드등을 확인하는 부분으로 4가지가 있는데 
ExclusiveFullScreen (전용 전체 화면)
FullScreenWindow (테두리 없는 전체 화면 / 전체 창 모드)
Windowed (일반 창 모드)
MaximizedWindow (최대화된 창 모드)
으로 사용되는데 기본적으로는 MaximizedWindow는 잘 사용되지 않는 듯 하다.
 
 */
