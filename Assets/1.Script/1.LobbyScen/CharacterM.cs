using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class CharacterM : MonoBehaviour
{
     public characterData character;
    [SerializeField] TextMeshProUGUI characterName;
    [SerializeField] TextMeshProUGUI gender;
    [SerializeField] TextMeshProUGUI birthDay;
    [SerializeField] TextMeshProUGUI message;
    [SerializeField] Button nameBtn;
    [SerializeField] Button genderBtn;
    [SerializeField] Button MessageBtn;
    [SerializeField] Image icon;
    [SerializeField] TMP_InputField ChangeText;
    [SerializeField] Button submitBtn;

    private EditMode currentEdit = EditMode.None;

    private void Start()
    {
        birthDay.text = $"{singlePlayer.birthMonth}-{singlePlayer.birthDay}";
        characterName.text = $"{singlePlayer.playerName}";
        gender.text = $"{singlePlayer.gender}";
        icon.sprite = singlePlayer.icon;
        message.text = PlayerPrefs.GetString("PlayerMessage", " ");
        // 각 버튼을 누르면 입력 모드를 바꾸고 입력 필드를 활성화
        nameBtn.onClick.AddListener(() => { SoundService.Current.playSFX("버튼"); currentEdit = EditMode.Name; ChangeText.text = ""; ChangeText.gameObject.SetActive(true); });
        MessageBtn.onClick.AddListener(() => { SoundService.Current.playSFX("버튼"); currentEdit = EditMode.Message; ChangeText.text = ""; ChangeText.gameObject.SetActive(true); });
        genderBtn.onClick.AddListener(() => 
        {
            SoundService.Current.playSFX("버튼");
            singlePlayer.isGirl = !singlePlayer.isGirl;
            singlePlayer.gender = singlePlayer.isGirl ? "여" : "남";
            gender.text = singlePlayer.gender;
            icon.sprite = singlePlayer.icon;
        });

        // 확인/완료 버튼을 눌렀을 때
        if (submitBtn != null)
        {
            submitBtn.onClick.AddListener(ApplyInputData);
        }
    }

    private void ApplyInputData()
    {
        SoundService.Current.playSFX("버튼");
        string inputStr = ChangeText.text;

        switch (currentEdit)
        {
            case EditMode.Name:
                characterName.text = inputStr;
                PlayerPrefs.SetString("PlayerName", characterName.text);
                PlayerPrefs.Save();
                break;
            case EditMode.Message:
                message.text = inputStr;
                PlayerPrefs.SetString("PlayerMessage", message.text);
                PlayerPrefs.Save();
               
                break;
            case EditMode.None:
                
                break;
        }

        // 입력 완료 후 모드 초기화 및 입력창 비우기
        ChangeText.gameObject.SetActive(false);
        currentEdit = EditMode.None;
        ChangeText.text = "";
    }
}
