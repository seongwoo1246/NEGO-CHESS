using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class StartSceneM : MonoBehaviour
{

    [Header("게임 관련 버튼")]
    [SerializeField] private Button GameStartBtn;
    [SerializeField] private Button GameSettingBtn;
    [SerializeField] private Button GameExitBtn;
    [SerializeField] private Button SingleBtn;
    [SerializeField] private Button MultiBtn;
    [Header("설정창")]
    [SerializeField] private GameObject Setting;
    [SerializeField] private GameObject Panel;
    private void Awake()
    {
        ScriptM.Register<StartSceneM>(this, UseSpace.Local);
    }

    private void Start()
    {
        if(GameStartBtn != null)
        {
            GameStartBtn.onClick.RemoveAllListeners();
            GameStartBtn.onClick.AddListener(SelectMode);
        }

        if(GameSettingBtn != null)
        {
            GameSettingBtn.onClick.RemoveAllListeners();
            GameSettingBtn.onClick.AddListener(OpenSetting);
        }

        if(GameExitBtn  != null)
        {
            GameExitBtn.onClick.RemoveAllListeners();
            GameExitBtn.onClick.AddListener(ExitGame);
        }
        
        if(SingleBtn  != null)
        {
            SingleBtn.onClick.RemoveAllListeners();
            SingleBtn.onClick.AddListener(EnterSingleMode);
        }

        if(MultiBtn != null)
        {
            MultiBtn.onClick.RemoveAllListeners();
            MultiBtn.onClick.AddListener(EnterMultiMode);
        }
    }


    private void SelectMode()
    {
        GameStartBtn.gameObject.SetActive(false);
        GameSettingBtn.gameObject.SetActive(false);
        GameExitBtn.gameObject.SetActive(false);
        SingleBtn.gameObject.SetActive(true);
        MultiBtn.gameObject.SetActive(true);
    }

    private void OpenSetting()
    {
        Panel.SetActive(true);
        Setting.SetActive(true);
    }

   private void ExitGame()
    {
        Application.Quit();
    }

    private void EnterSingleMode()
    {
        if(ScriptM.TryGet<ScenesM>(out ScenesM scenesM))
        {
            GameStartBtn.gameObject.SetActive(true);
            GameSettingBtn.gameObject.SetActive(true);
            GameExitBtn.gameObject.SetActive(true);
            SingleBtn.gameObject.SetActive(false);
            MultiBtn.gameObject.SetActive(false);
            scenesM.LoadScene(SceneNumber.Lobby);
        }
    }
    private void EnterMultiMode()
    {
        if(ScriptM.TryGet<ScenesM>(out ScenesM scenesM))
        {
            GameStartBtn.gameObject.SetActive(true);
            GameSettingBtn.gameObject.SetActive(true);
            GameExitBtn.gameObject.SetActive(true);
            SingleBtn.gameObject.SetActive(false);
            MultiBtn.gameObject.SetActive(false);
            scenesM.LoadScene(SceneNumber.MultiLobby);
        }
    }





}
