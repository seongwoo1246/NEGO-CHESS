using UnityEngine;
using UnityEngine.UI;

public class Setting : MonoBehaviour
{
    [Header("버튼 및 토글")]
    [SerializeField] Button ResetBtn;
    [SerializeField] Toggle MasterMute;
    [SerializeField] Toggle BGMMute;
    [SerializeField] Toggle SFXMute;

    [Header("사운드 열기")]
    [SerializeField] private GameObject DisPlay;
    [SerializeField] private GameObject Sound;
    [SerializeField] private GameObject setting;

    private void Awake()
    {
        if (ScriptM.TryGet<SoundM>(out SoundM service))
        {
            if (MasterMute != null)
            {
                MasterMute.onValueChanged.RemoveAllListeners();
                MasterMute.onValueChanged.AddListener((isMasterMuted) => service.SoundMute(VolumeType.Master));
            }

            if (BGMMute != null)
            {
                BGMMute.onValueChanged.RemoveAllListeners();
                BGMMute.onValueChanged.AddListener((isBGMMuted) => service.SoundMute(VolumeType.BGM));
            }

            if (SFXMute != null)
            {
                SFXMute.onValueChanged.RemoveAllListeners();
                SFXMute.onValueChanged.AddListener((isSFXMuted) => service.SoundMute(VolumeType.SFX));
            }

            if (ResetBtn != null)
            {
                ResetBtn.onClick.RemoveAllListeners();
                ResetBtn.onClick.AddListener(service.ResetSound);
            }
        }  
    }


    private void OnEnable()
    {
        if (ScriptM.TryGet<SoundM>(out SoundM service))
        {
            // 1. 슬라이더 스크립트에 선언된 정적(static) 이벤트와 매니저 함수를 연결
            VolumeSlider.OnMasterVolumeChanged += service.SetMasterVolume;
            VolumeSlider.OnBGMVolumeChanged += service.SetBGMVolume;
            VolumeSlider.OnSFXVolumeChanged += service.SetSFXVolume;
        }
      
    }


    private void OnDisable()
    {
        if (ScriptM.TryGet<SoundM>(out SoundM service))
        {
            // 2. 메모리 누수 및 중복 구독 방지를 위해 반드시 해제
            VolumeSlider.OnMasterVolumeChanged -= service.SetMasterVolume;
            VolumeSlider.OnBGMVolumeChanged -= service.SetBGMVolume;
            VolumeSlider.OnSFXVolumeChanged -= service.SetSFXVolume;
        }
           
    }

    public void OpenSound()
    {
        Sound.SetActive(true);
        DisPlay.SetActive(false);
        setting.SetActive(false);
    }

}
