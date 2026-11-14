using System;

/// <summary>
/// 멀티 대국 옵션 (static 보관함).
///  - AllowUpgrades : 방장이 방을 만들 때 정하는 값. 방장 컴퓨터에서만 의미가 있고, NetworkGameM이 시작할 때 읽어 갑니다.
///  - UpgradesActive: 이번 대국에 실제로 적용되는지. 모든 컴퓨터에서 NetworkGameM이 채워 줍니다. (PieceButtonUI가 읽음)
/// </summary>
public static class MatchOptions
{
    /// <summary>[방장] 강화(기물 최대 개수) 적용 여부. 방 만들기 화면의 토글에서 정하세요.</summary>
    public static bool AllowUpgrades;

    /// <summary>[모두] 이번 대국에 강화가 적용되는가</summary>
    public static bool UpgradesActive { get; private set; }

    public static event Action Changed;

    public static void SetActive(bool value)
    {
        if (UpgradesActive == value) return;
        UpgradesActive = value;
        Changed?.Invoke();
    }
}