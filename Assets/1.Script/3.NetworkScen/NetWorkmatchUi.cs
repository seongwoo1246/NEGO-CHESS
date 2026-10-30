using Cysharp.Threading.Tasks;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 씬의 화면 담당 ("홀 직원").
///  - 이름표: 검정(위) / 흰색(아래), 시계, 내 차례 표시
///  - 배치 단계: 리셋 / 준비 완료 버튼, 상대 준비 상태
///  - 대국 단계: 기권 버튼
///  - 결과 창: 승/패/무, 이유, "같은 상대와 다시" / "로비로"
/// 화면에 보이는 값은 모두 NetworkGameM(심판)에서 읽고, 버튼은 부탁만 보냅니다.
/// </summary>
public class NetworkMatchUI : MonoBehaviour
{
    [Header("플레이어 이름표 (검정 = 위, 흰색 = 아래)")]
    [SerializeField] private TMP_Text blackNameText;
    [SerializeField] private TMP_Text whiteNameText;
    [SerializeField] private TMP_Text blackTimeText;
    [SerializeField] private TMP_Text whiteTimeText;
    [Tooltip("차례인 쪽에 켜지는 표시 (선택)")]
    [SerializeField] private GameObject blackTurnMark;
    [SerializeField] private GameObject whiteTurnMark;

    [Header("안내 문구")]
    [SerializeField] private TMP_Text bannerText;
    [SerializeField] private TMP_Text toastText;
    [SerializeField, Min(0.5f)] private float toastSeconds = 2.5f;

    [Header("배치 단계")]
    [SerializeField] private GameObject setupPanel;
    [SerializeField] private Button setupResetButton;        // "리셋"
    [SerializeField] private Button setupReadyButton;        // "준비 완료"
    [SerializeField] private TMP_Text setupReadyButtonText;
    [SerializeField] private TMP_Text opponentReadyText;     // "상대: 배치 중... / 준비 완료!"

    [Header("대국 단계")]
    [SerializeField] private GameObject playPanel;
    [SerializeField] private Button resignButton;
    [SerializeField] private TMP_Text resignButtonText;

    [Header("결과 창")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private TMP_Text resultDetailText;
    [SerializeField] private TMP_Text rematchStatusText;
    [SerializeField] private Button rematchButton;           // "같은 상대와 다시"
    [SerializeField] private Button lobbyButton;             // "로비로"

    [Header("설정")]
    [Tooltip("Build Settings에 등록된 로비 씬 이름")]
    [SerializeField] private string lobbySceneName = "MultiLobby";

    private NetworkGameM _game;
    private float _toastHideAt;
    private float _resignArmedUntil;

    // ───────────── 켜기 / 끄기 ─────────────

    private void OnEnable()
    {
        setupResetButton.onClick.AddListener(OnClickReset);
        setupReadyButton.onClick.AddListener(OnClickReady);
        resignButton.onClick.AddListener(OnClickResign);
        rematchButton.onClick.AddListener(OnClickRematch);
        lobbyButton.onClick.AddListener(OnClickLobby);

        NetworkGameM.InstanceReady += Bind;
        NetworkGameM.InstanceLost += Unbind;
        NetworkGameM.Notice += ShowToast;

        if (NetworkGameM.Instance != null) Bind(NetworkGameM.Instance);
        else ShowOnlyBanner("상대방을 기다리는 중이에요...");

        if (toastText != null) toastText.text = string.Empty;
    }

    private void OnDisable()
    {
        setupResetButton.onClick.RemoveListener(OnClickReset);
        setupReadyButton.onClick.RemoveListener(OnClickReady);
        resignButton.onClick.RemoveListener(OnClickResign);
        rematchButton.onClick.RemoveListener(OnClickRematch);
        lobbyButton.onClick.RemoveListener(OnClickLobby);

        NetworkGameM.InstanceReady -= Bind;
        NetworkGameM.InstanceLost -= Unbind;
        NetworkGameM.Notice -= ShowToast;

        Unbind();
    }

    private void Bind(NetworkGameM game)
    {
        Unbind();
        _game = game;
        _game.Changed += Refresh;
        Refresh();
    }

    private void Unbind()
    {
        if (_game != null) _game.Changed -= Refresh;
        _game = null;
    }

    private void Update()
    {
        // 안내 문구는 잠깐 보였다가 사라져요
        if (_toastHideAt > 0f && Time.unscaledTime >= _toastHideAt)
        {
            _toastHideAt = 0f;
            if (toastText != null) toastText.text = string.Empty;
        }

        // "정말 기권?" 확인 시간이 지나면 원래 글자로
        if (_resignArmedUntil > 0f && Time.unscaledTime >= _resignArmedUntil)
        {
            _resignArmedUntil = 0f;
            if (resignButtonText != null) resignButtonText.text = "기권";
        }
    }

    // ───────────── 화면 갱신 ─────────────

    private void Refresh()
    {
        if (_game == null) return;

        GameState state = _game.State;
        bool hasLocalColor = _game.TryGetLocalColor(out PieceColor me);
        bool whiteTurn = _game.IsWhiteTurn;
        bool playing = state == GameState.Playing;

        // 이름표 (검정 = 위, 흰색 = 아래)
        blackNameText.text = _game.GetNickname(PieceColor.black);
        whiteNameText.text = _game.GetNickname(PieceColor.white);

        blackTimeText.text = FormatClock(_game.BlackMainTime, _game.CurrentBonusTime, playing && !whiteTurn);
        whiteTimeText.text = FormatClock(_game.WhiteMainTime, _game.CurrentBonusTime, playing && whiteTurn);

        if (blackTurnMark != null) blackTurnMark.SetActive(playing && !whiteTurn);
        if (whiteTurnMark != null) whiteTurnMark.SetActive(playing && whiteTurn);

        // 단계별 패널
        setupPanel.SetActive(state == GameState.GameStart);
        playPanel.SetActive(playing);
        resultPanel.SetActive(state == GameState.GameEnd);

        switch (state)
        {
            case GameState.WaitingForPlayers:
                ShowOnlyBanner("상대방을 기다리는 중이에요...");
                break;
            case GameState.GameStart:
                RefreshSetup(hasLocalColor, me);
                break;
            case GameState.Playing:
                bannerText.text = _game.IsLocalTurn ? "내 차례예요!" : "상대방 차례예요.";
                break;
            case GameState.GameEnd:
                RefreshResult(hasLocalColor, me);
                break;
        }
    }

    private void RefreshSetup(bool hasLocalColor, PieceColor me)
    {
        if (!hasLocalColor)
        {
            bannerText.text = "관전 중이에요.";
            setupReadyButton.interactable = false;
            setupResetButton.interactable = false;
            return;
        }

        bool myReady = _game.IsSetupReady(me);
        bool oppReady = _game.IsSetupReady(NetworkGameM.Opposite(me));

        bannerText.text = myReady
            ? "상대방이 준비하길 기다리고 있어요."
            : "기물을 배치하고 '준비 완료'를 눌러 주세요.";

        setupReadyButtonText.text = myReady ? "준비 취소" : "준비 완료";
        setupReadyButton.interactable = true;
        setupResetButton.interactable = !myReady;

        if (opponentReadyText != null) opponentReadyText.text = oppReady ? "상대: 준비 완료!" : "상대: 배치 중...";
    }

    private void RefreshResult(bool hasLocalColor, PieceColor me)
    {
        bool isDraw = _game.Winner == PlayerRef.None;
        bool iWon = hasLocalColor && _game.Winner == _game.Runner.LocalPlayer;

        resultTitleText.text = isDraw ? "무승부" : iWon ? "승리!" : "패배";
        resultDetailText.text = DescribeReason(_game.EndReason, iWon);
        bannerText.text = string.Empty;

        if (!hasLocalColor)
        {
            rematchButton.interactable = false;
            if (rematchStatusText != null) rematchStatusText.text = string.Empty;
            return;
        }

        PieceColor opp = NetworkGameM.Opposite(me);
        PostGameChoice mine = _game.GetChoice(me);
        PostGameChoice theirs = _game.GetChoice(opp);
        bool opponentLeft = theirs == PostGameChoice.Lobby || !_game.IsPresent(_game.PlayerOf(opp));

        rematchButton.interactable = !opponentLeft && mine == PostGameChoice.None;

        if (rematchStatusText != null)
        {
            if (opponentLeft) rematchStatusText.text = "상대방이 로비로 나갔어요.";
            else if (mine == PostGameChoice.Rematch) rematchStatusText.text = "상대방의 선택을 기다리는 중...";
            else if (theirs == PostGameChoice.Rematch) rematchStatusText.text = "상대방이 한 번 더 하고 싶어 해요!";
            else rematchStatusText.text = string.Empty;
        }
    }

    private void ShowOnlyBanner(string message)
    {
        setupPanel.SetActive(false);
        playPanel.SetActive(false);
        resultPanel.SetActive(false);
        bannerText.text = message;
    }

    private static string DescribeReason(GameEndReason reason, bool iWon)
    {
        switch (reason)
        {
            case GameEndReason.Checkmate: return "체크메이트";
            case GameEndReason.Stalemate: return "스테일메이트 (둘 수 있는 수가 없어요)";
            case GameEndReason.Draw: return "무승부";
            case GameEndReason.Resign: return iWon ? "상대방이 기권했어요." : "기권했어요.";
            case GameEndReason.Timeout: return iWon ? "상대방의 시간이 다 되었어요." : "시간이 다 되었어요.";
            case GameEndReason.Disconnect: return "상대방의 연결이 끊겼어요.";
            default: return string.Empty;
        }
    }

    private static string FormatClock(float mainTime, float bonusTime, bool isRunning)
    {
        if (mainTime > 0f)
        {
            int seconds = Mathf.CeilToInt(mainTime);
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
        // 기본 시간을 다 썼다면, 시계가 돌아가는 사람에게만 초읽기를 보여줍니다.
        return isRunning ? $"00:00 ({Mathf.CeilToInt(bonusTime)})" : "00:00";
    }

    private void ShowToast(string message)
    {
        if (toastText == null) return;
        toastText.text = message;
        _toastHideAt = Time.unscaledTime + toastSeconds;
    }

    // ───────────── 버튼 ─────────────

    private void OnClickReset()
    {
        if (ScriptM.TryGet<NetworkChessPieceM>(out NetworkChessPieceM pieces)) pieces.RequestClearMySide();
    }

    private void OnClickReady()
    {
        if (_game == null || !_game.TryGetLocalColor(out PieceColor me)) return;
        _game.RequestSetupReady(!_game.IsSetupReady(me));   // 준비 ↔ 준비 취소
    }

    private void OnClickResign()
    {
        if (_game == null) return;

        // 실수로 누르지 않도록 두 번 눌러야 기권됩니다.
        if (Time.unscaledTime > _resignArmedUntil || _resignArmedUntil <= 0f)
        {
            _resignArmedUntil = Time.unscaledTime + 3f;
            if (resignButtonText != null) resignButtonText.text = "정말 기권?";
            return;
        }

        _resignArmedUntil = 0f;
        if (resignButtonText != null) resignButtonText.text = "기권";
        _game.RequestResign();
    }

    private void OnClickRematch()
    {
        if (_game == null) return;
        rematchButton.interactable = false;   // 연타 방지
        _game.RequestPostGameChoice(PostGameChoice.Rematch);
    }

    private void OnClickLobby()
    {
        lobbyButton.interactable = false;
        NetworkRunner runner = _game != null ? _game.Runner : null;
        MatchExit.ReturnToLobbyAsync(runner, lobbySceneName).Forget();
    }
}