using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AchievementSlot : MonoBehaviour
{
    [Header("최대값을 가진 AchievementM (비워 두면 자동으로 찾습니다)")]
    [SerializeField] private AchievementM achievement;

    [Header("세오 관련")]
    [SerializeField] Slider seo;
    [SerializeField] TextMeshProUGUI seoText;

    [Header("레온 관련")]
    [SerializeField] Slider reon;
    [SerializeField] TextMeshProUGUI reonText;

    [Header("클로에 관련")]
    [SerializeField] Slider cloe;
    [SerializeField] TextMeshProUGUI cloeText;

    [Header("체력 관련")]
    [SerializeField] Slider hpbar;
    [SerializeField] TextMeshProUGUI hpbarText;

    private void OnEnable()
    {
        Refresh();  
    }

    public void Refresh()
    {
        AchievementM a = FindAchievement();
        if (a == null) return;

        SetBar(seo, seoText, singlePlayer.seoWin, a.MaxSeoWin);
        SetBar(reon, reonText, singlePlayer.reonWin, a.MaxReonWin);
        SetBar(cloe, cloeText, singlePlayer.cloeWin, a.MaxCloeWin);
        SetBar(hpbar, hpbarText, singlePlayer.useHp, a.MaxUseHp);
    }

    private AchievementM FindAchievement()
    {
        if (achievement != null) return achievement;
        achievement = GetComponentInParent<AchievementM>(true);
        if (achievement == null && ScriptM.TryGet<AchievementM>(out AchievementM found)) achievement = found;
        return achievement;
    }

    private static void SetBar(Slider bar, TextMeshProUGUI label, int current, int max)
    {
        if (bar != null) bar.value = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
        if (label != null) label.text = $"{current}/{max}";
    }
}