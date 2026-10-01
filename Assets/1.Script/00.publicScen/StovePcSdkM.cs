using System;
using UnityEngine;
// 스토브 PCSDK3 네임스페이스 및 Base static 연동
using Stove.PCSDK;
using static Stove.PCSDK.Base;

public class StovePCSDKManager : MonoBehaviour
{

    [Header("STOVE Configurations")]
    [Tooltip("SANDBOX, STAGE 또는 LIVE")]
    [SerializeField] private string env = "SANDBOX"; //접속환경, (개발자용 or 실서버 LIVE)
    [SerializeField] private string gameId = "YOUR_GAME_ID";// 스토브에서 받은 게임 아이디
    [SerializeField] private string appKey = "YOUR_APP_KEY";// 스토브에서 받은 앱 키


    /// <summary>
    /// 초기화 완료 여부 플래그 , SDK 초기화가 완료되었는지 체크하여 Update의 Base_RunCallback()이나 중복 초기화를 막는 데 사용됨
    /// </summary>
    public bool IsInitialized { get; private set; } = false;

    // [초기화 결과 이벤트 (델리게이트)]
    // SDK 초기화 성공/실패 시 외부(UI, 매니저 등)로 알림을 보내주는 C# 이벤트
    // 예: OnInitializeSuccess += ShowMainMenu; 처럼 구독해서 사용함.
    public event Action OnInitializeSuccess;
    public event Action<string> OnInitializeFailed;

    private void Awake()
    {
        ScriptM.Register<StovePCSDKManager>(this, UseSpace.Network_Global);
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<StovePCSDKManager>();
    }

    private void Start()
    {
        InitializeSDK();
    }

    private void Update()
    {
        // 매 프레임 수신 이벤트 처리
        if (IsInitialized)
        {
            Base_RunCallback();
        }
    }

    /// <summary>
    /// 스토브 PCSDK3 초기화
    /// </summary>
    public void InitializeSDK()
    {
        if (IsInitialized)
        {
            Debug.LogWarning("[StovePCSDK] 이미 초기화되어 있습니다.");
            return;
        }

        Debug.Log("[StovePCSDK3] 초기화 진행 중...");

        // 1. 파라미터 구성
        StovePCInitializeParamEx2 initParam = new StovePCInitializeParamEx2();
        initParam.environment = this.env;
        initParam.gameId = this.gameId;
        initParam.applicationKey = this.appKey;
        initParam.waitTimeMillisec = 60000;
        initParam.launchLauncher = true;

        // 2. 런처 구동 확인 후 초기화 실행
        Base_RestartAppIfNecessaryAsyncEx2(initParam, (CallbackResult callbackResult, bool restartAppIfNecessary) =>
        {
            if (restartAppIfNecessary)
            {
                Debug.Log("[StovePCSDK] 스토브 런처를 통해 다시 실행해야 합니다. 게임을 종료합니다.");
                Application.Quit();
                return;
            }

            // 3. Base SDK 초기화
            Base_InitializeEx((CallbackResult initResult) =>
            {
                // initResult 안의 result(Result struct)가 가진 IsSuccessful() 메소드 호출
                if (initResult.result.IsSuccessful())
                {
                    IsInitialized = true;
                    Debug.Log("[StovePCSDK3] 초기화 성공!");
                    OnInitializeSuccess?.Invoke();
                }
                else
                {
                    string errorMsg = $"[StovePCSDK3] 초기화 실패 - Code: {initResult.result.resultCode}, Msg: {initResult.errorMessage}";
                    Debug.LogError(errorMsg);
                    OnInitializeFailed?.Invoke(errorMsg);
                }
            });
        });
    }

    /// <summary>
    /// 게임을 종료할 때 호출 되는 것
    /// </summary>
    private void OnApplicationQuit()
    {
        if (IsInitialized)
        {
            Base_UnInitialize();
            IsInitialized = false;
            Debug.Log("[StovePCSDK3] Uninitialized.");
        }
    }
}