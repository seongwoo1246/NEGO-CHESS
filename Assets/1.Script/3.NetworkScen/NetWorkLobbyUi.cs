using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

    private readonly List<RoomListItemView> _itemViews = new List<RoomListItemView>();
    private bool _hasNickname;
    private bool _isBusy;

    // ───────────── 시작 준비 ─────────────

    private void Awake()
    {
        nicknameInput.characterLimit = PlayerProfile.MaxNicknameLength;
        roomNameInput.characterLimit = RoomCreateRequest.MaxNameLength;
        roomPasswordInput.characterLimit = RoomCreateRequest.MaxPasswordLength;
        roomPasswordInput.contentType = TMP_InputField.ContentType.Password;
    }

    private void Start()
    {
        nicknameInput.text = PlayerProfile.Nickname;
        _hasNickname = PlayerProfile.HasNickname;
        RefreshButtons();
        ShowMessage(string.Empty);
    }

    // 켜질 때 연결하고, 꺼질 때 풉니다 (짝을 맞추는 것이 중요!)
    private void OnEnable()
    {
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

        if (listStatusText != null)
        {
            bool isEmpty = rooms.Count == 0;
            listStatusText.gameObject.SetActive(isEmpty);
            if (isEmpty) listStatusText.text = browser.HasKeyword ? "검색 결과가 없어요." : "만들어진 방이 없어요.";
        }
    }

    private void OnBrowserStatusChanged(string status)
    {
        if (listStatusText == null || string.IsNullOrEmpty(status)) return;
        listStatusText.gameObject.SetActive(true);
        listStatusText.text = status;
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
        RunRoomOperationAsync(() => launcher.JoinRoomAsync(room, password), "방에 들어가는 중이에요...").Forget();
    }

    /// <summary>만들기/참가 공통 흐름: 잠그기 → 실행 → 결과 처리</summary>
    private async UniTaskVoid RunRoomOperationAsync(Func<UniTask<RoomResult>> operation, string progressMessage)
    {
        if (_isBusy) return;

        SetBusy(true);
        ShowMessage(progressMessage);

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
    }

    private void RefreshButtons()
    {
        createRoomButton.interactable = _hasNickname && !_isBusy;
        searchButton.interactable = !_isBusy;
    }

    private void ShowMessage(string message)
    {
        if (messageText != null) messageText.text = message;
    }
}