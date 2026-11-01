using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 화면 담당 ("홀 직원"): 닉네임 / 방 만들기 / 방 검색 / 방 목록 / 방 참가.
/// 실제로 방을 여닫는 일은 NetworkLauncher(주방), 목록 받기는 RoomBrowser(게시판)가 합니다.
///
/// ※ 이 스크립트는 "로비 패널" 오브젝트에 붙이세요. (패널이 켜질 때 방 목록을 자동으로 불러옵니다)
/// </summary>
public class NetWorkLobbyUi : MonoBehaviour
{
    [Header("화면 (패널)")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject roomPanel;

    [Header("닉네임")]
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button nicknameConfirmButton;   // "작성 완료"

    [Header("방 만들기")]
    [SerializeField] private TMP_InputField roomNameInput;
    [Tooltip("만들 때는 '새 방 비밀번호', 잠긴 방에 참가할 때는 '입장 비밀번호'로 함께 씁니다.")]
    [SerializeField] private TMP_InputField roomPasswordInput;
    [SerializeField] private Button createRoomButton;         // "생성하기"
    [SerializeField] private int maxPlayers = 2;

    [Header("방 검색 / 목록")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button searchButton;             // "검색하기"
    [SerializeField] private RectTransform roomListContent;   // 목록이 쌓일 부모 (ScrollView의 Content)
    [SerializeField] private RoomListItemView roomItemPrefab;
    [Tooltip("목록이 비었을 때 / 불러오는 중일 때 보여줄 글자 (선택)")]
    [SerializeField] private TMP_Text listStatusText;

    [Header("안내 문구 (선택)")]
    [SerializeField] private TMP_Text messageText;

    [Header("연결")]
    [SerializeField] private NetworkLauncher launcher;
    [SerializeField] private RoomBrowser browser;
    [SerializeField] private Button gotoLobby;

    private readonly List<RoomListItemView> _itemViews = new List<RoomListItemView>();
    private bool _hasNickname;
    private bool _isBusy;
    private string _listStatus = string.Empty;   // 목록 쪽 안내 ("불러오는 중...", "방이 없어요" 등)
    private string _message = string.Empty;      // 동작 결과 안내 ("방을 만드는 중이에요...", 오류 등)
    private float _messageHideAt;                // 0이면 자동으로 사라지지 않음 (진행 중 안내)
    private float _listStatusHideAt;             // 0이면 자동으로 사라지지 않음 (불러오는 중 등)

    private const float ResultMessageSeconds = 3f;

    // ───────────── 시작 준비 ─────────────

    private void Awake()
    {
        ScriptM.Register<NetWorkLobbyUi>(this, UseSpace.Network);
        nicknameInput.characterLimit = PlayerProfile.MaxNicknameLength;
        roomNameInput.characterLimit = RoomCreateRequest.MaxNameLength;
        roomPasswordInput.characterLimit = RoomCreateRequest.MaxPasswordLength;
        roomPasswordInput.contentType = TMP_InputField.ContentType.Password;

        // 안내 글자는 보기만 하는 용도 → 버튼/입력칸 클릭을 가로채지 않게 합니다.
        if (listStatusText != null) listStatusText.raycastTarget = false;
        if (messageText != null) messageText.raycastTarget = false;
    }

    private void Start()
    {
        if (gotoLobby != null)
        {
            gotoLobby.onClick.RemoveAllListeners();
            gotoLobby.onClick.AddListener(GoToLobby);
        }
        nicknameInput.text = PlayerProfile.Nickname;
        _hasNickname = PlayerProfile.HasNickname;
        RefreshButtons();
        ShowMessage(string.Empty);
    }

    // 켜질 때 연결하고, 꺼질 때 풉니다 (짝을 맞추는 것이 중요!)
    private void OnEnable()
    {
        // NetworkLauncher가 ScriptM으로 이 화면을 찾아 쓰므로, 켜질 때마다 등록합니다.
        // (방에 들어갔다 나오면 서비스가 정리되어도, 패널이 다시 켜질 때 다시 등록돼요)
        if (!ScriptM.TryGet<NetWorkLobbyUi>(out NetWorkLobbyUi _)) ScriptM.Register<NetWorkLobbyUi>(this, UseSpace.Network);

        // 방에서 돌아왔을 때 이전 안내 글자가 남아 있지 않도록 깨끗하게 시작
        _listStatus = string.Empty;
        _message = string.Empty;
        _messageHideAt = 0f;
        _listStatusHideAt = 0f;
        RenderStatus();
        RefreshButtons();

        nicknameConfirmButton.onClick.AddListener(OnClickConfirmNickname);
        nicknameInput.onValueChanged.AddListener(OnNicknameEdited);
        createRoomButton.onClick.AddListener(OnClickCreateRoom);
        searchButton.onClick.AddListener(OnClickSearch);
        searchInput.onSubmit.AddListener(OnSearchSubmitted);

        browser.RoomListChanged += OnRoomListChanged;
        browser.StatusChanged += OnBrowserStatusChanged;

        // 로비가 보이는 동안 방 목록을 계속 받아 옵니다.
        browser.StartBrowsingAsync().Forget();
    }

    private void OnDisable()
    {
        nicknameConfirmButton.onClick.RemoveListener(OnClickConfirmNickname);
        nicknameInput.onValueChanged.RemoveListener(OnNicknameEdited);
        createRoomButton.onClick.RemoveListener(OnClickCreateRoom);
        searchButton.onClick.RemoveListener(OnClickSearch);
        searchInput.onSubmit.RemoveListener(OnSearchSubmitted);

        browser.RoomListChanged -= OnRoomListChanged;
        browser.StatusChanged -= OnBrowserStatusChanged;

        if (ScriptM.TryGet<NetWorkLobbyUi>(out NetWorkLobbyUi registered) && registered == this)
        {
            ScriptM.Unregister<NetWorkLobbyUi>();
        }
    }

    private void Update()
    {
        bool changed = false;

        if (_messageHideAt > 0f && Time.unscaledTime >= _messageHideAt)
        {
            _message = string.Empty;
            _messageHideAt = 0f;
            changed = true;
        }

        if (_listStatusHideAt > 0f && Time.unscaledTime >= _listStatusHideAt)
        {
            _listStatus = string.Empty;
            _listStatusHideAt = 0f;
            changed = true;
        }

        if (changed) RenderStatus();
    }

    /// <summary>
    /// NetworkLauncher가 "연결을 시작/끝냈어요" 하고 알려 주는 창구.
    /// 연결 중에는 버튼을 잠그고, 끝나면 풉니다.
    /// </summary>
    public void SetConnecting(bool connecting)
    {
        SetBusy(connecting);
    }

    // ───────────── 닉네임 ─────────────

    private void OnClickConfirmNickname()
    {
        if (!PlayerProfile.TrySetNickname(nicknameInput.text, out string error))
        {
            ShowMessage(error);
            return;
        }

        nicknameInput.text = PlayerProfile.Nickname;
        _hasNickname = true;
        RefreshButtons();
        ShowMessage($"{PlayerProfile.Nickname}님, 반가워요!");
    }

    // 확정한 뒤에 글자를 고치면 "다시 확정해야 하는 상태"로 되돌립니다.
    private void OnNicknameEdited(string text)
    {
        _hasNickname = PlayerProfile.HasNickname && text.Trim() == PlayerProfile.Nickname;
        RefreshButtons();
    }

    // ───────────── 검색 / 목록 ─────────────

    private void OnClickSearch() => browser.SetKeyword(searchInput.text);
    private void OnSearchSubmitted(string text) => browser.SetKeyword(text);

    private void OnRoomListChanged(IReadOnlyList<RoomListItem> rooms)
    {
        // 줄(프리팹)을 필요한 만큼만 만들고, 남는 줄은 숨겨서 재활용합니다.
        while (_itemViews.Count < rooms.Count)
        {
            _itemViews.Add(Instantiate(roomItemPrefab, roomListContent));
        }

        for (int i = 0; i < _itemViews.Count; i++)
        {
            bool inUse = i < rooms.Count;
            _itemViews[i].gameObject.SetActive(inUse);
            if (inUse) _itemViews[i].Bind(rooms[i], OnClickJoinRoom);
        }

        if (_isBusy) return;   // 입장 중에 목록이 비워지는 것은 안내할 필요 없어요

        if (rooms.Count == 0)
        {
            SetListStatus(browser.HasKeyword ? "검색 결과가 없어요." : "만들어진 방이 없어요.", ResultMessageSeconds);
        }
        else
        {
            SetListStatus(string.Empty, 0f);
        }
    }

    private void OnBrowserStatusChanged(string status)
    {
        if (_isBusy) return;
        SetListStatus(status ?? string.Empty, 0f);   // "불러오는 중..." 등은 끝날 때까지 유지
    }

    private void SetListStatus(string text, float visibleSeconds)
    {
        _listStatus = text;
        _listStatusHideAt = visibleSeconds > 0f && !string.IsNullOrEmpty(text) ? Time.unscaledTime + visibleSeconds : 0f;
        RenderStatus();
    }

    /// <summary>
    /// 안내 글자를 화면에 반영합니다. ★ 핵심 규칙: 두 글자 중 "한 번에 하나만" 보입니다.
    ///  1순위: 동작 결과 안내(_message)  2순위: 방 목록 안내(_listStatus)
    /// → 두 글자가 같은 자리에 있어도 겹치지 않아요.
    /// </summary>
    private void RenderStatus()
    {
        bool showMessage = !string.IsNullOrEmpty(_message);
        if (messageText != null)
        {
            messageText.gameObject.SetActive(showMessage);
            if (showMessage) messageText.text = _message;
        }

        bool showList = !showMessage && !_isBusy && !string.IsNullOrEmpty(_listStatus);
        if (listStatusText != null)
        {
            listStatusText.gameObject.SetActive(showList);
            if (showList) listStatusText.text = _listStatus;
        }
    }

    // ───────────── 방 만들기 / 참가 ─────────────

    private void OnClickCreateRoom()
    {
        if (!_hasNickname) { ShowMessage("먼저 닉네임을 정하고 '작성 완료'를 눌러 주세요."); return; }

        var request = new RoomCreateRequest(roomNameInput.text, roomPasswordInput.text, maxPlayers);
        if (!request.Validate(out string error)) { ShowMessage(error); return; }

        RunRoomOperationAsync(() => launcher.CreateRoomAsync(request), "방을 만드는 중이에요...").Forget();
    }

    private void OnClickJoinRoom(RoomListItem room)
    {
        if (!_hasNickname) { ShowMessage("먼저 닉네임을 정하고 '작성 완료'를 눌러 주세요."); return; }

        string password = roomPasswordInput.text;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 비밀번호가 안 맞는다고 나올 때 원인을 찾기 위한 임시 로그 (비밀번호 자체는 찍지 않아요)
        if (room.HasPassword)
        {
            string fingerprint = RoomCreateRequest.HashPassword(room.SessionId, (password ?? string.Empty).Trim());
            Debug.Log($"[Lobby] 입장 비밀번호 확인 | 입력 {(password ?? string.Empty).Trim().Length}글자 | 일치={fingerprint == room.PasswordHash} | 방 지문 '{room.PasswordHash}' / 내 지문 '{fingerprint}'");
        }
#endif

        RunRoomOperationAsync(() => launcher.JoinRoomAsync(room, password), "방에 들어가는 중이에요...").Forget();
    }

    /// <summary>만들기/참가 공통 흐름: 잠그기 → 실행 → 결과 처리</summary>
    private async UniTaskVoid RunRoomOperationAsync(Func<UniTask<RoomResult>> operation, string progressMessage)
    {
        if (_isBusy) return;

        SetBusy(true);
        ShowMessage(progressMessage, 0f);   // 진행 중 안내: 끝날 때까지 유지

        RoomResult result = await operation();

        SetBusy(false);

        if (!result.Success)
        {
            ShowMessage(result.Message);
            browser.StartBrowsingAsync().Forget(); // 실패했으니 방 목록을 다시 받아 옵니다.
            return;
        }

        roomPasswordInput.text = string.Empty;
        ShowMessage(string.Empty);
        EnterRoomScreen();
    }

    private void EnterRoomScreen()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(true);
    }

    // ───────────── 작은 도우미 함수들 ─────────────

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        RefreshButtons();
        RenderStatus();   // 입장 중에는 목록 안내를 숨깁니다
    }

    private void RefreshButtons()
    {
        createRoomButton.interactable = _hasNickname && !_isBusy;
        searchButton.interactable = !_isBusy;
    }

    /// <summary>동작 결과 안내를 보여 줍니다. visibleSeconds가 0이면 다음 안내가 올 때까지 유지합니다.</summary>
    private void ShowMessage(string message, float visibleSeconds = ResultMessageSeconds)
    {
        _message = message ?? string.Empty;
        _messageHideAt = visibleSeconds > 0f && !string.IsNullOrEmpty(_message) ? Time.unscaledTime + visibleSeconds : 0f;
        RenderStatus();
    }

    private void GoToLobby()
    {
        if (ScriptM.TryGet<ScenesM>(out ScenesM scenesM))
        {
            scenesM.LoadScene(SceneNumber.GameStart);
        }
    }

}