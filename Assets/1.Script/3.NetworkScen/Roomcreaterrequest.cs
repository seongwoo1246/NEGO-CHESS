using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// "방 만들기 주문서".
/// 방 이름, 비밀번호, 최대 인원을 한곳에 모아 두고,
/// 올바르게 적었는지 스스로 검사(Validate)합니다.
/// </summary>
public readonly struct RoomCreateRequest
{
    // ── 규칙은 여기 한 곳에서만 바꾸면 됩니다 ──
    public const int MinNameLength = 2;
    public const int MaxNameLength = 12;
    public const int MinPasswordLength = 4;
    public const int MaxPasswordLength = 12;

    public string RoomName { get; }
    public string Password { get; }
    public int MaxPlayers { get; }

    /// <summary>비밀번호를 적었는지 (비밀번호는 선택 사항)</summary>
    public bool HasPassword => !string.IsNullOrEmpty(Password);

    //방 만드는 함수
    public RoomCreateRequest(string roomName, string password, int maxPlayers)
    {
        // Trim(): 글자 앞뒤의 실수로 들어간 빈칸을 지워 줍니다.
        RoomName = (roomName ?? string.Empty).Trim();
        Password = (password ?? string.Empty).Trim();
        MaxPlayers = maxPlayers;
    }

    /// <summary>
    /// 주문서가 올바른지 검사합니다.
    /// 올바르면 true, 틀렸으면 false + 화면에 보여줄 안내 문구(errorMessage)를 돌려줍니다.
    /// </summary>
    public bool Validate(out string errorMessage)
    {
        if (RoomName.Length < MinNameLength || RoomName.Length > MaxNameLength)
        {
            errorMessage = $"방 이름은 {MinNameLength}~{MaxNameLength}글자로 적어 주세요.";
            return false;
        }

        if (HasPassword && (Password.Length < MinPasswordLength || Password.Length > MaxPasswordLength))
        {
            errorMessage = $"비밀번호는 {MinPasswordLength}~{MaxPasswordLength}글자로 적어 주세요. (안 쓰려면 비워 두세요)";
            return false;
        }

        if (MaxPlayers < 2)
        {
            errorMessage = "최대 인원은 2명 이상이어야 해요.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 비밀번호를 그대로 저장하지 않고 "지문(해시)"으로 바꿉니다.
    /// 같은 비밀번호는 항상 같은 지문이 나오지만, 지문만 봐서는 원래 비밀번호를 알 수 없어요.
    /// 방 번호(sessionId)를 섞어서, 다른 방과 같은 비밀번호여도 지문이 달라집니다.
    /// </summary>
    public static string HashPassword(string sessionId, string password)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sessionId + ":" + password));
            return Convert.ToBase64String(bytes);
        }
    }
}
