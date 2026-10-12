
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
    [SerializeField] private bool isFristCahr;
    [SerializeField] private GameObject Player;
    [SerializeField] public TMP_InputField PlayerName;
    [SerializeField] public TMP_InputField PlayerMonth;
    [SerializeField] public TMP_InputField PlayerDay;
    [SerializeField] public Button genderChangeBtn;
    [SerializeField] public Button completeBtn;
    [SerializeField] private TextMeshProUGUI Texts;

    


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
        if(singlePlayer.isGirl == true)
        {
            singlePlayer.gender = "여";
            
        }
    }

    public List<Sprite> GetSpritesByCharacterId(int targetId)
    {
        // ID가 일치하는 항목을 리스트에서 한 줄로 쏙 찾아옵니다!
        CharacterEntry foundEntry = characterData.characterList.Find(entry => entry.characterId == targetId);

        if (foundEntry.sprites != null)
        {
            return foundEntry.sprites; // 찾았으면 스프라이트 리스트 반환
        }

        Debug.LogWarning("해당 ID의 캐릭터를 찾을 수 없습니다: " + targetId);
        return null;
    }
}
