using System;
using Fusion;
using UnityEngine;

/// <summary>
/// 멀티 체스 한 판의 "심판" (게임 씬에 놓는 NetworkObject).
///
/// [핵심 규칙: 결정은 방장(상태 권한자) 한 명만 한다]
///  - 다른 사람은 RPC로 "이렇게 하고 싶어요" 하고 부탁만 합니다.
///  - 방장이 규칙에 맞는지 검사한 뒤에만 반영하고, 모두에게 알려요.
///  - 누가 보냈는지는 Fusion이 알려 주는 값(RpcInfo.Source)을 쓰기 때문에 남인 척 속일 수 없어요.
///
/// [진행 순서]
///  WaitingForPlayers : 두 사람이 모두 게임 씬에 들어올 때까지 기다림
///  GameStart         : 각자 기물 배치 (서로의 배치 모습이 보임) → 둘 다 "준비 완료"
///  Playing           : 흰색부터 번갈아 대국 (시계 작동)
///  GameEnd           : 결과 표시 → 재대결(배치부터 다시) 또는 로비로
/// </summary>
public class NetworkGameM : NetworkBehaviour, IPlayerLeft
{
    // ───────────── 어디서든 접근하는 창구 ─────────────
    public static NetworkGameM Instance { get; private set; }
    public static event Action<NetworkGameM> InstanceReady;
    public static event Action InstanceLost;

    /// <summary>화면에 띄울 짧은 안내 ("둘 수 없는 수예요." 등). 나에게 온 것만 울립니다.</summary>
    public static event Action<string> Notice;

    /// <summary>화면에 보이는 내용이 바뀔 때마다 호출 (UI 갱신용)</summary>
    public event Action Changed;

    /// <summary>게임 단계가 바뀔 때 호출</summary>
    public event Action<GameState> StateChanged;

    // ───────────── 설정 (Inspector) ─────────────
    [Header("시간 규칙")]
    [SerializeField, Min(10f)] private float mainTimeSeconds = 300f;   // 각자 기본 시간(5분)
    [SerializeField, Min(0f)] private float bonusTimeSeconds = 5f;     // 기본 시간이 끝난 뒤 한 수당 초읽기(5초)
    [Tooltip("켜면 시간 초과 시 패배, 끄면 차례만 넘어갑니다. (기존 동작 = 끄기)")]
    [SerializeField] private bool forfeitOnTimeout = false;

    [Header("재대결")]
    [Tooltip("켜면 재대결 때 흑/백을 서로 바꿉니다.")]
    [SerializeField] private bool swapSidesOnRematch = false;

    [Header("연결")]
    [Tooltip("IChessBoardAdapter를 구현한 컴포넌트 (예: CheseGameMBoardAdapter)")]
    [SerializeField] private MonoBehaviour boardAdapter;

    // ───────────── 네트워크로 공유되는 값 ─────────────
    [Networked] public PlayerRef WhitePlayer { get; private set; }
    [Networked] public PlayerRef BlackPlayer { get; private set; }
    [Networked] public GameState State { get; private set; }
    [Networked] public NetworkBool IsWhiteTurn { get; private set; }

    [Networked] public float WhiteMainTime { get; private set; }
    [Networked] public float BlackMainTime { get; private set; }
    [Networked] public float CurrentBonusTime { get; private set; }

    [Networked] public PlayerRef Winner { get; private set; }          // None이면 무승부
    [Networked] public GameEndReason EndReason { get; private set; }

    [Networked] public int MoveCount { get; private set; }
    [Networked] public int MatchRound { get; private set; }            // 재대결할 때마다 1씩 증가

    [Networked] public NetworkBool WhiteLoaded { get; private set; }
    [Networked] public NetworkBool BlackLoaded { get; private set; }
    [Networked] public NetworkBool WhiteSetupReady { get; private set; }
    [Networked] public NetworkBool BlackSetupReady { get; private set; }

    [Networked] public PostGameChoice WhiteChoice { get; private set; }
    [Networked] public PostGameChoice BlackChoice { get; private set; }

    // ───────────── 내 컴퓨터에서만 쓰는 값 ─────────────
    private const int PresenceCheckInterval = 30;   // 30틱마다 "둘 다 아직 있나?" 확인
    private const float LoadedPingInterval = 1f;

    private readonly SetupBoardModel _model = new SetupBoardModel();   // 방장만 사용
    private IChessBoardAdapter _adapter;
    private bool _movePending;                                         // 방장만: 방금 허락한 수가 아직 적용 전

    private int _appliedRound = -1;
    private GameState? _shownState;
    private bool? _shownWhiteTurn;
    private int _lastSignature = int.MinValue;
    private float _nextLoadedPing;

    public IChessBoardAdapter Adapter => _adapter;

    // ───────────── 만들어질 때 / 사라질 때 ─────────────

    private void Awake()
    {
        // 네트워크 관련 서비스는 Spawned가 아닌 Awake에서 등록한다.
        ScriptM.Register<NetworkGameM>(this, UseSpace.Network);

        _adapter = boardAdapter as IChessBoardAdapter;
        if (_adapter == null)
        {
            Debug.LogError("[NetworkGameM] boardAdapter가 비어 있거나 IChessBoardAdapter가 아니에요. Inspector에서 연결해 주세요.");
        }
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<NetworkGameM>();
    }

    public override void Spawned()
    {
        Instance = this;
        _appliedRound = -1;
        _shownState = null;
        _shownWhiteTurn = null;
        _lastSignature = int.MinValue;

        if (Object.HasStateAuthority) InitializeMatch();

        InstanceReady?.Invoke(this);

        // "저 게임 씬에 들어왔어요" 하고 방장에게 알림 (놓칠 수 있으니 Render에서 주기적으로 다시 보냄)
        _nextLoadedPing = Time.unscaledTime + LoadedPingInterval;
        RPC_ReportLoaded();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this)
        {
            Instance = null;
            InstanceLost?.Invoke();
        }
    }

    // ───────────── 자리/닉네임 도우미 ─────────────

    public bool TryGetColor(PlayerRef player, out PieceColor color)
    {
        if (player != PlayerRef.None)
        {
            if (player == WhitePlayer) { color = PieceColor.white; return true; }
            if (player == BlackPlayer) { color = PieceColor.black; return true; }
        }
        color = default;
        return false;
    }

    public bool TryGetLocalColor(out PieceColor color) => TryGetColor(Runner.LocalPlayer, out color);

    public PlayerRef PlayerOf(PieceColor color) => color == PieceColor.white ? WhitePlayer : BlackPlayer;

    public static PieceColor Opposite(PieceColor color) => color == PieceColor.white ? PieceColor.black : PieceColor.white;

    public string GetNickname(PieceColor color)
    {
        PlayerRef player = PlayerOf(color);
        if (player == PlayerRef.None) return "대기 중...";
        return MatchRoster.TryGetNickname(player, out string nickname) ? nickname : $"Player{player.PlayerId}";
    }

    public bool IsSetupReady(PieceColor color) => color == PieceColor.white ? (bool)WhiteSetupReady : (bool)BlackSetupReady;
    public bool IsLoaded(PieceColor color) => color == PieceColor.white ? (bool)WhiteLoaded : (bool)BlackLoaded;
    public PostGameChoice GetChoice(PieceColor color) => color == PieceColor.white ? WhiteChoice : BlackChoice;

    public bool IsLocalTurn
    {
        get
        {
            if (!TryGetLocalColor(out PieceColor me)) return false;
            bool whiteTurn = IsWhiteTurn;
            return whiteTurn == (me == PieceColor.white);
        }
    }

    public bool IsPresent(PlayerRef player)
    {
        if (player == PlayerRef.None) return false;
        foreach (PlayerRef active in Runner.ActivePlayers)
        {
            if (active == player) return true;
        }
        return false;
    }

    // ───────────── 부탁하기 (누구나 호출 → 방장이 실행) ─────────────

    public void RequestSetupReady(bool ready) => RPC_SetSetupReady(ready);
    public void RequestResign() => RPC_RequestResign();
    public void RequestPostGameChoice(PostGameChoice choice) => RPC_SubmitPostGameChoice((int)choice);

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportLoaded(RpcInfo info = default)
    {
        if (info.Source == WhitePlayer) WhiteLoaded = true;
        else if (info.Source == BlackPlayer) BlackLoaded = true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetSetupReady(bool ready, RpcInfo info = default)
    {
        if (State != GameState.GameStart) return;
        if (!TryGetColor(info.Source, out PieceColor color)) return;

        if (ready && !_model.CanBeReady(color, out string reason))
        {
            Announce(info.Source, reason);
            return;
        }

        if (color == PieceColor.white) WhiteSetupReady = ready;
        else BlackSetupReady = ready;

        bool whiteReady = WhiteSetupReady;
        bool blackReady = BlackSetupReady;
        if (whiteReady && blackReady) BeginPlaying();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestResign(RpcInfo info = default)
    {
        if (State != GameState.Playing) return;
        if (!TryGetColor(info.Source, out PieceColor color)) return;

        EndGame(PlayerOf(Opposite(color)), GameEndReason.Resign);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitPostGameChoice(int choiceId, RpcInfo info = default)
    {
        if (State != GameState.GameEnd) return;
        if (!TryGetColor(info.Source, out PieceColor color)) return;

        var choice = (PostGameChoice)choiceId;
        if (choice != PostGameChoice.Rematch && choice != PostGameChoice.Lobby) return;

        SetChoice(color, choice);

        bool bothRematch = WhiteChoice == PostGameChoice.Rematch && BlackChoice == PostGameChoice.Rematch;
        if (bothRematch) StartRematch();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Notice(int playerId, string message)
    {
        if (Runner != null && Runner.LocalPlayer.PlayerId == playerId) Notice?.Invoke(message);
    }

    /// <summary>방장 → 특정 사람에게만 짧은 안내를 보냅니다.</summary>
    internal void Announce(PlayerRef target, string message)
    {
        if (string.IsNullOrEmpty(message) || target == PlayerRef.None) return;
        RPC_Notice(target.PlayerId, message);
    }

    // ───────────── 방장만 쓰는 "심판" 기능 ─────────────

    private void InitializeMatch()
    {
        _model.Clear();
        _movePending = false;

        ResetClocks();
        IsWhiteTurn = true;
        Winner = PlayerRef.None;
        EndReason = GameEndReason.None;
        MoveCount = 0;
        MatchRound = 0;

        WhiteLoaded = false;
        BlackLoaded = false;
        WhiteSetupReady = false;
        BlackSetupReady = false;
        WhiteChoice = PostGameChoice.None;
        BlackChoice = PostGameChoice.None;

        State = GameState.WaitingForPlayers;
        AssignSeats();
    }

    private void ResetClocks()
    {
        WhiteMainTime = mainTimeSeconds;
        BlackMainTime = mainTimeSeconds;
        CurrentBonusTime = bonusTimeSeconds;
    }

    /// <summary>방(로비)에서 정한 팀대로 흑/백 자리를 정합니다.</summary>
    private void AssignSeats()
    {
        PlayerRef white = PlayerRef.None;
        PlayerRef black = PlayerRef.None;

        if (MatchRoster.TryGetSeat(RoomTeam.White, out MatchSeat whiteSeat) && IsPresent(whiteSeat.Player)) white = whiteSeat.Player;
        if (MatchRoster.TryGetSeat(RoomTeam.Black, out MatchSeat blackSeat) && IsPresent(blackSeat.Player)) black = blackSeat.Player;

        // 로비를 거치지 않고 게임 씬을 직접 실행한 테스트용: 접속 번호가 작은 사람이 백
        if (MatchRoster.Count == 0)
        {
            PlayerRef first = PlayerRef.None;
            PlayerRef second = PlayerRef.None;
            foreach (PlayerRef p in Runner.ActivePlayers)
            {
                if (first == PlayerRef.None || p.PlayerId < first.PlayerId) { second = first; first = p; }
                else if (second == PlayerRef.None || p.PlayerId < second.PlayerId) { second = p; }
            }
            white = first;
            black = second;
        }

        WhitePlayer = white;
        BlackPlayer = black;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        switch (State)
        {
            case GameState.WaitingForPlayers:
                if (WhitePlayer == PlayerRef.None || BlackPlayer == PlayerRef.None) AssignSeats();
                if (WhitePlayer != PlayerRef.None && BlackPlayer != PlayerRef.None)
                {
                    bool whiteLoaded = WhiteLoaded;
                    bool blackLoaded = BlackLoaded;
                    if (whiteLoaded && blackLoaded) EnterSetup();
                }
                break;

            case GameState.Playing:
                TickClock(Runner.DeltaTime);
                break;
        }

        if (Runner.Tick.Raw % PresenceCheckInterval == 0) EnforcePresence();
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!Object.HasStateAuthority) return;
        if (TryGetColor(player, out PieceColor color)) HandleAbsent(color);
    }

    private void EnforcePresence()
    {
        if (State == GameState.WaitingForPlayers) return;

        if (WhitePlayer != PlayerRef.None && !IsPresent(WhitePlayer)) HandleAbsent(PieceColor.white);
        if (BlackPlayer != PlayerRef.None && !IsPresent(BlackPlayer)) HandleAbsent(PieceColor.black);
    }

    private void HandleAbsent(PieceColor absent)
    {
        if (State == GameState.Playing || State == GameState.GameStart)
        {
            EndGame(PlayerOf(Opposite(absent)), GameEndReason.Disconnect);
        }
        SetChoice(absent, PostGameChoice.Lobby);   // 나간 사람은 "로비"를 고른 것으로 처리 → 재대결 불가
    }

    private void EnterSetup()
    {
        _model.Clear();
        WhiteSetupReady = false;
        BlackSetupReady = false;
        State = GameState.GameStart;
    }

    private void BeginPlaying()
    {
        _movePending = false;
        MoveCount = 0;
        IsWhiteTurn = true;   // 흰색부터 시작
        ResetClocks();
        State = GameState.Playing;
    }

    // ───────────── 시계 ─────────────

    private void TickClock(float dt)
    {
        bool timedOut;
        bool whiteTurn = IsWhiteTurn;

        if (whiteTurn) WhiteMainTime = UpdatePlayerTime(WhiteMainTime, dt, out timedOut);
        else BlackMainTime = UpdatePlayerTime(BlackMainTime, dt, out timedOut);

        if (timedOut) OnTimeOut();
    }

    private float UpdatePlayerTime(float mainTime, float dt, out bool timedOut)
    {
        timedOut = false;

        // 1) 기본 시간이 남아 있으면 기본 시간부터 줄어듦
        if (mainTime > 0f) return Mathf.Max(0f, mainTime - dt);

        // 2) 기본 시간을 다 썼으면 초읽기가 줄어듦
        CurrentBonusTime = Mathf.Max(0f, CurrentBonusTime - dt);
        timedOut = CurrentBonusTime <= 0f;
        return 0f;
    }

    private void OnTimeOut()
    {
        if (_movePending) return;   // 방금 허락한 수가 적용되는 중이면 그 수를 먼저 처리

        bool whiteTurn = IsWhiteTurn;
        PieceColor loser = whiteTurn ? PieceColor.white : PieceColor.black;

        if (forfeitOnTimeout) EndGame(PlayerOf(Opposite(loser)), GameEndReason.Timeout);
        else SetTurn(Opposite(loser));
    }

    private void SetTurn(PieceColor next)
    {
        IsWhiteTurn = next == PieceColor.white;
        CurrentBonusTime = bonusTimeSeconds;   // 차례가 바뀔 때마다 초읽기 리셋
    }

    // ───────────── 배치 단계 심사 (NetworkChessPieceM이 호출) ─────────────

    internal bool AuthorityTryPlace(PlayerRef sender, pieceType type, int x, int y, out PieceColor color, out string reason)
    {
        reason = null;
        if (_adapter == null || State != GameState.GameStart || !TryGetColor(sender, out color))
        {
            color = default;
            return false;
        }

        if (IsSetupReady(color))
        {
            reason = "준비를 취소해야 기물을 바꿀 수 있어요.";
            return false;
        }

        SetupBoardModel.PlaceResult result = _model.TryPlace(color, type, x, y, _adapter.GetMaxPieceCount);
        if (result != SetupBoardModel.PlaceResult.Ok)
        {
            reason = SetupBoardModel.Describe(result);
            return false;
        }
        return true;
    }

    internal bool AuthorityTryRemove(PlayerRef sender, int x, int y, out PieceColor color)
    {
        if (State != GameState.GameStart || !TryGetColor(sender, out color) || IsSetupReady(color))
        {
            color = default;
            return false;
        }
        return _model.TryRemove(color, x, y);
    }

    internal bool AuthorityTryClearSide(PlayerRef sender, out PieceColor color)
    {
        if (State != GameState.GameStart || !TryGetColor(sender, out color) || IsSetupReady(color))
        {
            color = default;
            return false;
        }
        _model.ClearSide(color);
        return true;
    }

    // ───────────── 대국 단계 심사 (NetworkChessPieceM이 호출) ─────────────

    /// <summary>수를 두어도 되는지 검사하고, 허락하면 "적용 대기" 상태로 만듭니다.</summary>
    internal bool AuthorityTryBeginMove(PlayerRef sender, int fromX, int fromY, int toX, int toY, out PieceColor mover, out string reason)
    {
        reason = null;
        mover = default;

        if (_adapter == null || State != GameState.Playing || _movePending) return false;
        if (!TryGetColor(sender, out mover)) return false;

        bool whiteTurn = IsWhiteTurn;
        if (whiteTurn != (mover == PieceColor.white))
        {
            reason = "상대방 차례예요.";
            return false;
        }

        if (!SetupBoardModel.InBounds(fromX, fromY) || !SetupBoardModel.InBounds(toX, toY)) return false;

        if (!_adapter.IsLegalMove(mover, fromX, fromY, toX, toY))
        {
            reason = "둘 수 없는 수예요.";
            return false;
        }

        _movePending = true;
        return true;
    }

    /// <summary>모든 컴퓨터가 수를 적용한 직후, 방장이 다음 일을 처리합니다.</summary>
    internal void AuthorityOnMoveApplied(PieceColor mover)
    {
        _movePending = false;
        if (State != GameState.Playing) return;

        MoveCount++;
        PieceColor next = Opposite(mover);

        switch (_adapter.EvaluateOutcome(next))
        {
            case ChessOutcome.Checkmate: EndGame(PlayerOf(mover), GameEndReason.Checkmate); break;
            case ChessOutcome.Stalemate: EndGame(PlayerRef.None, GameEndReason.Stalemate); break;
            case ChessOutcome.Draw: EndGame(PlayerRef.None, GameEndReason.Draw); break;
            default: SetTurn(next); break;
        }
    }

    // ───────────── 게임 끝 / 재대결 ─────────────

    private void EndGame(PlayerRef winner, GameEndReason reason)
    {
        if (State == GameState.GameEnd) return;   // 이미 끝난 게임이면 중복 방지

        _movePending = false;
        Winner = winner;
        EndReason = reason;
        WhiteChoice = PostGameChoice.None;
        BlackChoice = PostGameChoice.None;
        State = GameState.GameEnd;
    }

    private void SetChoice(PieceColor color, PostGameChoice choice)
    {
        if (color == PieceColor.white) WhiteChoice = choice;
        else BlackChoice = choice;
    }

    private void StartRematch()
    {
        if (swapSidesOnRematch)
        {
            PlayerRef white = WhitePlayer;
            WhitePlayer = BlackPlayer;
            BlackPlayer = white;
        }

        _model.Clear();
        _movePending = false;

        MatchRound++;   // 모든 컴퓨터가 이 숫자를 보고 자기 판을 비웁니다
        ResetClocks();
        IsWhiteTurn = true;
        MoveCount = 0;
        Winner = PlayerRef.None;
        EndReason = GameEndReason.None;
        WhiteSetupReady = false;
        BlackSetupReady = false;
        WhiteChoice = PostGameChoice.None;
        BlackChoice = PostGameChoice.None;

        State = GameState.GameStart;
    }

    // ───────────── 모든 컴퓨터가 매 프레임 하는 일 ─────────────

    /// <summary>새 판(라운드)이 시작되었다면 내 판을 비웁니다. (여러 번 불려도 한 번만 실행)</summary>
    internal void EnsureRound(int round)
    {
        if (round <= _appliedRound) return;

        _appliedRound = round;
        _adapter?.ClearBoard();
    }

    public override void Render()
    {
        EnsureRound(MatchRound);

        bool whiteTurn = IsWhiteTurn;
        bool stateChanged = _shownState == null || _shownState.Value != State;
        bool turnChanged = _shownWhiteTurn == null || _shownWhiteTurn.Value != whiteTurn;

        if (stateChanged) _shownState = State;
        if (turnChanged) _shownWhiteTurn = whiteTurn;

        if (State == GameState.Playing && (stateChanged || turnChanged))
        {
            _adapter?.SetSideToMove(whiteTurn ? PieceColor.white : PieceColor.black);
        }

        if (stateChanged) StateChanged?.Invoke(State);

        PingLoadedIfNeeded();

        int signature = ComputeSignature();
        if (signature != _lastSignature)
        {
            _lastSignature = signature;
            ApplyInputPermissions();
            Changed?.Invoke();
        }
    }

    private void PingLoadedIfNeeded()
    {
        if (State != GameState.WaitingForPlayers) return;
        if (Time.unscaledTime < _nextLoadedPing) return;
        if (TryGetLocalColor(out PieceColor me) && IsLoaded(me)) return;

        _nextLoadedPing = Time.unscaledTime + LoadedPingInterval;
        RPC_ReportLoaded();
    }

    /// <summary>지금 단계에서 내가 할 수 있는 입력만 켜 줍니다.</summary>
    private void ApplyInputPermissions()
    {
        if (_adapter == null) return;

        if (!TryGetLocalColor(out PieceColor local))
        {
            _adapter.SetSetupInputEnabled(false, PieceColor.white);
            _adapter.SetPlayInputEnabled(false, PieceColor.white);
            return;
        }

        _adapter.SetSetupInputEnabled(State == GameState.GameStart && !IsSetupReady(local), local);
        _adapter.SetPlayInputEnabled(State == GameState.Playing && IsLocalTurn, local);
    }

    private int ComputeSignature()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + (int)State;
            hash = hash * 31 + MatchRound;
            hash = hash * 31 + MoveCount;
            hash = hash * 31 + (IsWhiteTurn ? 1 : 0);
            hash = hash * 31 + (WhiteLoaded ? 1 : 0);
            hash = hash * 31 + (BlackLoaded ? 1 : 0);
            hash = hash * 31 + (WhiteSetupReady ? 1 : 0);
            hash = hash * 31 + (BlackSetupReady ? 1 : 0);
            hash = hash * 31 + (int)WhiteChoice;
            hash = hash * 31 + (int)BlackChoice;
            hash = hash * 31 + (int)EndReason;
            hash = hash * 31 + WhitePlayer.PlayerId;
            hash = hash * 31 + BlackPlayer.PlayerId;
            hash = hash * 31 + Winner.PlayerId;
            hash = hash * 31 + Mathf.CeilToInt(WhiteMainTime);
            hash = hash * 31 + Mathf.CeilToInt(BlackMainTime);
            hash = hash * 31 + Mathf.CeilToInt(CurrentBonusTime);
            return hash;
        }
    }
}