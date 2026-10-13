
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartSceneM : MonoBehaviour
{
    [SerializeField] characterData characterData;

    [Header("게임 관련 버튼")]
    [SerializeField] private Button GameStartBtn;
    [SerializeField] private Button GameSettingBtn;
    [SerializeField] private Button GameExitBtn;
    [SerializeField] private Button SingleBtn;
    [SerializeField] private Button MultiBtn;

    [Header("설정창")]
    [SerializeField] private GameObject Setting;
    [SerializeField] private GameObject Panel;

    [Header("캐릭터 설정")]
    [SerializeField] private bool isFristCahr = true;
    [SerializeField] private GameObject Player;
    [SerializeField] public TMP_InputField PlayerName;
    [SerializeField] public TMP_InputField PlayerMonth;
    [SerializeField] public TMP_InputField PlayerDay;
    [SerializeField] public Button genderChangeBtn;
    [SerializeField] public Button completeBtn;
    [SerializeField] private TextMeshProUGUI Texts;
    [SerializeField] public Image Icon;

    


    private void Awake()
    {
        ScriptM.Register<StartSceneM>(this, UseSpace.Local);
    }

    private void Start()
    {
        characterData.InitializeDictionary();

        if (GameStartBtn != null)
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
            SingleBtn.onClick.AddListener(openChar);
        }

        if(completeBtn != null)
        {
            completeBtn.onClick.RemoveAllListeners();
            completeBtn.onClick.AddListener(EnterSingleMode);
        }

        if(MultiBtn != null)
        {
            MultiBtn.onClick.RemoveAllListeners();
            MultiBtn.onClick.AddListener(EnterMultiMode);
        }

        if(genderChangeBtn != null)
        {
            genderChangeBtn.onClick.RemoveAllListeners();
            genderChangeBtn.onClick.AddListener(ChangeGender);
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
            InputData();
            scenesM.LoadScene(SceneNumber.Lobby);
        }
    }

    private void openChar()
    {
        Player.SetActive(true);
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

    private void InputData()
    {
        if(isFristCahr == true)
        {
            singlePlayer.playerName = PlayerName.text;

            if (int.TryParse(PlayerMonth.text, out singlePlayer.birthMonth))
            {

            }
            else
            {
                Texts.text = "예) 12월 = 12 식으로 적어주세요.";
            }

            if (int.TryParse(PlayerDay.text, out singlePlayer.birthDay))
            {

            }
            else
            {
                Texts.text = "예) 12일 = 12 식으로 적어주세요.";
            }

            isFristCahr = false;
        }

      
    }

    private void ChangeGender()
    {
        singlePlayer.isGirl = !singlePlayer.isGirl;

        if (singlePlayer.isGirl == true)
        {
            singlePlayer.gender = "여";
           List<Sprite> sprites = characterData.GetSprites(3);
            singlePlayer.icon = sprites[0];
            Icon.sprite = singlePlayer.icon;
        }
        else
        {
            singlePlayer.gender = "남";
            List<Sprite> sprites = characterData.GetSprites(3);
            singlePlayer.icon = sprites[1];
            Icon.sprite = singlePlayer.icon;
        }
    }

   
}
