using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

/// <summary>
/// "방 목록 게시판" 담당.
/// 로비에 접속해서 만들어진 방들의 목록을 계속 받아오고, 검색어로 걸러서 UI에 알려줍니다.
///
/// - 방에 들어가기 전(로비)에만 켜 두고, 방에 들어갈 때는 NetworkLauncher가 꺼 줍니다.
/// - 이 스크립트는 NetworkLauncher와 "다른" 빈 오브젝트에 붙이세요. (러너가 겹치지 않게)
/// </summary>
public class RoomBrowser : MonoBehaviour, INetworkRunnerCallbacks
{
    /// <summary>화면에 보여줄 방 목록이 바뀔 때마다 호출됩니다.</summary>
    public event Action<IReadOnlyList<RoomListItem>> RoomListChanged;

    /// <summary>"불러오는 중..." 같은 안내 문구가 바뀔 때 호출됩니다. (빈 글자면 안내 없음)</summary>
    public event Action<string> StatusChanged;

    public bool HasKeyword => !string.IsNullOrEmpty(_keyword);

    private NetworkRunner _runner;
    private GameObject _runnerObject;
    private string _keyword = string.Empty;

    private readonly List<SessionInfo> _latestSessions = new List<SessionInfo>();
    private readonly List<RoomListItem> _visibleRooms = new List<RoomListItem>();

    // 들어갈 수 있는 방을 위로, 그다음 이름순
    private static readonly Comparison<RoomListItem> RoomOrder = (a, b) =>
    {
        int joinable = b.CanJoin.CompareTo(a.CanJoin);
        return joinable != 0 ? joinable : string.CompareOrdinal(a.RoomName, b.RoomName);
    };

    // ───────────── 켜기 / 끄기 ─────────────

    /// <summary>로비에 접속해서 방 목록 받기를 시작합니다. 이미 켜져 있으면 아무 일도 안 해요.</summary>
    public async UniTask StartBrowsingAsync()
    {
        if (_runner != null) return;

        var runnerObject = new GameObject("RoomBrowserRunner");
        runnerObject.transform.SetParent(transform, false);
        var runner = runnerObject.AddComponent<NetworkRunner>();
        runner.AddCallbacks(this);

        _runnerObject = runnerObject;
        _runner = runner;
        StatusChanged?.Invoke("방 목록을 불러오는 중이에요...");

        try
        {
            var result = await runner.JoinSessionLobby(SessionLobby.Shared);

            if (_runner != runner) return; // 기다리는 사이에 꺼졌다면 조용히 끝

            if (!result.Ok)
            {
                Debug.LogWarning($"[RoomBrowser] 로비 접속 실패: {result.ShutdownReason}");
                await StopBrowsingAsync();
                StatusChanged?.Invoke("방 목록을 불러오지 못했어요. 인터넷 연결을 확인해 주세요.");
               
                return;
            }

            StatusChanged?.Invoke(string.Empty);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (_runner == runner) await StopBrowsingAsync();
            StatusChanged?.Invoke("방 목록을 불러오지 못했어요.");
           
        }

     

    }

    /// <summary>방 목록 받기를 멈추고 로비 연결을 끊습니다.</summary>
    public async UniTask StopBrowsingAsync()
    {
        NetworkRunner runner = _runner;
        GameObject runnerObject = _runnerObject;
        if (runner == null) return;

        _runner = null;
        _runnerObject = null;

        _latestSessions.Clear();
        RebuildVisibleRooms();

        runner.RemoveCallbacks(this);
        await runner.Shutdown(destroyGameObject: true);

        if (runnerObject != null) Destroy(runnerObject);
    }

    // ───────────── 검색 ─────────────

    /// <summary>검색어를 정합니다. 빈 글자면 모든 방을 보여줘요.</summary>
    public void SetKeyword(string keyword)
    {
        _keyword = (keyword ?? string.Empty).Trim();
        RebuildVisibleRooms();
    }

    private void RebuildVisibleRooms()
    {
        _visibleRooms.Clear();

        for (int i = 0; i < _latestSessions.Count; i++)
        {
            RoomListItem item = RoomListItem.From(_latestSessions[i]);

            bool matches = !HasKeyword ||
                           item.RoomName.IndexOf(_keyword, StringComparison.OrdinalIgnoreCase) >= 0;
            if (matches) _visibleRooms.Add(item);
        }

        _visibleRooms.Sort(RoomOrder);
        RoomListChanged?.Invoke(_visibleRooms);
    }

    private void OnDestroy()
    {
        if (_runner != null) _runner.RemoveCallbacks(this);
    }

    // ───────────── Fusion 콜백 ─────────────

    // ★ 우리가 실제로 쓰는 것은 이것 하나뿐입니다: 방 목록이 갱신되었을 때
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        _latestSessions.Clear();
        _latestSessions.AddRange(sessionList);
        RebuildVisibleRooms();
    }

    // ── 아래는 인터페이스를 채우기 위한 "빈 칸"들입니다 (쓰지 않음) ──
    // Fusion 버전에 따라 빠진 항목이 있으면 Visual Studio에서 Ctrl + . → "인터페이스 구현"으로 채우세요.
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}