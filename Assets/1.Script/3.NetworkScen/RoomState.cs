using System;
using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;



/// <summary>네트워크로 동기화되는 "플레이어 한 명의 방 안 정보"</summary>
public struct RoomPlayerData : INetworkStruct
{
    public NetworkString<_16> Nickname;
    public byte Team;
    public NetworkBool Ready;
}

/// <summary>UI가 읽기 편하도록 복사해 둔 "플레이어 한 명의 화면용 정보"</summary>
public readonly struct RoomPlayerView
{
    public PlayerRef Player { get; }
    public string Nickname { get; }
    public RoomTeam Team { get; }
    public bool Ready { get; }
    public bool IsHost { get; }
    public bool IsLocal { get; }

    public RoomPlayerView(PlayerRef player, string nickname, RoomTeam team, bool ready, bool isHost, bool isLocal)
    {
        Player = player;
        Nickname = nickname;
        Team = team;
        Ready = ready;
        IsHost = isHost;
        IsLocal = isLocal;
    }
}

/// <summary>
/// 방 하나의 "공용 게시판" (모든 사람이 같은 내용을 봅니다).
///
/// [핵심 규칙: 쓰는 사람은 방장 한 명뿐]
///  - 방 상태(닉네임, 팀, 준비 여부, 카운트다운)는 상태 권한자(= 방장)만 바꿀 수 있어요.
///  - 다른 사람은 RPC로 "저 준비했어요" 하고 부탁만 하고, 방장이 검사한 뒤 반영합니다.
///  - 누가 보냈는지는 Fusion이 알려주는 값(RpcInfo.Source)을 쓰기 때문에, 남인 척 속일 수 없어요.
///
/// [흐름] 모두 입장 → 모두 준비 → 카운트다운(기본 5초) → 방 잠금 → 게임 씬 로드
/// </summary>
public class RoomState : NetworkBehaviour, IPlayerLeft
{
    // ───────────── 어디서든 접근하는 창구 ─────────────
    public static RoomState Instance { get; private set; }
    public static event Action<RoomState> InstanceReady;   // 방 상태가 준비됨
    public static event Action InstanceLost;               // 방 상태가 사라짐 (연결 끊김/방 나감)

    /// <summary>화면에 보이는 내용이 바뀔 때마다 호출 (사람/팀/준비/카운트다운)</summary>
    public event Action Changed;

    /// <summary>게임 시작이 확정되었을 때 모든 클라이언트에서 호출</summary>
    public event Action GameStarting;

    // ───────────── 설정 (프리팹 Inspector에서 조절) ─────────────
    [SerializeField, Min(1f)] private float countdownSeconds = 5f;
    [Tooltip("Build Settings에 등록된 게임 씬 이름")]
    [SerializeField] private string gameSceneName = "NetworkGame";

    // ───────────── 네트워크로 공유되는 값 ─────────────
    [Networked] public NetworkString<_32> RoomName { get; private set; }
    [Networked] public int MaxPlayers { get; private set; }
    [Networked] private TickTimer Countdown { get; set; }
    [Networked] private NetworkBool GameStarted { get; set; }
    [Networked, Capacity(8)] private NetworkDictionary<PlayerRef, RoomPlayerData> Players => default;

    // ───────────── 내 컴퓨터에서만 쓰는 값 ─────────────
    private bool _gameStartHandled;
    private int _lastSignature = int.MinValue;
    private readonly List<PlayerRef> _removeBuffer = new List<PlayerRef>(4);

    private static readonly Comparison<RoomPlayerView> ByPlayerId =
        (a, b) => a.Player.PlayerId.CompareTo(b.Player.PlayerId);

    // ───────────── 만들어질 때 / 사라질 때 ─────────────

    /// <summary>방을 만든 사람이 스폰 직전에 한 번 호출합니다.</summary>
    public void Initialize(string roomName, int maxPlayers)
    {
        RoomName = roomName;
        MaxPlayers = Mathf.Clamp(maxPlayers, 1, 2);
    }

    public override void Spawned()
    {
        Instance = this;
        _gameStartHandled = false;
        _lastSignature = int.MinValue;

        // 게임 씬으로 넘어가도 닉네임/팀 정보를 계속 쓸 수 있게 유지
        if (transform.parent == null) DontDestroyOnLoad(gameObject);

        InstanceReady?.Invoke(this);

        // 입장하자마자 "내 닉네임으로 등록해 주세요" 부탁하기
        RPC_Register(PlayerProfile.Nickname);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
        {
            Instance = null;
            InstanceLost?.Invoke();
        }
    }

    // ───────────── 부탁하기 (누구나 호출 → 방장이 실행) ─────────────

    public void RequestSetReady(bool ready) => RPC_SetReady(ready);
    public void RequestSetTeam(RoomTeam team) => RPC_SetTeam((byte)team);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Register(NetworkString<_16> nickname, RpcInfo info = default)
    {
        PlayerRef sender = info.Source;
        if (GameStarted || Players.ContainsKey(sender) || Players.Count >= MaxPlayers) return;

        string cleaned = nickname.ToString().Trim();
        if (cleaned.Length == 0) cleaned = $"Player{sender.PlayerId}";

        Players.Add(sender, new RoomPlayerData
        {
            Nickname = cleaned,
            Team = (byte)FindFreeTeam(),
            Ready = false
        });
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetReady(bool ready, RpcInfo info = default)
    {
        if (GameStarted) return;
        if (!Players.TryGet(info.Source, out RoomPlayerData me)) return;

        me.Ready = ready;
        Players.Set(info.Source, me);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetTeam(byte team, RpcInfo info = default)
    {
        if (GameStarted || team > (byte)RoomTeam.White) return;
        if (!Players.TryGet(info.Source, out RoomPlayerData me) || me.Team == team) return;

        // 이미 그 팀을 쓰는 사람이 있으면 서로 팀을 바꿉니다 (체스는 흑/백이 겹치면 안 되니까)
        bool foundOther = false;
        PlayerRef otherKey = default;
        RoomPlayerData other = default;
        foreach (var kv in Players)
        {
            if (kv.Key != info.Source && kv.Value.Team == team)
            {
                foundOther = true;
                otherKey = kv.Key;
                other = kv.Value;
                break;
            }
        }

        if (foundOther)
        {
            other.Team = me.Team;
            other.Ready = false;
            Players.Set(otherKey, other);
        }

        me.Team = team;
        me.Ready = false; // 팀을 바꾸면 준비를 다시 하게 함 (카운트다운도 자동 취소)
        Players.Set(info.Source, me);
    }

    // ───────────── 방장이 매 틱마다 하는 일 ─────────────

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority || GameStarted) return;

        // 혹시 놓친 퇴장자가 있는지 가끔 청소
        if (Runner.Tick.Raw % 30 == 0) RemoveAbsentPlayers();

        bool everyoneReady = IsEveryoneReady();

        if (everyoneReady && !Countdown.IsRunning)
        {
            Countdown = TickTimer.CreateFromSeconds(Runner, countdownSeconds);   // 카운트다운 시작
        }
        else if (!everyoneReady && Countdown.IsRunning)
        {
            Countdown = default;                                                 // 누가 취소/퇴장 → 취소
        }

        if (Countdown.Expired(Runner)) BeginGame();
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (Object.HasStateAuthority) Players.Remove(player);
    }

    private void RemoveAbsentPlayers()
    {
        _removeBuffer.Clear();
        foreach (var kv in Players)
        {
            if (!IsActivePlayer(kv.Key)) _removeBuffer.Add(kv.Key);
        }
        for (int i = 0; i < _removeBuffer.Count; i++) Players.Remove(_removeBuffer[i]);
    }

    private bool IsActivePlayer(PlayerRef player)
    {
        foreach (PlayerRef active in Runner.ActivePlayers)
        {
            if (active == player) return true;
        }
        return false;
    }

    private bool IsEveryoneReady()
    {
        if (Players.Count < MaxPlayers) return false;

        int black = 0, white = 0;
        foreach (var kv in Players)
        {
            if (!kv.Value.Ready) return false;
            if (kv.Value.Team == (byte)RoomTeam.Black) black++; else white++;
        }

        // 2인 방은 흑 1명 + 백 1명이어야 시작 가능
        return MaxPlayers != 2 || (black == 1 && white == 1);
    }

    private RoomTeam FindFreeTeam()
    {
        bool blackTaken = false, whiteTaken = false;
        foreach (var kv in Players)
        {
            if (kv.Value.Team == (byte)RoomTeam.Black) blackTaken = true; else whiteTaken = true;
        }
        return blackTaken && !whiteTaken ? RoomTeam.White : RoomTeam.Black;
    }

    private void BeginGame()
    {
        GameStarted = true;
        Countdown = default;

        // 시작한 뒤에는 다른 사람이 들어오거나 목록에 보이지 않게 방을 잠급니다.
        if (Runner.SessionInfo.IsValid)
        {
            Runner.SessionInfo.IsOpen = false;
            Runner.SessionInfo.IsVisible = false;
        }
    }

    // ───────────── 모든 사람의 화면에서 매 프레임 하는 일 ─────────────

    public override void Render()
    {
        // 1) 게임 시작 신호를 처음 본 순간 딱 한 번 처리
        if (GameStarted && !_gameStartHandled)
        {
            _gameStartHandled = true;
            GameStarting?.Invoke();
            LoadGameSceneIfAllowed();
        }

        // 2) 화면에 보이는 내용이 달라졌을 때만 UI에 알림 (매 프레임 UI 갱신 방지)
        int signature = ComputeSignature();
        if (signature != _lastSignature)
        {
            _lastSignature = signature;
            Changed?.Invoke();
        }
    }

    private int ComputeSignature()
    {
        unchecked
        {
            int hash = 17;
            foreach (var kv in Players)
            {
                hash = hash * 31 + kv.Key.PlayerId;
                hash = hash * 31 + kv.Value.Team;
                hash = hash * 31 + (kv.Value.Ready ? 1 : 0);
                hash = hash * 31 + kv.Value.Nickname.Length;
            }
            hash = hash * 31 + (int)Phase;
            hash = hash * 31 + CountdownSecondsLeft;
            hash = hash * 31 + MaxPlayers;
            return hash;
        }
    }

    // 장면(씬)을 불러올 수 있는 사람(공유 모드에서는 마스터 클라이언트) 한 명만 실행합니다.
    private void LoadGameSceneIfAllowed()
    {
        if (!Runner.IsSceneAuthority) return;

        int buildIndex = FindBuildIndex(gameSceneName);
        if (buildIndex < 0)
        {
            Debug.LogError($"[RoomState] Build Settings에서 '{gameSceneName}' 씬을 찾지 못했어요.");
            return;
        }

        Runner.LoadScene(SceneRef.FromIndex(buildIndex));
    }

    private static int FindBuildIndex(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (Path.GetFileNameWithoutExtension(path) == sceneName) return i;
        }
        return -1;
    }

    // ───────────── UI / 게임 씬이 읽어 가는 정보 ─────────────

    public string RoomNameText => RoomName.ToString();
    public int PlayerCount => Players.Count;
    public bool IsHost => Object.HasStateAuthority;

    public int ReadyCount
    {
        get
        {
            int count = 0;
            foreach (var kv in Players) if (kv.Value.Ready) count++;
            return count;
        }
    }

    public int CountdownSecondsLeft
    {
        get
        {
            float? remaining = Countdown.RemainingTime(Runner);
            return remaining.HasValue ? Mathf.CeilToInt(remaining.Value) : 0;
        }
    }

    public RoomPhase Phase
    {
        get
        {
            if (GameStarted) return RoomPhase.Starting;
            if (Countdown.IsRunning) return RoomPhase.Countdown;
            if (Players.Count < MaxPlayers) return RoomPhase.WaitingForPlayers;
            return RoomPhase.WaitingForReady;
        }
    }

    /// <summary>모든 플레이어 정보를 buffer에 담아 줍니다 (입장 순서대로 정렬).</summary>
    public void GetPlayers(List<RoomPlayerView> buffer)
    {
        buffer.Clear();
        foreach (var kv in Players) buffer.Add(ToView(kv.Key, kv.Value));
        buffer.Sort(ByPlayerId);
    }

    public bool TryGetLocalPlayer(out RoomPlayerView view)
    {
        PlayerRef local = Runner.LocalPlayer;
        if (Players.TryGet(local, out RoomPlayerData data))
        {
            view = ToView(local, data);
            return true;
        }
        view = default;
        return false;
    }

    /// <summary>게임 씬에서 "흑/백을 맡은 사람이 누구인지" 알고 싶을 때 사용합니다.</summary>
    public bool TryGetPlayerByTeam(RoomTeam team, out RoomPlayerView view)
    {
        foreach (var kv in Players)
        {
            if (kv.Value.Team == (byte)team)
            {
                view = ToView(kv.Key, kv.Value);
                return true;
            }
        }
        view = default;
        return false;
    }

    private RoomPlayerView ToView(PlayerRef player, RoomPlayerData data)
    {
        return new RoomPlayerView(
            player,
            data.Nickname.ToString(),
            (RoomTeam)data.Team,
            data.Ready,
            player == Object.StateAuthority,
            player == Runner.LocalPlayer);
    }
}