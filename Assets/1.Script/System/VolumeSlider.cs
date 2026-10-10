using UnityEngine;
using UnityEngine.UI;
using Debug = DebugM<VolumeSlider>;
public class VolumeSlider : MonoBehaviour
{
    public VolumeType type;

    private Slider slider;

    // 전역 수신 가능한 이벤트 정의 
    public static event System.Action<float> OnMasterVolumeChanged;
    public static event System.Action<float> OnBGMVolumeChanged;
    public static event System.Action<float> OnSFXVolumeChanged;

    private void Awake()
    {
        // 1. 컴포넌트 캐싱 및 예외 처리
        slider = GetComponent<Slider>();
        if (slider == null)
        {
            Debug.LogError($"[VolumeSlider] '{gameObject.name}'에 Slider 컴포넌트가 없습니다!");
        }
    }

    private void OnEnable()
    {
        if (slider != null)
        {
            // 2. 슬라이더 값이 바뀔 때 실행될 리스너 등록
            slider.onValueChanged.RemoveListener(OnSliderValueChanged);
            slider.onValueChanged.AddListener(OnSliderValueChanged);
        }
    }

    private void OnDisable()
    {
        if (slider != null)
        {
            // 3. 메모리 누수 방지를 위한 리스너 해제
            slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
    }

    /// <summary>
    /// 슬라이더를 움직였을 때 타입에 맞추어 해당하는 이벤트를 발생시킴
    /// </summary>
    private void OnSliderValueChanged(float value)
    {
        switch (type)
        {
            case VolumeType.Master:
                OnMasterVolumeChanged?.Invoke(value);
                break;
            case VolumeType.BGM:
                OnBGMVolumeChanged?.Invoke(value);
                break;
            case VolumeType.SFX:
                OnSFXVolumeChanged?.Invoke(value);
                break;
        }
    }

  
}
