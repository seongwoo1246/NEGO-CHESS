using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = DebugM<KeySettingM>;
public class KeySettingM : MonoBehaviour
{
    public static KeySettingM Instance;

    [Header("Input Action Asset 참조")]
    [SerializeField] private InputActionAsset inputActions;
    [Header("UI 교체를 위한 텍스트들")]
    [SerializeField] private TextMeshProUGUI turnoverT;
    [SerializeField] private TextMeshProUGUI negotiationT;
    [SerializeField] private TextMeshProUGUI giveUpT;
    [SerializeField] private TextMeshProUGUI openMenuT;
    [SerializeField] private TextMeshProUGUI readyT;
    [SerializeField] private TextMeshProUGUI gameStartT;

    // 초기화를 위해 처음 글자를 저장 하는 역할
    private string defaultTurnoverText;
    private string defaultNegotiationText;
    private string defaultGiveUpText;
    private string defaultOpenMenuText;
    private string defaultReadyText;
    private string defaultGameStartText;

    [Header("입력 열기")]
    [SerializeField] GameObject DisPlay;
    [SerializeField] GameObject Sound;
    [SerializeField] GameObject Setting;
    [SerializeField] GameObject SettingPopUp;
    [SerializeField] GameObject SettingPanal;


    private const string BINDING_SAVE_KEY = "CustomKeyBindings";

    public event System.Action OnTurnoverTriggered;
    public event System.Action OnNegotiationTriggered;
    public event System.Action OnGiveUpTriggered;
    public event System.Action OnOpenMenuTriggered;
    public event System.Action OnReadyTriggered;
    public event System.Action OnGameStartTriggered;

    private InputAction turnoverAction;
    private InputAction negotiationAction;
    private InputAction giveUpAction;
    private InputAction openMenuAction;
    private InputAction readyAction;
    private InputAction gameStartAction;

    private void OnTurnover(InputAction.CallbackContext context) => OnTurnoverTriggered?.Invoke();
    private void OnNegotiation(InputAction.CallbackContext context) => OnNegotiationTriggered?.Invoke();
    private void OnGiveUp(InputAction.CallbackContext context) => OnGiveUpTriggered?.Invoke();
    private void OnOpenMenu(InputAction.CallbackContext context) => OnOpenMenuTriggered?.Invoke();
    private void OnReady(InputAction.CallbackContext context) => OnReadyTriggered?.Invoke();
    private void OnGameStart(InputAction.CallbackContext context) => OnGameStartTriggered?.Invoke();

    private void Awake()
    {
        Instance=this;

        if (turnoverT != null) defaultTurnoverText = turnoverT.text;
        if (negotiationT != null) defaultNegotiationText = negotiationT.text;
        if (giveUpT != null) defaultGiveUpText = giveUpT.text;
        if (openMenuT != null) defaultOpenMenuText = openMenuT.text;
        if (readyT != null) defaultReadyText = readyT.text;
        if (gameStartT != null) defaultGameStartText = gameStartT.text;

        if (inputActions != null)
        {
            // 액션 맵과 각 액션 이름은 사용 중이신 Input Action 에셋 설정에 맞게 수정해주세요.
            var map = inputActions.FindActionMap("Player");
            if (map != null)
            {
                turnoverAction = map.FindAction("Turnover");
                negotiationAction = map.FindAction("Negotiation");
                giveUpAction = map.FindAction("GiveUp");
                openMenuAction = map.FindAction("OpenMenu");
                readyAction = map.FindAction("Ready");
                gameStartAction = map.FindAction("GameStart");
            }
        }

        // 게임 시작 시 기존에 저장된 키 세팅이 있다면 불러오기
        LoadKeyBindings();
    }
    private void OnEnable()
    {
        if (inputActions != null) inputActions.Enable();

        // 각 입력이 수행(performed)되었을 때 이벤트 호출 연결
        if (turnoverAction != null) turnoverAction.performed += OnTurnover;
        if (negotiationAction != null) negotiationAction.performed += OnNegotiation;
        if (giveUpAction != null) giveUpAction.performed += OnGiveUp;
        if (openMenuAction != null) openMenuAction.performed += OnOpenMenu;
        if (readyAction != null) readyAction.performed += OnReady;
        if (gameStartAction != null) gameStartAction.performed += OnGameStart;
    }

    private void OnDisable()
    {
        if (inputActions != null) inputActions.Disable();

        // 메모리 누수 방지를 위한 이벤트 해제
        if (turnoverAction != null) turnoverAction.performed -= OnTurnover;
        if (negotiationAction != null) negotiationAction.performed -= OnNegotiation;
        if (giveUpAction != null) giveUpAction.performed -= OnGiveUp;
        if (openMenuAction != null) openMenuAction.performed -= OnOpenMenu;
        if (readyAction != null) readyAction.performed -= OnReady;
        if (gameStartAction != null) gameStartAction.performed -= OnGameStart;
    }

    public void OpenSetting()
    {
        Sound.SetActive(false);
        DisPlay.SetActive(false);
        Setting.SetActive(true);
        UpdateAllUI();
    }

    public void ExitSetting()
    {
        Sound.SetActive(false);
        DisPlay.SetActive(false);
        Setting.SetActive(true);
        SettingPanal.SetActive(false);
        SettingPopUp.SetActive(false);

    }

   

    /// <summary>
    /// 모든 UI 버튼 텍스트를 현재 설정된 키 이름으로 갱신
    /// </summary>
    public void UpdateAllUI()
    {
        if (inputActions == null) return;

        // 각 액션을 에셋에서 직접 찾아와서 UI 텍스트 업데이트
        UpdateActionUI(inputActions.FindAction("Turnover"), turnoverT, 0);
        UpdateActionUI(inputActions.FindAction("Negotiation"), negotiationT, 0);
        UpdateActionUI(inputActions.FindAction("GiveUp"), giveUpT, 0);
        UpdateActionUI(inputActions.FindAction("OpenMenu"), openMenuT, 0);
        UpdateActionUI(inputActions.FindAction("Ready"), readyT, 0);
        UpdateActionUI(inputActions.FindAction("GameStart"), gameStartT, 0);
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
        if (actionToRebind == null) return;

        // 💡 핵심: 액션이 속한 액션 맵이 꺼져있다면 켜주어야 입력을 감지할 수 있습니다!
        if (actionToRebind.actionMap != null && !actionToRebind.actionMap.enabled)
        {
            actionToRebind.actionMap.Enable();
        }

        if (targetText != null) targetText.text = "...";

        // 리바인딩 대상 액션만 비활성화
        actionToRebind.Disable();

        var rebindingOperation = actionToRebind.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .OnComplete(operation =>
            {
                operation.Dispose();
                actionToRebind.Enable();

                if (targetText != null)
                {
                    targetText.text = actionToRebind.GetBindingDisplayString(bindingIndex);
                }

                SaveKeyBindings();
                Debug.Log($"[KeySettingM] 키 변경 완료: {actionToRebind.name}");
            })
            .OnCancel(operation =>
            {
                operation.Dispose();
                actionToRebind.Enable();

                if (targetText != null)
                {
                    targetText.text = actionToRebind.GetBindingDisplayString(bindingIndex);
                }
                Debug.Log($"[KeySettingM] 키 변경 취소됨");
            })
            .Start();

        Debug.Log($"[KeySettingM] 리바인딩 대기 시작: {actionToRebind.name}"); // 진입 확인용 로그
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
        UpdateAllUI();
    }

    /// <summary>
    /// 기본 키 세팅으로 리셋
    /// </summary>
    public void ResetToDefault()
    {

        if (inputActions == null) return;

        // 1. 메모리에 남아있는 키 변경 오버라이드 제거
        inputActions.RemoveAllBindingOverrides();

        // 2. 저장된 파일/레지스트리 삭제
        PlayerPrefs.DeleteKey(BINDING_SAVE_KEY);
        PlayerPrefs.Save();

        // 3. UI 텍스트 초기화 (작성하신 코드)
        if (turnoverT != null) turnoverT.text = defaultTurnoverText;
        if (negotiationT != null) negotiationT.text = defaultNegotiationText;
        if (giveUpT != null) giveUpT.text = defaultGiveUpText;
        if (openMenuT != null) openMenuT.text = defaultOpenMenuText;
        if (readyT != null) readyT.text = defaultReadyText;
        if (gameStartT != null) gameStartT.text = defaultGameStartText;

    }

    

    #region 키 변경 함수 모음

    public void OnClickRebindTurnOver()
    {
        if (inputActions == null) return;
        InputAction targetAction = inputActions.FindAction("Turnover"); // 에셋에서 직접 찾기
        if (targetAction != null)
        {
            StartRebinding(targetAction, turnoverT, 0);
        }
    }

    public void OnClickRebindNegotiation()
    {
        if (inputActions == null) return;
        InputAction targetAction = inputActions.FindAction("Negotiation");
        if (targetAction != null)
        {
            StartRebinding(targetAction, negotiationT, 0);
        }
    }

    public void OnClickRebindGiveUp()
    {
         if (inputActions == null) return;
        InputAction targetAction = inputActions.FindAction("GiveUp");
        if (targetAction != null)
        {
            StartRebinding(targetAction, giveUpT, 0);
        }
    }

    public void OnClickRebindOpenMenu()
    {
        if (inputActions == null) return;
        InputAction targetAction = inputActions.FindAction("OpenMenu");
        if (targetAction != null)
        {
            StartRebinding(targetAction, openMenuT, 0);
        }
    }

    public void OnClickRebindReady()
    {
        if (inputActions == null) return;
        InputAction targetAction = inputActions.FindAction("Ready");
        if (targetAction != null)
        {
            StartRebinding(targetAction, readyT, 0);
        }
    }

    public void OnClickRebindGameStart()
    {
         if (inputActions == null) return;
        InputAction targetAction = inputActions.FindAction("GameStart");
        if (targetAction != null)
        {
            StartRebinding(targetAction, gameStartT, 0);
        }
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
