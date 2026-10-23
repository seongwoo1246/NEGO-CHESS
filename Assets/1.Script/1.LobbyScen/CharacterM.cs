using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = DebugM<CharacterM>;

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
        icon.sprite = character.GetSprites(3)[0];
        message.text = PlayerPrefs.GetString("PlayerMessage", " ");
        // 각 버튼을 누르면 입력 모드를 바꾸고 입력 필드를 활성화
        nameBtn.onClick.AddListener(() => { currentEdit = EditMode.Name; ChangeText.text = ""; ChangeText.gameObject.SetActive(true); });
        MessageBtn.onClick.AddListener(() => { currentEdit = EditMode.Message; ChangeText.text = ""; ChangeText.gameObject.SetActive(true); });
        genderBtn.onClick.AddListener(() => {
          if(singlePlayer.isGirl == true)
            {
                singlePlayer.gender = "여";
                gender.text = singlePlayer.gender;
                icon.sprite = character.GetSprites(3)[0];
                singlePlayer.isGirl = false ;
            }
            else
            {
                singlePlayer.gender = "남";
                gender.text = singlePlayer.gender;
                icon.sprite = character.GetSprites(3)[1];
                singlePlayer.isGirl = true;
            }
            });

        // 확인/완료 버튼을 눌렀을 때
        if (submitBtn != null)
        {
            submitBtn.onClick.AddListener(ApplyInputData);
        }
    }

    private void ApplyInputData()
    {
        string inputStr = ChangeText.text;

        switch (currentEdit)
        {
            case EditMode.Name:
                characterName.text = inputStr;
                Debug.Log($"이름이 {characterName}(으)로 변경되었습니다.");
                break;
            case EditMode.Message:
                message.text = inputStr;
                PlayerPrefs.SetString("PlayerMessage", message.text);
                PlayerPrefs.Save();
                Debug.Log($"메세지가 변경되었습니다.");
                break;
            case EditMode.None:
                Debug.Log("변경할 항목을 먼저 선택해주세요.");
                break;
        }

        // 입력 완료 후 모드 초기화 및 입력창 비우기
        ChangeText.gameObject.SetActive(false);
        currentEdit = EditMode.None;
        ChangeText.text = "";
    }
}
