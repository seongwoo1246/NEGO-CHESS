using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방 안 화면 담당: 방 이름, 플레이어 이름/팀, "0/2 준비 완료", 준비(게임 시작) 버튼, 방 나가기.
/// 화면에 보이는 모든 값은 RoomState(공용 게시판)에서 읽어 오고,
/// 버튼을 누르면 RoomState에게 "부탁"만 합니다. (직접 값을 바꾸지 않아요)
///
/// ※ 이 스크립트는 "방 패널" 오브젝트에 붙이세요.
/// </summary>
public class RoomPanelUI : MonoBehaviour
{
    /// <summary>플레이어 한 칸(이름 + 팀 드롭다운)</summary>
    [Serializable]
    public class PlayerSlotView
    {
        public TMP_Text nameText;
        [Tooltip("옵션 순서: 0 = 흑팀, 1 = 백팀")]
        public TMP_Dropdown teamDropdown;
        [Tooltip("준비하면 켜지는 표시 (선택)")]
        public GameObject readyMark;

        public void ShowPlayer(RoomPlayerView player, bool canEditTeam)
        {
            nameText.text = player.IsHost ? $"{player.Nickname} (방장)" : player.Nickname;

            teamDropdown.SetValueWithoutNotify((int)player.Team);   // 화면만 바꾸고, 변경 이벤트는 울리지 않음
            teamDropdown.interactable = player.IsLocal && canEditTeam; // 내 팀만 내가 바꿀 수 있어요

            if (readyMark != null) readyMark.SetActive(player.Ready);
        }

        public void ShowEmpty()
        {
            nameText.text = "대기 중...";
            teamDropdown.interactable = false;
            if (readyMark != null) readyMark.SetActive(false);
        }
    }

    [Header("방 정보")]
    [SerializeField] private TMP_Text roomTitleText;
    [Tooltip("'0/2 준비 완료' 글자. 카운트다운 중에는 '3초 후 시작!'으로 바뀝니다.")]
    [SerializeField] private TMP_Text readyCountText;
    [Tooltip("안내 문구 (선택)")]
    [SerializeField] private TMP_Text statusText;

    [Header("플레이어 칸 (입장 순서대로: 1번, 2번 ...)")]
    [SerializeField] private PlayerSlotView[] slots;

    [Header("버튼")]
    [SerializeField] private Button readyButton;     // 방장: "게임 시작" / 참가자: "준비하기"
    [SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button leaveButton;     // "방 나가기"

    [Header("연결")]
    [SerializeField] private NetworkLauncher launcher;
    [SerializeField] private RoomBrowser browser;
    [SerializeField] private GameObject lobbyPanel;

    private readonly List<RoomPlayerView> _players = new List<RoomPlayerView>(8);
    private RoomState _state;
    private bool _isLeaving;

    // ───────────── 켜기 / 끄기 ─────────────

    private void Awake()
    {
        // 팀 드롭다운: 몇 번째 칸이 바뀌었는지 알 수 있게 연결
        for (int i = 0; i < slots.Length; i++)
        {
            int slotIndex = i;
            slots[i].teamDropdown.onValueChanged.AddListener(value => OnTeamDropdownChanged(slotIndex, value));
        }
    }

    private void OnEnable()
    {
        _isLeaving = false;

        readyButton.onClick.AddListener(OnClickReady);
        leaveButton.onClick.AddListener(OnClickLeave);

        RoomState.InstanceReady += BindState;
        RoomState.InstanceLost += OnRoomLost;

        if (RoomState.Instance != null) BindState(RoomState.Instance);
        else ClearScreen();
    }

    private void OnDisable()
    {
        readyButton.onClick.RemoveListener(OnClickReady);
        leaveButton.onClick.RemoveListener(OnClickLeave);

        RoomState.InstanceReady -= BindState;
        RoomState.InstanceLost -= OnRoomLost;

        UnbindState();
    }

    private void BindState(RoomState state)
    {
        UnbindState();
        _state = state;
        _state.Changed += Refresh;
        Refresh();
    }

    private void UnbindState()
    {
        if (_state != null) _state.Changed -= Refresh;
        _state = null;
    }

    // ───────────── 화면 갱신 ─────────────

    private void Refresh()
    {
        if (_state == null) return;

        _state.GetPlayers(_players);
        RoomPhase phase = _state.Phase;
        bool locked = phase == RoomPhase.Starting;

        roomTitleText.text = _state.RoomNameText;

        // 플레이어 칸
        for (int i = 0; i < slots.Length; i++)
        {
            if (i < _players.Count) slots[i].ShowPlayer(_players[i], !locked);
            else slots[i].ShowEmpty();
        }

        // "n/2 준비 완료" 또는 카운트다운
        readyCountText.text = phase == RoomPhase.Countdown
            ? $"{_state.CountdownSecondsLeft}초 후 시작!"
            : $"{_state.ReadyCount}/{_state.MaxPlayers} 준비 완료";

        if (statusText != null) statusText.text = BuildStatusMessage(phase);

        // 준비 버튼: 방이 가득 찼을 때만 누를 수 있어요 (= 시작 가능한 상태)
        bool meReady = _state.TryGetLocalPlayer(out RoomPlayerView me) && me.Ready;
        readyButton.interactable = !locked && _state.PlayerCount >= _state.MaxPlayers;
        readyButtonText.text = _state.IsHost
            ? (meReady ? "시작 취소" : "게임 시작")
            : (meReady ? "준비 취소" : "준비하기");

        leaveButton.interactable = !locked && !_isLeaving;
    }

    private string BuildStatusMessage(RoomPhase phase)
    {
        switch (phase)
        {
            case RoomPhase.WaitingForPlayers: return "상대방이 들어오길 기다리고 있어요.";
            case RoomPhase.WaitingForReady: return "모두 준비하면 곧 게임이 시작돼요.";
            case RoomPhase.Countdown: return "곧 시작해요! 취소하려면 버튼을 다시 누르세요.";
            default: return "게임을 시작합니다!";
        }
    }

    private void ClearScreen()
    {
        roomTitleText.text = string.Empty;
        readyCountText.text = string.Empty;
        if (statusText != null) statusText.text = string.Empty;
        for (int i = 0; i < slots.Length; i++) slots[i].ShowEmpty();
        readyButton.interactable = false;
    }

    // ───────────── 버튼 / 입력 ─────────────

    private void OnClickReady()
    {
        if (_state == null || !_state.TryGetLocalPlayer(out RoomPlayerView me)) return;
        _state.RequestSetReady(!me.Ready);   // 준비 ↔ 준비 취소
    }

    private void OnTeamDropdownChanged(int slotIndex, int value)
    {
        if (_state == null || slotIndex >= _players.Count || !_players[slotIndex].IsLocal) return;
        _state.RequestSetTeam((RoomTeam)value);
    }

    private void OnClickLeave()
    {
        LeaveAsync().Forget();
    }

    private async UniTaskVoid LeaveAsync()
    {
        if (_isLeaving) return;
        _isLeaving = true;
        leaveButton.interactable = false;

        await launcher.LeaveRoomAsync();
        ReturnToLobby();
    }

    // 연결이 끊기거나 쫓겨나서 방 정보가 사라졌을 때도 로비로 돌아갑니다.
    private void OnRoomLost() => ReturnToLobby();

    private void ReturnToLobby()
    {
        if (!gameObject.activeSelf) return; // 이미 돌아갔다면 두 번 하지 않음

        gameObject.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(true);
        if (browser != null) browser.StartBrowsingAsync().Forget();
    }
}