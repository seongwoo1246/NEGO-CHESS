using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = DebugM<KeySettingM>;
public class KeySettingM : MonoBehaviour
{
    [Header("Input Action Asset 참조")]
    [SerializeField] private InputActionAsset inputActions;

    [SerializeField] private InputAction turnover;
    [SerializeField] private InputAction negotiation;
    [SerializeField] private InputAction giveUp;
    [SerializeField] private InputAction openMenu;
    [SerializeField] private InputAction ready;
    [SerializeField] private InputAction gameStart;

    [Header("UI 교체를 위한 텍스트들")]
    [SerializeField] private TextMeshProUGUI turnoverT;
    [SerializeField] private TextMeshProUGUI negotiationT;
    [SerializeField] private TextMeshProUGUI giveUpT;
    [SerializeField] private TextMeshProUGUI openMenuT;
    [SerializeField] private TextMeshProUGUI readyT;
    [SerializeField] private TextMeshProUGUI gameStartT;

    [Header("입력 열기")]
    [SerializeField] GameObject DisPlay;
    [SerializeField] GameObject Sound;
    [SerializeField] GameObject Setting;
    [SerializeField] GameObject SettingPopUp;
    [SerializeField] GameObject SettingPanal;


    private const string BINDING_SAVE_KEY = "CustomKeyBindings";

    private void Awake()
    {
        ScriptM.Register<KeySettingM>(this, UseSpace.Local);

       
        // 게임 시작 시 기존에 저장된 키 세팅이 있다면 불러오기
        LoadKeyBindings();
    }

    public void OpenSetting()
    {
        Sound.SetActive(false);
        DisPlay.SetActive(false);
        Setting.SetActive(true);
    }

    public void ExitSetting()
    {
        Sound.SetActive(false);
        DisPlay.SetActive(true);
        Setting.SetActive(false);
        SettingPanal.SetActive(false);
        SettingPopUp.SetActive(false);

    }

    /// <summary>
    /// 모든 UI 버튼 텍스트를 현재 설정된 키 이름으로 갱신
    /// </summary>
    public void UpdateAllUI()
    {
        UpdateActionUI(turnover, turnoverT, 0);
        UpdateActionUI(negotiation, negotiationT, 0);
        UpdateActionUI(giveUp, giveUpT, 0);
        UpdateActionUI(openMenu, openMenuT, 0);
        UpdateActionUI(ready, readyT, 0);
        UpdateActionUI(gameStart, gameStartT, 0);
    }

    /// <summary>
    /// 단일 액션의 바인딩 키 이름을 텍스트 UI에 반영
    /// </summary>
    private void UpdateActionUI(InputAction action, TMP_Text textUI, int bindingIndex = 0)
    {
        if (action == null || textUI == null) return;

        // GetBindingDisplayString을 사용하면 "<Keyboard>/space" -> "Space" 형태로 가독성 있게 변환됨
        string displayString = action.GetBindingDisplayString(bindingIndex);
        textUI.text = displayString;
    }

    /// <summary>
    /// 특정 액션의 키를 변경(리바인딩)하는 함수
    /// </summary>
    /// <param name="actionToRebind">바꿀 InputAction (예: Controls.Player.Move)</param>
    /// <param name="bindingIndex">바꿀 바인딩 인덱스 (기본 0)</param>
    public void StartRebinding(InputAction actionToRebind, TMP_Text targetText, int bindingIndex = 0)
    {
        // 1. 리바인딩 시작 전 해당 액션 비활성화
        actionToRebind.Disable();

        if (targetText != null) targetText.text = "...";

        // 2. 대기 모드 진입 및 키 입력 감지
        var rebindingOperation = actionToRebind.PerformInteractiveRebinding(bindingIndex)
            // 키보드/마우스/패드 등의 아무 입력이나 대기
            .WithControlsExcluding("<Mouse>/position") // 마우스 이동 등 제외할 입력 지정 가능
            .WithControlsExcluding("<Mouse>/delta")
            .OnComplete(operation =>
            {
                // 입력 완료 시 처리
                actionToRebind.Enable();
                operation.Dispose(); // 메모리 해제

                // 입력시 UI변경
                UpdateActionUI(actionToRebind, targetText, bindingIndex);

                // 바뀐 키 세팅 저장
                SaveKeyBindings();
                Debug.Log($"키 변경 완료: {actionToRebind.name} -> {actionToRebind.bindings[bindingIndex].effectivePath}");
            })
            .OnCancel(operation =>
            {
                // 취소 시 처리
                actionToRebind.Enable();
                operation.Dispose();

                //취소 시 기존 키 복원
                UpdateActionUI(actionToRebind, targetText, bindingIndex);
            });

        // 3. 리바인딩 시작
        rebindingOperation.Start();
    }

    /// <summary>
    /// 바뀐 키 세팅을 PlayerPrefs에 저장
    /// </summary>
    public void SaveKeyBindings()
    {
        if (inputActions == null) return;

        // 1. 전체 Input Action의 바인딩 오버라이드 정보를 JSON 문자열로 변환
        string bindingsJson = inputActions.SaveBindingOverridesAsJson();

        // 2. PlayerPrefs에 저장
        PlayerPrefs.SetString(BINDING_SAVE_KEY, bindingsJson);
        PlayerPrefs.Save();

        Debug.Log("[KeySettingM] 키 바인딩 저장 완료");
    }

    /// <summary>
    /// 저장된 키 세팅 불러오기
    /// </summary>
    public void LoadKeyBindings()
    {
        if (inputActions == null) return;

        if (PlayerPrefs.HasKey(BINDING_SAVE_KEY))
        {
            string bindingsJson = PlayerPrefs.GetString(BINDING_SAVE_KEY);

            if (!string.IsNullOrEmpty(bindingsJson))
            {
                // JSON 문자열을 가져와 Input Action 에셋에 덮어씌움
                inputActions.LoadBindingOverridesFromJson(bindingsJson);
                Debug.Log("[KeySettingM] 키 바인딩 불러오기 완료");
            }
        }
    }

    /// <summary>
    /// 기본 키 세팅으로 리셋
    /// </summary>
    public void ResetToDefault()
    {
        if (inputActions == null) return;

        // 1. 메모리상의 모든 오버라이드 제거
        inputActions.RemoveAllBindingOverrides();

        // 2. PlayerPrefs 저장값 삭제
        PlayerPrefs.DeleteKey(BINDING_SAVE_KEY);
        PlayerPrefs.Save();

        Debug.Log("[KeySettingM] 키 바인딩 기본값 초기화 완료");
    }

    #region 키 변경 함수 모음

    public void OnClickRebindTurnOver()
    {
        StartRebinding(turnover, turnoverT, 0);
    }

    public void OnClickRebindNegotiation()
    {
        StartRebinding(negotiation, negotiationT, 0);
    }

    public void OnClickRebindGiveUp()
    {
        StartRebinding(giveUp, giveUpT, 0);
    }

    public void OnClickRebindOpenMenu()
    {
        StartRebinding(openMenu, openMenuT, 0);
    }

    public void OnClickRebindReady()
    {
        StartRebinding(ready, readyT,0);
    }

    public void OnClickRebindGameStart()
    {
        StartRebinding(gameStart, gameStartT,0);
    }

    #endregion
}

/*
 키 바꾸는 부분에 대해서
과거(GetKeyDown)으로 부르는 부분과
최신(InputAction)을 이용하는  부분

부분에서 과거부분은 키 변경 및 다른 부분에서 해야할 코드도 복잡하고 일일히 작성해야 하는 부분을
최신부분은 New Input System은 런타임 키 리바인딩(Rebinding) 기능과 JSON 저장/불러오기 기능을 자체적으로 제공하기 때문에 
최신 부분으로 활용하는 편이 코드 난이도도 줄어들고 코드량이 많이 줄어든다.

내장된 Rebinding API 제공 = InputAction.PerformInteractiveRebinding() 매소드 한 개로 다음 플레이어 누른 키로 자동으로 감지해서 변경해준다고 한다.
 inputActionAsset.SaveBindingOverridesAsJson()를 이용해서 바로 json으로 저장하는 기능이 같이 들어가 있었다. 그리고 플레이어프리팹으로 간편하게 저장 및 복원 할 수 있다.
 복합 키 / 패드 / 키보드 자동 지원 - 다른 입력장치에도 동일하게 적용할 수 있다고 한다.
 
 */
