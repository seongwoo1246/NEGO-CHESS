using System;
using UnityEngine;
// 스토브 PCSDK3 C# 공식 네임스페이스
using Stove.PCSDK;
using Stove.PCSDK.Base;
using static Stove.PCSDK.Base;

public class StovePCSDKManager : MonoBehaviour
{
    private static StovePCSDKManager instance;

    public static StovePCSDKManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject obj = new GameObject("@StovePCSDKManager");
                instance = obj.AddComponent<StovePCSDKManager>();
                DontDestroyOnLoad(obj);
            }
            return instance;
        }
    }

    [Header("STOVE Configurations")]
    [Tooltip("SANDBOX, STAGE 또는 LIVE")]
    [SerializeField] private string env = "SANDBOX";
    [SerializeField] private string gameId = "YOUR_GAME_ID";
    [SerializeField] private string appKey = "YOUR_APP_KEY";

    public bool IsInitialized { get; private set; } = false;

    public event Action OnInitializeSuccess;
    public event Action<string> OnInitializeFailed;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeSDK();
    }

    private void Update()
    {
        // 수신 이벤트 갱신 (Base_RunCallback)
        if (IsInitialized)
        {
            Base_RunCallback();
        }
    }

    /// <summary>
    /// 스토브 PCSDK3 공식 초기화
    /// </summary>
    public void InitializeSDK()
    {
        if (IsInitialized)
        {
            Debug.LogWarning("[StovePCSDK] 이미 초기화되어 있습니다.");
            return;
        }

        Debug.Log("[StovePCSDK3] 초기화 진행 중...");

        // 1. 초기화 파라미터 설정
        StovePCInitializeParamEx2 initParam = new StovePCInitializeParamEx2
        {
            env = this.env,
            gameId = this.gameId,
            applicationKey = this.appKey,
            waitTimeMillisec = 60000,
            launchLauncher = true
        };

        // 2. 런처 체크 후 초기화 수행
        Base_RestartAppIfNecessaryAsyncEx2(ref initParam, (CallbackResult callbackResult, bool restartAppIfNecessary) =>
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
                if (initResult.GetResult().IsSuccessful())
                {
                    IsInitialized = true;
                    Debug.Log("[StovePCSDK3] 초기화 성공!");
                    OnInitializeSuccess?.Invoke();
                }
                else
                {
                    string errorMsg = $"[StovePCSDK3] 초기화 실패 (Code: {initResult.GetResult().GetCode()}, Msg: {initResult.GetResult().GetMessage()})";
                    Debug.LogError(errorMsg);
                    OnInitializeFailed?.Invoke(errorMsg);
                }
            });
        });
    }

    private void OnApplicationQuit()
    {
        if (IsInitialized)
        {
            Base_Uninitialize();
            IsInitialized = false;
            Debug.Log("[StovePCSDK3] Uninitialized.");
        }
    }
}