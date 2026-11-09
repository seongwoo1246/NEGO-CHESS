using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class Setting : MonoBehaviour
{
    [Header("버튼 및 토글")]
    [SerializeField] Button ResetBtn;
    [SerializeField] Button GoToStratScene;
    [SerializeField] Button SoundBtn;
    [SerializeField] Toggle MasterMute;
    [SerializeField] Toggle BGMMute;
    [SerializeField] Toggle SFXMute;

    [Header("사운드 열기")]
    [SerializeField] private GameObject DisPlay;
    [SerializeField] private GameObject Sound;
    [SerializeField] private GameObject setting;

    private void Start()
    {
        
        
        {
            if (MasterMute != null)
            {
                MasterMute.onValueChanged.RemoveAllListeners();
                MasterMute.onValueChanged.AddListener((isMasterMuted) => SoundService.Current.SoundMute(VolumeType.Master));
            }

            if (BGMMute != null)
            {
                BGMMute.onValueChanged.RemoveAllListeners();
                BGMMute.onValueChanged.AddListener((isBGMMuted) => SoundService.Current.SoundMute(VolumeType.BGM));
            }

            if (SFXMute != null)
            {
                SFXMute.onValueChanged.RemoveAllListeners();
                SFXMute.onValueChanged.AddListener((isSFXMuted) => SoundService.Current.SoundMute(VolumeType.SFX));
            }

            if (ResetBtn != null)
            {
                ResetBtn.onClick.RemoveAllListeners();
                ResetBtn.onClick.AddListener(SoundService.Current.ResetAllSoundSettings);
            }

            if(SoundBtn != null)
            {
                SoundBtn.onClick.RemoveAllListeners();
                SoundBtn.onClick.AddListener(OpenSound);
            }
            if(GoToStratScene != null)
            {
                GoToStratScene.onClick.RemoveAllListeners();
                GoToStratScene.onClick.AddListener(GoToStartScenes);
            }

            VolumeSlider.OnMasterVolumeChanged -= SoundService.Current.SetMasterVolume;
            VolumeSlider.OnBGMVolumeChanged -= SoundService.Current.SetBGMVolume;
            VolumeSlider.OnSFXVolumeChanged -= SoundService.Current.SetSFXVolume;
            
            VolumeSlider.OnMasterVolumeChanged += SoundService.Current.SetMasterVolume;
            VolumeSlider.OnBGMVolumeChanged += SoundService.Current.SetBGMVolume;
            VolumeSlider.OnSFXVolumeChanged += SoundService.Current.SetSFXVolume;
        }  
    }

    public void OpenSound()
    {
        Sound.SetActive(true);
        DisPlay.SetActive(false);
        setting.SetActive(false);
    }


    public void GoToStartScenes()
    {

        SoundService.Current.FadeSound("평소", 3f).Forget();
        if (ScriptM.TryGet<HP>(out  HP hp))
        {
            if(hp!=null) { hp.HidePanel(); }
        }
        if (ScriptM.TryGet<ScenesM>(out ScenesM service))
        {
            Sound.SetActive(false);
            DisPlay.SetActive(false);
            setting.SetActive(true);
            ScriptM.ClearSceneLocalServices();
            service.LoadScene(SceneNumber.GameStart);
        }
    }
}
