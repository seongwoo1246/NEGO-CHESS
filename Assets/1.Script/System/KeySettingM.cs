using UnityEngine;
using UnityEngine.InputSystem;
using Debug = DebugM<KeySettingM>;
public class KeySettingM : MonoBehaviour
{
    [Header("Input Action Asset 참조")]
    [SerializeField] private InputActionAsset inputActions;

    private const string REBIND_SAVE_KEY = "CustomKeyBindings";

    private void Awake()
    {
        // 게임 시작 시 기존에 저장된 키 세팅이 있다면 불러오기
        LoadKeyBindings();
    }

    /// <summary>
    /// 특정 액션의 키를 변경(리바인딩)하는 함수
    /// </summary>
    /// <param name="actionToRebind">바꿀 InputAction (예: Controls.Player.Move)</param>
    /// <param name="bindingIndex">바꿀 바인딩 인덱스 (기본 0)</param>
    public void StartRebinding(InputAction actionToRebind, int bindingIndex = 0)
    {
        // 1. 리바인딩 시작 전 해당 액션 비활성화
        actionToRebind.Disable();

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

                // 바뀐 키 세팅 저장
                SaveKeyBindings();
                Debug.Log($"키 변경 완료: {actionToRebind.name} -> {actionToRebind.bindings[bindingIndex].effectivePath}");
            })
            .OnCancel(operation =>
            {
                // 취소 시 처리
                actionToRebind.Enable();
                operation.Dispose();
            });

        // 3. 리바인딩 시작
        rebindingOperation.Start();
    }

    /// <summary>
    /// 바뀐 키 세팅을 PlayerPrefs에 저장
    /// </summary>
    public void SaveKeyBindings()
    {
        string rebindsJson = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString(REBIND_SAVE_KEY, rebindsJson);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 저장된 키 세팅 불러오기
    /// </summary>
    public void LoadKeyBindings()
    {
        if (PlayerPrefs.HasKey(REBIND_SAVE_KEY))
        {
            string rebindsJson = PlayerPrefs.GetString(REBIND_SAVE_KEY);
            inputActions.LoadBindingOverridesFromJson(rebindsJson);
        }
    }

    /// <summary>
    /// 기본 키 세팅으로 리셋
    /// </summary>
    public void ResetToDefault()
    {
        inputActions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(REBIND_SAVE_KEY);
    }


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
