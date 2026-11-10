
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.UI;

public class StartSceneM : MonoBehaviour
{
    [SerializeField] characterData characterData;

    [Header("게임 관련 버튼")]
    [SerializeField] private Button GameStartBtn;
    [SerializeField] private Button GameExitBtn;
    [SerializeField] private Button SingleBtn;
    [SerializeField] private Button MultiBtn;
    [SerializeField] private Button credicBtn;
    

    [Header("캐릭터 설정")]
    [SerializeField] private GameObject Player;
    [SerializeField] public TMP_InputField PlayerName;
    [SerializeField] public TMP_InputField PlayerMonth;
    [SerializeField] public TMP_InputField PlayerDay;
    [SerializeField] public Button genderChangeBtn;
    [SerializeField] public Button completeBtn;
    [SerializeField] private TextMeshProUGUI Texts;
    [SerializeField] public Image Icon;

    [SerializeField] private GameObject credicPanel;


    private void Awake()
    {
        ScriptM.Register<StartSceneM>(this, UseSpace.Local);
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }

    private void Start()
    {
        characterData.InitializeDictionary();

        if (GameStartBtn != null)
        {
            GameStartBtn.onClick.RemoveAllListeners();
            GameStartBtn.onClick.AddListener(SelectMode);
        }
        if (credicBtn != null)
        {
            credicBtn.onClick.RemoveAllListeners();
            credicBtn.onClick.AddListener(OpenCredic);
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
        SoundService.Current.playSFX("버튼");
        GameStartBtn.gameObject.SetActive(false);
        GameExitBtn.gameObject.SetActive(false);
        SingleBtn.gameObject.SetActive(true);
        MultiBtn.gameObject.SetActive(true);
    }

    private void OpenCredic()
    {
        SoundService.Current.playSFX("버튼");
        credicPanel.SetActive(!credicPanel.activeSelf);
    }
   

   private void ExitGame()
    {
        SoundService.Current.playSFX("버튼");
        Application.Quit();
    }

    private void EnterSingleMode()
    {
        SoundService.Current.playSFX("버튼");
        if (ScriptM.TryGet<ScenesM>(out ScenesM scenesM))
        {
            GameStartBtn.gameObject.SetActive(true);
            GameExitBtn.gameObject.SetActive(true);
            SingleBtn.gameObject.SetActive(false);
            MultiBtn.gameObject.SetActive(false);
           if( singlePlayer.isFristPlay == true)
            {
                InputData();
            }
            if (ScriptM.TryGet<HP>(out HP hp))
            {
                if (hp != null) { hp.ShowPanel(); }
            }
            scenesM.LoadScene(SceneNumber.Lobby);
        }
    }

    private void openChar()
    {
        SoundService.Current.playSFX("버튼");
        if (singlePlayer.isFristPlay == true)
        {
            Player.SetActive(true);
        }
        else
        {
            EnterSingleMode();
        }
    }


    private void EnterMultiMode()
    {
        SoundService.Current.playSFX("버튼");
        SoundService.Current.FadeSound("로비", 3f).Forget();
        if (ScriptM.TryGet<ScenesM>(out ScenesM scenesM))
        {
            GameStartBtn.gameObject.SetActive(true);
            GameExitBtn.gameObject.SetActive(true);
            SingleBtn.gameObject.SetActive(false);
            MultiBtn.gameObject.SetActive(false);
            scenesM.LoadScene(SceneNumber.MultiLobby);
        }
    }

    private void InputData()
    {
        if (!int.TryParse(PlayerMonth.text, out int month) || month < 1 || month > 12)
        {
            Texts.text = "예) 12월 = 12 식으로 적어주세요.";
            return;
        }

        // 윤년의 2월 29일도 허용하려고 2024년 기준으로 그 달의 마지막 날을 계산
        int maxDay = System.DateTime.DaysInMonth(2024, month);
        if (!int.TryParse(PlayerDay.text, out int day) || day < 1 || day > maxDay)
        {
            Texts.text = $"예) 12일 = 12 식으로 적어주세요. ({month}월은 1~{maxDay}일)";
            return;
        }

        singlePlayer.playerName = PlayerName.text;
        singlePlayer.birthMonth = month;
        singlePlayer.birthDay = day;
        singlePlayer.isFristPlay = false;
    }

    private void ChangeGender()
    {
        SoundService.Current.playSFX("버튼");
        singlePlayer.isGirl = !singlePlayer.isGirl;               // 먼저 바꾸고
        singlePlayer.gender = singlePlayer.isGirl ? "여" : "남";
        Icon.sprite = singlePlayer.icon;
    }

   
}
