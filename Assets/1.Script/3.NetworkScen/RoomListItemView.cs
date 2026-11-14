using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 방 목록의 "한 줄" (프리팹에 붙입니다).
/// 방 이름, 인원(1/2), 자물쇠 표시, "참가" 버튼을 보여줍니다.
/// </summary>
public class RoomListItemView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_InputField passward;
    [Tooltip("비밀번호가 있는 방에서만 켜지는 자물쇠 그림 (선택)")]
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_Text joinButtonText;

    private RoomListItem _item;
    private Action<RoomListItem, string> _onJoin;
    private string _boundSessionId;

    private void Awake()
    {
        joinButton.onClick.AddListener(HandleJoinClicked);
    }

    private void OnDestroy()
    {
        joinButton.onClick.RemoveListener(HandleJoinClicked);
    }

    /// <summary>이 줄에 방 정보를 채워 넣습니다. (줄을 재활용하므로 여러 번 불려요)</summary>
    public void Bind(RoomListItem item, Action<RoomListItem, string> onJoin)
    {
        // 줄을 재활용하므로, 다른 방으로 바뀌었을 때만 이전에 적은 비밀번호를 지웁니다.
        if (_boundSessionId != item.SessionId)
        {
            _boundSessionId = item.SessionId;
            if (passward != null) passward.text = string.Empty;
        }

        _item = item;
        _onJoin = onJoin;

        nameText.text = item.RoomName;
        countText.text = $"{item.PlayerCount}/{item.MaxPlayers}";
        if (lockIcon != null) lockIcon.SetActive(item.HasPassword);
        if (passward != null) passward.gameObject.SetActive(item.HasPassword); // 비번 있는 방만 입력칸 표시

        joinButton.interactable = item.CanJoin;
        if (joinButtonText != null)
        {
            joinButtonText.text = !item.IsOpen ? "진행 중" : item.IsFull ? "가득 참" : "참가";
        }
    }


    private void HandleJoinClicked()
    {
        string pw = passward != null ? passward.text : string.Empty;
        _onJoin?.Invoke(_item, pw);
    }
}