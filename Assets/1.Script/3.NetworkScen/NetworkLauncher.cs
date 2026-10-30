using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// 방 만들기 / 방 참가 / 방 나가기의 "결과 상자".
/// Success가 true면 성공, false면 Message에 실패 이유가 들어 있어요.
/// </summary>
public readonly struct RoomResult
{
    public bool Success { get; }
    public string Message { get; }

    private RoomResult(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public static RoomResult Ok(string message = "") => new RoomResult(true, message);
    public static RoomResult Fail(string message) => new RoomResult(false, message);
}

/// <summary>
/// 내 프로필(닉네임) 보관함.
/// PlayerPrefs를 여기저기서 직접 만지면 키 이름이 틀리기 쉬우므로, 이 클래스 한 곳으로만 읽고 씁니다.
/// </summary>
public static class PlayerProfile
{
    public const int MinNicknameLength = 2;
    public const int MaxNicknameLength = 8;
    private const string NicknameKey = "Nickname";

    public static string Nickname => PlayerPrefs.GetString(NicknameKey, string.Empty);
    public static bool HasNickname => !string.IsNullOrEmpty(Nickname);

    /// <summary>닉네임을 검사하고, 올바르면 저장합니다.</summary>
    public static bool TrySetNickname(string raw, out string errorMessage)
    {
        string nickname = (raw ?? string.Empty).Trim();

        if (nickname.Length < MinNicknameLength || nickname.Length > MaxNicknameLength)
        {
            errorMessage = $"닉네임은 {MinNicknameLength}~{MaxNicknameLength}글자로 적어 주세요.";
            return false;
        }

        PlayerPrefs.SetString(NicknameKey, nickname);
        PlayerPrefs.Save();
        errorMessage = string.Empty;
        return true;
    }
}

/// <summary>
/// 방 목록에 보여줄 "방 한 개"의 정보 카드.
/// Fusion이 주는 SessionInfo를 UI가 쓰기 쉬운 모양으로 바꿔 담아 둡니다.
/// </summary>
public readonly struct RoomListItem
{
    public string SessionId { get; }       // 진짜 주소 (접속할 때 사용)
    public string RoomName { get; }        // 문패 (화면에 보여줄 이름)
    public int PlayerCount { get; }
    public int MaxPlayers { get; }
    public bool IsOpen { get; }
    public string PasswordHash { get; }    // 비밀번호 지문 (없으면 빈 글자)

    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);
    public bool IsFull => PlayerCount >= MaxPlayers;
    public bool CanJoin => IsOpen && !IsFull;

    private RoomListItem(string sessionId, string roomName, int playerCount, int maxPlayers, bool isOpen, string passwordHash)
    {
        SessionId = sessionId;
        RoomName = roomName;
        PlayerCount = playerCount;
        MaxPlayers = maxPlayers;
        IsOpen = isOpen;
        PasswordHash = passwordHash;
    }

    /// <summary>Fusion의 SessionInfo → RoomListItem 변환</summary>
    public static RoomListItem From(SessionInfo info)
    {
        string roomName = ReadString(info.Properties, NetworkLauncher.KeyRoomName);
        if (string.IsNullOrEmpty(roomName)) roomName = "(이름 없는 방)";

        string passwordHash = ReadString(info.Properties, NetworkLauncher.KeyPasswordHash);

        return new RoomListItem(info.Name, roomName, info.PlayerCount, info.MaxPlayers, info.IsOpen, passwordHash);
    }

    private static string ReadString(IReadOnlyDictionary<string, SessionProperty> properties, string key)
    {
        if (properties != null &&
            properties.TryGetValue(key, out SessionProperty property) &&
            property.PropertyValue is string text)
        {
            return text;
        }
        return string.Empty;
    }
}
/// <summary>
/// 네트워크 상에서 방을 만들고, 참가하고, 나가는 일을 맡은 스크립트 ("주방").
///
/// [방 주소 규칙]
///  - 진짜 주소 = 겹칠 일 없는 랜덤 번호 (SessionName)
///  - 문패       = 사람이 보는 방 이름   (SessionProperties["roomName"])
/// </summary>
public class NetworkLauncher : MonoBehaviour
{
    // 방에 붙여 두는 "이름표" 열쇠들 (방 목록에서 읽을 때도 똑같이 씁니다)
    public const string KeyRoomName = "roomName";
    public const string KeyPasswordHash = "pw";

    private const string LastSessionKey = "LastSessionName";

    [Header("연결")]
    [Tooltip("RoomState 프리팹 (NetworkObject + RoomState가 붙은 프리팹)")]
    [SerializeField] private NetworkPrefabRef roomStatePrefab;
    [Tooltip("방 목록 게시판 (방에 들어가기 전에 꺼 주기 위해 연결)")]
    [SerializeField] private RoomBrowser browser;
    [Tooltip("방에 들어간 뒤 방 정보가 도착하길 기다리는 최대 시간(초)")]
    [SerializeField, Min(1f)] private float roomStateWaitSeconds = 8f;

    private NetworkRunner _runner;
    private NetworkSceneManagerDefault _sceneManager;
    private bool _isBusy; // 만드는 중/들어가는 중에 또 누르는 것을 막는 자물쇠

    // 내 클라이언트의 고유 토큰 (재접속 시 동일 인물인지 확인하는 용도)
    private static string LocalCustomAuthToken => SystemInfo.deviceUniqueIdentifier;

    public bool IsInRoom => _runner != null && _runner.IsRunning;

    // ───────────── 방 만들기 ─────────────

    public UniTask<RoomResult> CreateRoomAsync(RoomCreateRequest request)
    {
        if (!request.Validate(out string error)) return UniTask.FromResult(RoomResult.Fail(error));
        if (!roomStatePrefab.IsValid) return UniTask.FromResult(RoomResult.Fail("RoomState 프리팹이 연결되지 않았어요. (개발자 설정 필요)"));
       

        return RunExclusiveAsync(async () =>
        {
            if (ScriptM.TryGet<NetWorkLobbyUi>(out NetWorkLobbyUi ui))
                if (ui != null) ui.SetConnecting(true);
            try
            {
                string sessionId = Guid.NewGuid().ToString("N");

                var properties = new Dictionary<string, SessionProperty> { [KeyRoomName] = request.RoomName };
                if (request.HasPassword)
                {
                    properties[KeyPasswordHash] = RoomCreateRequest.HashPassword(sessionId, request.Password);
                }

                bool started = await StartSessionAsync(new StartGameArgs
                {
                    SessionName = sessionId,
                    PlayerCount = request.MaxPlayers,
                    IsVisible = true,
                    IsOpen = true,
                    SessionProperties = properties
                });
                if (!started)
                {
                  
                    await ShutdownSessionAsync();
                    return RoomResult.Fail("방을 만들지 못했어요. 인터넷 연결을 확인하고 다시 해 주세요.");
                }

                // 방 공용 게시판(RoomState)은 방을 만든 사람이 딱 한 번 만듭니다.
                _runner.Spawn(roomStatePrefab, onBeforeSpawned: (runner, spawned) =>
                    spawned.GetComponent<RoomState>().Initialize(request.RoomName, request.MaxPlayers));

                if (!await WaitForRoomStateAsync())
                {
                    await ShutdownSessionAsync();
                    return RoomResult.Fail("방 정보를 준비하지 못했어요. 다시 시도해 주세요.");
                }
                return RoomResult.Ok(request.RoomName);
            }
            finally
            {
                if (ui != null) ui.SetConnecting(false);
            }
            
        });
        
    }

    // ───────────── 방 참가 ─────────────

    public UniTask<RoomResult> JoinRoomAsync(RoomListItem room, string password)
    {
        if (!room.IsOpen) return UniTask.FromResult(RoomResult.Fail("이미 게임이 시작된 방이에요."));
        if (room.IsFull) return UniTask.FromResult(RoomResult.Fail("방이 가득 찼어요."));
        if (ScriptM.TryGet<NetWorkLobbyUi>(out NetWorkLobbyUi ui))
            // 비밀번호 확인: 입력한 비밀번호의 지문과 방에 붙어 있는 지문을 비교합니다.
            if (room.HasPassword)
        {
            string typed = (password ?? string.Empty).Trim();
            if (RoomCreateRequest.HashPassword(room.SessionId, typed) != room.PasswordHash)
            {
                return UniTask.FromResult(RoomResult.Fail("비밀번호가 맞지 않아요. 위 비밀번호 칸에 적고 다시 눌러 주세요."));
            }
        }

        return RunExclusiveAsync(async () =>
        {
            if (ui != null) ui.SetConnecting(true);
            try
            {
                bool started = await StartSessionAsync(new StartGameArgs { SessionName = room.SessionId });
                if (!started)
                {
                    await ShutdownSessionAsync();
                    return RoomResult.Fail("방에 들어가지 못했어요. 인터넷 연결을 확인해 주세요.");
                }

                if (!await WaitForRoomStateAsync())
                {
                    await ShutdownSessionAsync();
                    return RoomResult.Fail("방이 사라졌거나 연결이 너무 늦어요. 방 목록을 다시 확인해 주세요.");
                }

                return RoomResult.Ok(RoomState.Instance.RoomNameText);
            }
            finally
            {
                if (ui != null) ui.SetConnecting(false);
            }
        });
    }

    // ───────────── 방 나가기 ─────────────

    public async UniTask LeaveRoomAsync()
    {
        PlayerPrefs.DeleteKey(LastSessionKey); // 내가 직접 나가는 것이므로 "재접속"은 하지 않음
        await ShutdownSessionAsync();
    }

    // ───────────── 공통 부품 ─────────────

    /// <summary>한 번에 하나의 방 작업만 하도록 지켜 주는 감싸개</summary>
    private async UniTask<RoomResult> RunExclusiveAsync(Func<UniTask<RoomResult>> work)
    {
        if (_isBusy) return RoomResult.Fail("처리 중이에요. 잠깐만 기다려 주세요.");
        if (IsInRoom) return RoomResult.Fail("이미 방에 들어와 있어요.");

        _isBusy = true;
        try
        {
            return await work();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            await ShutdownSessionAsync();
            return RoomResult.Fail("알 수 없는 문제가 생겼어요. 잠시 후 다시 해 주세요.");
        }
        finally
        {
            _isBusy = false; // 성공이든 실패든 자물쇠는 꼭 풀어 줍니다
        }
    }

    /// <summary>도구를 준비하고 방(세션)에 접속합니다. 성공하면 true.</summary>
    private async UniTask<bool> StartSessionAsync(StartGameArgs args)
    {
        // 방 목록 게시판은 방에 들어가기 전에 꺼 둡니다.
        if (browser != null) await browser.StopBrowsingAsync();

        PrepareNetworkServices(args.SessionName);

        args.GameMode = GameMode.Shared;
        args.ConnectionToken = Encoding.UTF8.GetBytes(LocalCustomAuthToken);
        args.SceneManager = _sceneManager;

        var result = await _runner.StartGame(args);
        if (!result.Ok)
        {
            Debug.LogWarning($"[NetworkLauncher] 접속 실패: {result.ShutdownReason}");
            return false;
        }

        // 성공했을 때만 재접속용 방 번호를 저장
        PlayerPrefs.SetString(LastSessionKey, args.SessionName);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>방 공용 게시판(RoomState)이 도착할 때까지 기다립니다.</summary>
    private async UniTask<bool> WaitForRoomStateAsync()
    {
        float deadline = Time.unscaledTime + roomStateWaitSeconds;
        while (RoomState.Instance == null)
        {
            if (Time.unscaledTime > deadline) return false;
            await UniTask.Yield();
        }
        return true;
    }

    /// <summary>방 접속에 필요한 도구들(재접속 서비스, 러너, 씬 매니저)을 준비합니다.</summary>
    private void PrepareNetworkServices(string sessionId)
    {
        var reconnectService = new GameObject("ReconnectionM").AddComponent<ReconnectionM>();
        ScriptM.Register(reconnectService);

        _runner = gameObject.AddComponent<NetworkRunner>();
        _sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        reconnectService.InitRunner(_runner, sessionId, GameMode.Shared);
    }

    /// <summary>방에서 나가거나 실패했을 때, 만들어 둔 도구들을 모두 치웁니다. (여러 번 불러도 안전)</summary>
    private async UniTask ShutdownSessionAsync()
    {
        if (_runner != null)
        {
            // destroyGameObject:false → 이 스크립트가 붙은 오브젝트까지 같이 지워지는 것을 막아요!
            await _runner.Shutdown(destroyGameObject: false);
            Destroy(_runner);
            _runner = null;
        }

        if (_sceneManager != null)
        {
            Destroy(_sceneManager);
            _sceneManager = null;
        }

        CleanupNetworkServices();
    }

    // 멀티플레이 종료 또는 로비 퇴장 시 호출
    public void CleanupNetworkServices()
    {
        if (ScriptM.TryGet<ReconnectionM>(out var reconnectService))
        {
            ScriptM.Unregister<ReconnectionM>();
            Destroy(reconnectService.gameObject);
        }
        // 전역이 아닌 모든 서비스를 해제한다.
        ScriptM.ClearSceneLocalServices();
    }
}