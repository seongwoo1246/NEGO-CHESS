using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

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
///  GameStart         : 각자 기물 배치 (상대에게는 안 보임!) → 둘 다 "준비 완료"
///  Playing           : 시작 순간 서로의 배치를 공개 → 흰색부터 번갈아 대국 (시계 작동)
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

    [Header("진행 방식")]
    [Tooltip("켜면 한 수를 둔 즉시 차례가 넘어갑니다. 끄면 이동 후 '턴 넘기기(Enter)'를 눌러야 넘어갑니다.")]
    [SerializeField] private bool autoEndTurnAfterMove = true;
    [Tooltip("게임 시작 직후, 서로의 배치를 보여 주며 시계를 멈추는 시간(초)")]
    [SerializeField, Min(0f)] private float revealSeconds = 3f;

    [Header("재대결")]
    [Tooltip("켜면 재대결 때 흑/백을 서로 바꿉니다.")]
    [SerializeField] private bool swapSidesOnRematch = true;
    [SerializeField] private Toggle swapToggle;

    [Header("연결")]
    [Tooltip("IChessBoardAdapter를 구현한 컴포넌트 (예: SingleChessGameM)")]
    [SerializeField] private MonoBehaviour boardAdapter;

    public event Action HostClosedRoom;

    public bool IsNetReady => Object != null && Object.IsValid;// 차례가 넘어갈 때마다 +1 (규칙 금지 기간 계산 기준)
    public int TurnNumber => IsNetReady ? (TurnSerial + 1) / 2 : 0;       // UI의 "현재 턴": 흰색이 움직일 때마다 +1
    // ───────────── 네트워크로 공유되는 값 ─────────────
    [Networked] public NetworkBool UpgradesEnabled { get; private set; }   // 방장이 정한 "강화 적용" 옵션
    [Networked] public NetworkBool SwapSidesOnRematch { get; private set; }
    [Networked] public PlayerRef WhitePlayer { get; private set; }
    [Networked] public PlayerRef BlackPlayer { get; private set; }
    [Networked] public GameState State { get; private set; }
    [Networked] public NetworkBool IsWhiteTurn { get; private set; }

    [Networked] public float WhiteMainTime { get; private set; }
    [Networked] public float BlackMainTime { get; private set; }
    [Networked] public float CurrentBonusTime { get; private set; }

    [Networked] public PlayerRef Winner { get; private set; }          // None이면 무승부
    [Networked] public GameEndReason EndReason { get; private set; }

    [Networked] public NetworkBool TurnMoved { get; private set; }     // 이번 차례에 이미 기물을 움직였나
    [Networked] public TickTimer RevealTimer { get; private set; }     // 공개 시간 동안 시계/입력 정지
    [Networked] public NetworkBool PromotionPending { get; private set; }   // 폰이 끝줄에 닿아 승급 선택을 기다리는 중

    [Networked] public int MoveCount { get; private set; }
    [Networked] public int MatchRound { get; private set; }            // 재대결할 때마다 1씩 증가

    [Networked] public NetworkBool WhiteLoaded { get; private set; }
    [Networked] public NetworkBool BlackLoaded { get; private set; }
    [Networked] public NetworkBool WhiteSetupReady { get; private set; }
    [Networked] public NetworkBool BlackSetupReady { get; private set; }

    [Networked] public PostGameChoice WhiteChoice { get; private set; }
    [Networked] public PostGameChoice BlackChoice { get; private set; }
    [Networked] public NetworkBool NegotiationPending { get; private set; }   // 무르기 협상 중: 시계/이동 정지
    [Networked] public int TurnSerial { get; private set; }


    // ───────────── 내 컴퓨터에서만 쓰는 값 ─────────────
    private const int PresenceCheckInterval = 30;   // 30틱마다 "둘 다 아직 있나?" 확인
    private const float LoadedPingInterval = 1f;
    private const int RevealChunkSize = 64;         // 한 번에 공개하는 기물 수 (1개 = 4바이트)

    private readonly List<SetupBoardModel.PlacedPiece> _revealBuffer = new List<SetupBoardModel.PlacedPiece>(32);

    private readonly SetupBoardModel _model = new SetupBoardModel();   // 방장만 사용
    private IChessBoardAdapter _adapter;
    private bool _movePending;                                         // 방장만: 방금 허락한 수가 아직 적용 전
    private int _promotionX, _promotionY;                              // 방장만: 승급을 기다리는 폰의 칸
    private bool _promotionInFlight;                                   // 방장만: 승급 선택을 허락해서 적용 대기 중

    private int _appliedRound = -1;
    private GameState? _shownState;
    private bool? _shownWhiteTurn;
    private int _lastSignature = int.MinValue;
    private float _nextLoadedPing;

    public IChessBoardAdapter Adapter => _adapter;
    public bool AutoEndTurnAfterMove => autoEndTurnAfterMove;

    private readonly Dictionary<pieceType, int>[] _maxReported =            // 방장만: 색별로 신고받은 최대 개수
   {
    new Dictionary<pieceType, int>(),   // 백
    new Dictionary<pieceType, int>()    // 흑
};
    private int _sentMaxRound = -1;                                        // 내가 이 라운드에 신고했는지

    [Networked] public int WhiteNegoBonus { get; private set; }            // 강화로 얻은 협상력 보너스 (신고받은 값)
    [Networked] public int BlackNegoBonus { get; private set; }

    /// <summary>그 색의 협상력 보너스. 강화 옵션이 꺼져 있으면 0</summary>
    public float GetNegoBonus(PieceColor color) =>
        UpgradesEnabled ? (color == PieceColor.white ? WhiteNegoBonus : BlackNegoBonus) : 0f;
    private static readonly pieceType[] ReportTypes =
        { pieceType.Pawn, pieceType.Knight, pieceType.Bishop, pieceType.Rook, pieceType.Queen };
    // ───────────── 만들어질 때 / 사라질 때 ─────────────

    private void Awake()
    {
        // 네트워크 관련 서비스는 Spawned가 아닌 Awake에서 등록한다.
        ScriptM.Register<NetworkGameM>(this, UseSpace.Network);

        _adapter = boardAdapter as IChessBoardAdapter;
        if (_adapter == null)
        { }
      
    }

    private void OnDestroy()
    {
        MatchOptions.SetActive(false);
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
        if (Object.HasStateAuthority) SwapSidesOnRematch = swapSidesOnRematch;
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

    /// <summary>게임 시작 직후 "상대 배치 공개" 시간인가요? (이 동안은 시계가 멈추고 아무도 둘 수 없어요)</summary>
    public bool IsRevealing => RevealTimer.IsRunning && !RevealTimer.Expired(Runner);

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
    public void RequestEndTurn() => RPC_RequestEndTurn();
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
        SoundService.Current.playSFX("버튼");
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
        SoundService.Current.playSFX("버튼");
        if (State != GameState.Playing) return;
        if (!TryGetColor(info.Source, out PieceColor color)) return;

        SoundService.Current.FadeSound("로비", 3f).Forget();
        EndGame(PlayerOf(Opposite(color)), GameEndReason.Resign);
    }

    /// <summary>"턴 넘기기(Enter)": 이번 차례에 이미 기물을 움직인 사람만 차례를 넘길 수 있어요.</summary>
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestEndTurn(RpcInfo info = default)
    {
        SoundService.Current.playSFX("턴이동");
        if (State != GameState.Playing || _movePending || IsRevealing || NegotiationPending) return;
        if (!TryGetColor(info.Source, out PieceColor color)) return;

        bool whiteTurn = IsWhiteTurn;
        if (whiteTurn != (color == PieceColor.white)) return;   // 내 차례가 아님
        if (PromotionPending) { Announce(info.Source, "승급할 기물을 먼저 골라 주세요."); return; }
        if (!TurnMoved) { Announce(info.Source, "먼저 기물을 움직여 주세요."); return; }

        SetTurn(Opposite(color));
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitPostGameChoice(int choiceId, RpcInfo info = default)
    {
        SoundService.Current.playSFX("버튼");
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

    /// <summary>
    /// 게임 시작 때 방장이 "양쪽 배치"를 한꺼번에 공개합니다.
    /// 각 컴퓨터는 이미 가지고 있는 내 기물은 건너뛰고, 상대 기물만 판 위에 올립니다.
    /// (한 기물 = 4바이트: x, y, 종류, 흰색 여부)
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RevealPieces(int round, byte[] packed)
    {
        SoundService.Current.playSFX("강화");
        EnsureRound(round);
        if (_adapter == null || packed == null || !TryGetLocalColor(out PieceColor me)) return;

        bool iAmWhite = me == PieceColor.white;
        for (int i = 0; i + 3 < packed.Length; i += 4)
        {
            bool pieceIsWhite = packed[i + 3] == 1;
            if (pieceIsWhite == iAmWhite) continue;   // 내 기물은 이미 판 위에 있어요

            _adapter.PlacePiece((pieceType)packed[i + 2], pieceIsWhite ? PieceColor.white : PieceColor.black, packed[i], packed[i + 1]);
        }
    }

    private void RevealAllPieces()
    {
        _model.CopyPieces(_revealBuffer);

        for (int start = 0; start < _revealBuffer.Count; start += RevealChunkSize)
        {
            int count = Mathf.Min(RevealChunkSize, _revealBuffer.Count - start);
            var packed = new byte[count * 4];

            for (int i = 0; i < count; i++)
            {
                SetupBoardModel.PlacedPiece piece = _revealBuffer[start + i];
                int o = i * 4;
                packed[o] = (byte)piece.X;
                packed[o + 1] = (byte)piece.Y;
                packed[o + 2] = (byte)piece.Type;
                packed[o + 3] = (byte)(piece.Color == PieceColor.white ? 1 : 0);
            }

            RPC_RevealPieces(MatchRound, packed);
        }
    }

    /// <summary>방장 → 특정 사람에게만 짧은 안내를 보냅니다.</summary>
    internal void Announce(PlayerRef target, string message)
    {
        if (string.IsNullOrEmpty(message) || target == PlayerRef.None) return;
        RPC_Notice(target.PlayerId, message);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_CloseRoom()
    {

        SoundService.Current.FadeSound("로비", 3f).Forget();
        if (Object.HasStateAuthority) return;   // 방장 본인은 이미 나가는 중
        HostClosedRoom?.Invoke();
    }
    // ───────────── 방장만 쓰는 "심판" 기능 ─────────────

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ReportMaxCounts(byte[] counts, int negoBonus, RpcInfo info = default)
    {
        if (!UpgradesEnabled || counts == null || counts.Length != ReportTypes.Length) return;
        if (State != GameState.GameStart) return;
        if (!TryGetColor(info.Source, out PieceColor color)) return;

        Dictionary<pieceType, int> table = _maxReported[color == PieceColor.white ? 0 : 1];
        for (int i = 0; i < ReportTypes.Length; i++) table[ReportTypes[i]] = counts[i];

        int bonus = Mathf.Clamp(negoBonus, 0, 1000);   // 협상력은 어차피 0~100으로 잘립니다
        if (color == PieceColor.white) WhiteNegoBonus = bonus;
        else BlackNegoBonus = bonus;
    }

    /// <summary>그 색이 그 기물을 최대 몇 개 놓을 수 있나. (강화 옵션이 켜져 있으면 신고받은 값, 아니면 기본값)</summary>
    private int MaxCountFor(PieceColor color, pieceType type)
    {
        if (UpgradesEnabled && _maxReported[color == PieceColor.white ? 0 : 1].TryGetValue(type, out int v)) return v;
        return _adapter.GetMaxPieceCount(type);
    }
    private void InitializeMatch()
    {
       
        UpgradesEnabled = RoomState.Instance != null && RoomState.Instance.UpgradesEnabled;
        _maxReported[0].Clear();
        _maxReported[1].Clear();
        WhiteNegoBonus = 0;
        BlackNegoBonus = 0;
        _model.Clear();
        _movePending = false;
        ClearPromotion();

        ResetClocks();
        IsWhiteTurn = true;
        Winner = PlayerRef.None;
        EndReason = GameEndReason.None;
        TurnSerial = 0;
        MoveCount = 0;
        MatchRound = 0;
        TurnMoved = false;
        RevealTimer = default;
        NegotiationPending = false;
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

    internal bool AuthorityCanBeginNegotiation(PieceColor color, out string reason)
    {
        reason = null;
        if (_adapter == null || State != GameState.Playing) { reason = "대국 중에만 할 수 있어요."; return false; }
        if (NegotiationPending) { reason = "이미 협상이 진행 중이에요."; return false; }
        if (IsRevealing || _movePending || _promotionInFlight || PromotionPending) { reason = "지금은 할 수 없어요."; return false; }
        if (IsWhiteTurn != (color == PieceColor.white)) { reason = "내 차례에만 할 수 있어요."; return false; }
        if (TurnMoved) { reason = "기물을 움직이기 전에만 할 수 있어요."; return false; }
        return true;
    }

    internal void AuthoritySetNegotiating(bool value) => NegotiationPending = value;

    /// <summary>협상 종료. 무르기가 성사됐다면 되돌린 수만큼 MoveCount를 줄이고, 아직 내 차례(움직이기 전)로 둡니다.</summary>
    internal void AuthorityOnNegotiationEnded(int undonePlies)
    {
        NegotiationPending = false;
        if (undonePlies > 0)
        {
            MoveCount = Mathf.Max(0, MoveCount - undonePlies);
            TurnMoved = false;
            ClearPromotion();
        }
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
                if (!IsRevealing) TickClock(Runner.DeltaTime);   // 공개 시간 동안은 시계 정지
                if (!IsRevealing && !NegotiationPending) TickClock(Runner.DeltaTime);
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
        RevealAllPieces();    // 지금까지 숨겨져 있던 서로의 배치를 공개

        TurnSerial = 0;
        NegotiationPending = false;
        _movePending = false;
        ClearPromotion();
        MoveCount = 0;
        TurnMoved = false;
        IsWhiteTurn = true;   // 흰색부터 시작
        ResetClocks();
        RevealTimer = revealSeconds > 0f ? TickTimer.CreateFromSeconds(Runner, revealSeconds) : default;
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
        SoundService.Current.playSFX("턴이동");
        if (_movePending || _promotionInFlight) return;   // 방금 허락한 수/승급이 적용되는 중이면 먼저 처리

        // 승급을 고르다가 시간이 다 되면 퀸으로 자동 승급
        if (PromotionPending)
        {
            AuthorityPromote(pieceType.Queen);
            return;
        }

        bool whiteTurn = IsWhiteTurn;
        PieceColor loser = whiteTurn ? PieceColor.white : PieceColor.black;

        if (forfeitOnTimeout) EndGame(PlayerOf(Opposite(loser)), GameEndReason.Timeout);
        else SetTurn(Opposite(loser));
    }

    private void SetTurn(PieceColor next)
    {
        TurnSerial++;
        TurnMoved = false;
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
       
        PieceColor placeColor = color;
        SetupBoardModel.PlaceResult result = _model.TryPlace(color, type, x, y, t => MaxCountFor(placeColor, t));

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

        if (_adapter == null || State != GameState.Playing || _movePending || NegotiationPending) return false;
        if (!TryGetColor(sender, out mover)) return false;

        if (IsRevealing)
        {
            reason = "상대방의 배치를 공개하는 중이에요. 잠시만 기다려 주세요.";
            return false;
        }

        bool whiteTurn = IsWhiteTurn;
        if (whiteTurn != (mover == PieceColor.white))
        {
            reason = "상대방 차례예요.";
            return false;
        }

        if (PromotionPending)
        {
            reason = "승급할 기물을 먼저 골라 주세요.";
            return false;
        }

        if (TurnMoved)
        {
            reason = "이미 기물을 움직였어요. 턴 넘기기를 눌러 주세요.";
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
            default:
                if (_adapter.TryFindPromotion(mover, out int promoX, out int promoY))
                {
                    // 폰이 끝줄에 닿았어요: 기물을 고를 때까지 차례를 넘기지 않고 기다립니다.
                    _promotionX = promoX;
                    _promotionY = promoY;
                    _promotionInFlight = false;
                    PromotionPending = true;
                }
                else if (autoEndTurnAfterMove) SetTurn(next);   // 바로 상대 차례
                else TurnMoved = true;                          // 이동은 끝, "턴 넘기기"를 기다림
                break;
        }
    }

    // ───────────── 프로모션 심사 (NetworkChessPieceM이 호출) ─────────────

    /// <summary>승급 선택이 올바른지 검사하고, 허락하면 "적용 대기" 상태로 만듭니다.</summary>
    internal bool AuthorityTryPromote(PlayerRef sender, int typeId, out int x, out int y, out PieceColor color, out string reason)
    {
        reason = null;
        x = _promotionX;
        y = _promotionY;
        color = default;

        if (_adapter == null || State != GameState.Playing || !PromotionPending || _promotionInFlight) return false;
        if (!TryGetColor(sender, out color)) return false;

        bool whiteTurn = IsWhiteTurn;
        if (whiteTurn != (color == PieceColor.white)) return false;   // 내 차례의 승급이 아님

        var type = (pieceType)typeId;
        if (type != pieceType.Queen && type != pieceType.Rook && type != pieceType.Bishop && type != pieceType.Knight)
        {
            reason = "그 기물로는 승급할 수 없어요.";
            return false;
        }

        _promotionInFlight = true;
        return true;
    }

    /// <summary>방장이 직접 승급시킵니다. (시간 초과 시 자동 승급용)</summary>
    private void AuthorityPromote(pieceType type)
    {
        if (!ScriptM.TryGet<NetworkChessPieceM>(out NetworkChessPieceM pieces)) return;

        bool whiteTurn = IsWhiteTurn;
        _promotionInFlight = true;
        pieces.AuthorityBroadcastPromotion(_promotionX, _promotionY, type, whiteTurn ? PieceColor.white : PieceColor.black);
    }

    /// <summary>모든 컴퓨터가 승급을 적용한 직후, 방장이 차례를 넘깁니다.</summary>
    internal void AuthorityOnPromotionApplied(PieceColor mover)
    {
        ClearPromotion();
        if (State != GameState.Playing) return;

        if (autoEndTurnAfterMove) SetTurn(Opposite(mover));
        else TurnMoved = true;
    }

    private void ClearPromotion()
    {
        PromotionPending = false;
        _promotionInFlight = false;
    }

    // ───────────── 게임 끝 / 재대결 ─────────────

    private void EndGame(PlayerRef winner, GameEndReason reason)
    {
        if (State == GameState.GameEnd) return;   // 이미 끝난 게임이면 중복 방지

        NegotiationPending = false;
        _movePending = false;
        ClearPromotion();
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

    private void RefreshSwapToggle()
    {
        SoundService.Current.playSFX("버튼");
        if (swapToggle == null) return;

        bool show = State == GameState.GameEnd && Object.HasStateAuthority;   // 방장 = 상태 권한자
        swapToggle.gameObject.SetActive(show);
        if (!show) return;

        swapToggle.SetIsOnWithoutNotify(SwapSidesOnRematch);

        // 방장이 재대결을 누른 뒤에는 색 변경 잠금
        bool lockedIn = TryGetLocalColor(out PieceColor me) && GetChoice(me) == PostGameChoice.Rematch;
        swapToggle.interactable = !lockedIn;
    }

    public void SetSwapSidesOnRematch(bool value)
    {
        SoundService.Current.playSFX("버튼");
        if (!Object.HasStateAuthority || State != GameState.GameEnd) return;
        if (TryGetLocalColor(out PieceColor me) && GetChoice(me) == PostGameChoice.Rematch) return;   // 잠금
        SwapSidesOnRematch = value;
    }

    private void StartRematch()
    {
        if (SwapSidesOnRematch)
        {
            PlayerRef white = WhitePlayer;
            WhitePlayer = BlackPlayer;
            BlackPlayer = white;
        }
        _maxReported[0].Clear();
        _maxReported[1].Clear();
        WhiteNegoBonus = 0;
        BlackNegoBonus = 0;
        _model.Clear();
        _movePending = false;
        ClearPromotion();
        TurnSerial = 0;
        NegotiationPending = false;
        MatchRound++;   // 모든 컴퓨터가 이 숫자를 보고 자기 판을 비웁니다
        ResetClocks();
        IsWhiteTurn = true;
        MoveCount = 0;
        TurnMoved = false;
        RevealTimer = default;
        Winner = PlayerRef.None;
        EndReason = GameEndReason.None;
        WhiteSetupReady = false;
        BlackSetupReady = false;
        WhiteChoice = PostGameChoice.None;
        BlackChoice = PostGameChoice.None;

        State = GameState.GameStart;
    }

    /// <summary>방장이 "로비로" 누를 때 호출 → 상대도 같이 나가게 함</summary>
    public void RequestCloseRoom()
    {
        if (!Object.HasStateAuthority) return;
        RPC_CloseRoom();
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
        MatchOptions.SetActive(UpgradesEnabled);

        // 배치 단계에 들어오면(라운드마다 한 번) 내 최대 개수를 방장에게 신고
        if (UpgradesEnabled && State == GameState.GameStart && _sentMaxRound != MatchRound && _adapter != null)
        {
            
            _sentMaxRound = MatchRound;
            var counts = new byte[ReportTypes.Length];
            for (int i = 0; i < ReportTypes.Length; i++)
                counts[i] = (byte)Mathf.Clamp(_adapter.GetMaxPieceCount(ReportTypes[i]), 0, 255);
            RPC_ReportMaxCounts(counts, Mathf.RoundToInt(singlePlayer.bonusNegoPower));
        }
        
        RuleBans.CurrentTurn = TurnSerial;

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
            RefreshSwapToggle();
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
            _adapter.SetPromotionChoiceVisible(false, PieceColor.white);
            return;
        }

        bool promoting = State == GameState.Playing && PromotionPending;
        bool turnMoved = TurnMoved;

        _adapter.SetSetupInputEnabled(State == GameState.GameStart && !IsSetupReady(local), local);
        _adapter.SetPlayInputEnabled(State == GameState.Playing && !IsRevealing && IsLocalTurn && !turnMoved && !promoting && !NegotiationPending, local );
        _adapter.SetPromotionChoiceVisible(promoting && IsLocalTurn, local);   // 승급 선택창은 이동한 본인에게만
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
            hash = hash * 31 + (TurnMoved ? 1 : 0);
            hash = hash * 31 + (State == GameState.Playing && IsRevealing ? 1 : 0);
            hash = hash * 31 + (PromotionPending ? 1 : 0);
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
            hash = hash * 31 + (NegotiationPending ? 1 : 0);
            hash = hash * 31 + TurnSerial;
            return hash;
        }
    }
}