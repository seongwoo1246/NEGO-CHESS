using DG.Tweening;
using System;
using TMPro;
using UnityEngine;

public enum AchievementType { SeoWin = 0, ReonWin = 1, CloeWin = 2, UseHp = 3 }

public class AchievementM : MonoBehaviour
{
    [SerializeField] GameObject LockedReon;
    [SerializeField] GameObject LockedCloe;

    [Header("단계 규칙")]
    [Tooltip("승수 업적: 단계마다 이만큼씩 목표가 늘어남 (10 → 20 → ... )")]
    [SerializeField, Min(1)] private int winStep = 10;
    [Tooltip("체력 업적: 단계마다 이만큼씩 목표가 늘어남 (100 → 200 → ... )")]
    [SerializeField, Min(1)] private int hpStep = 100;
    [Tooltip("총 단계 수. 10이면 승수 100, 체력 1000까지")]
    [SerializeField, Min(1)] private int maxTier = 10;
    [Tooltip("1단계 보상. 단계마다 이만큼씩 늘어남 (5000 → 10000 → ...)")]
    [SerializeField, Min(0)] private int rewardStep = 5000;

    // 슬롯이 읽는 "현재 목표". 단계가 오르면 자동으로 갱신됩니다.
    public int MaxSeoWin = 10;
    public int MaxReonWin = 10;
    public int MaxCloeWin = 10;
    public int MaxUseHp = 100;

    public event Action<int> OnSeoWinChanged;
    public event Action<int> OnReonWinChanged;
    public event Action<int> OnCloeWinChanged;
    public event Action<int> OnUseHpChanged;
    public event Action Refreshed;

    /// <summary>보상을 받았을 때 (받은 골드 합계)</summary>
    public event Action<int> OnRewardGranted;

    [Header("UI References")]
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private PopupUI popupUIObject;
    [SerializeField] private GameObject AchievementPanel;
    [Tooltip("보상 안내 글자 (선택)")]
    [SerializeField] private TMP_Text rewardNoticeText;

    private Sequence _noticeSeq;

    private static readonly AchievementType[] Types =
    {
        AchievementType.SeoWin, AchievementType.ReonWin,
        AchievementType.CloeWin, AchievementType.UseHp
    };

    // ───────────── 켜기 ─────────────

    private void Awake()
    {
        ScriptM.Register<AchievementM>(this, UseSpace.Local);
        ReturnFromBattle();

        bool isReonUnlocked = PlayerPrefs.GetInt("Character_Unlock_1", 0) == 1;
        bool isCloeUnlocked = PlayerPrefs.GetInt("Character_Unlock_2", 0) == 1;
        if (LockedReon != null) LockedReon.SetActive(!isReonUnlocked);
        if (LockedCloe != null) LockedCloe.SetActive(!isCloeUnlocked);
    }

    private void OnEnable()
    {
        SoundService.Current.playSFX("강화");
        ClaimAllRewards();   // 1) 달성한 업적이 있으면 보상 + 다음 단계로
        ApplyTargets();      // 2) 슬롯이 읽을 목표 갱신
        RefreshAll();        // 3) 화면 갱신
        Refreshed?.Invoke();
    }

    private void OnDisable()
    {
        _noticeSeq?.Kill();   // 패널이 꺼질 때 연출 정리
        _noticeSeq = null;
    }
    public void ReturnFromBattle()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }
        if (popupUIObject != null) popupUIObject.enabled = true;
        if (AchievementPanel != null) AchievementPanel.SetActive(false);
    }

    public void RefreshAll()
    {
        OnSeoWinChanged?.Invoke(singlePlayer.seoWin);
        OnReonWinChanged?.Invoke(singlePlayer.reonWin);
        OnCloeWinChanged?.Invoke(singlePlayer.cloeWin);
        OnUseHpChanged?.Invoke(singlePlayer.useHp);
    }

    // ───────────── 단계 / 목표 / 보상 ─────────────

    public bool IsMaxed(AchievementType type) => GetTier(type) >= maxTier;

    /// <summary>지금 단계의 목표치. 모든 단계를 끝냈다면 마지막 목표를 그대로 보여 줍니다.</summary>
    public int GetTarget(AchievementType type)
    {
        int tier = Mathf.Min(GetTier(type), maxTier - 1);
        return StepOf(type) * (tier + 1);
    }

    /// <summary>해당 단계를 깼을 때의 보상 (0단계 = 5000, 1단계 = 10000, ...)</summary>
    public int GetReward(int tier) => rewardStep * (tier + 1);

    /// <summary>달성한 업적마다 보상을 주고, 단계를 올리고, 진행도를 0으로 되돌립니다.</summary>
    public int ClaimAllRewards()
    {
        int total = 0;

        for (int i = 0; i < Types.Length; i++)
        {
            AchievementType type = Types[i];
            int tier = GetTier(type);
            if (tier >= maxTier) continue;                       // 이미 끝까지 깸
            if (GetProgress(type) < GetTarget(type)) continue;   // 아직 못 깸

            total += GetReward(tier);
            SetTier(type, tier + 1);   // 단계와 진행도를 함께 바꿔서 중복 지급을 막음
            SetProgress(type, 0);
        }

        ShowRewardNotice(total);

        if (total <= 0) return 0;

        singlePlayer.Gold += total;
        SaveM.Save(singlePlayer.CreateSaveData());
        OnRewardGranted?.Invoke(total);
        return total;
    }

    private void ApplyTargets()
    {
        MaxSeoWin = GetTarget(AchievementType.SeoWin);
        MaxReonWin = GetTarget(AchievementType.ReonWin);
        MaxCloeWin = GetTarget(AchievementType.CloeWin);
        MaxUseHp = GetTarget(AchievementType.UseHp);
    }

    private int StepOf(AchievementType type) => type == AchievementType.UseHp ? hpStep : winStep;

    private void ShowRewardNotice(int total)
    {
        if (rewardNoticeText == null) return;

        _noticeSeq?.Kill();                        // 이전 연출이 남아 있으면 정리

        if (total <= 0)
        {
            rewardNoticeText.gameObject.SetActive(false);
            return;
        }

        rewardNoticeText.text = $"업적 보상 +{total:N0}원!";
        rewardNoticeText.gameObject.SetActive(true);

        Color c = rewardNoticeText.color;
        c.a = 0f;
        rewardNoticeText.color = c;                // 투명하게 시작

        _noticeSeq = DOTween.Sequence()
            .Append(rewardNoticeText.DOFade(1f, 0.25f))   // 나타남
            .AppendInterval(1.5f)                          // 유지
            .Append(rewardNoticeText.DOFade(0f, 0.5f))    // 사라짐
            .OnComplete(() => rewardNoticeText.gameObject.SetActive(false))
            .SetUpdate(true);                              // TimeScale 0이어도 동작
    }

    // ───────────── singlePlayer 값 읽기/쓰기 (할당 없음) ─────────────

    public int GetTier(AchievementType type)
    {
        int raw;
        switch (type)
        {
            case AchievementType.SeoWin: raw = singlePlayer.seoTier; break;
            case AchievementType.ReonWin: raw = singlePlayer.reonTier; break;
            case AchievementType.CloeWin: raw = singlePlayer.cloeTier; break;
            default: raw = singlePlayer.hpTier; break;
        }
        return Mathf.Clamp(raw, 0, maxTier);   // 잘못된 저장값 방어
    }

    private static void SetTier(AchievementType type, int value)
    {
        switch (type)
        {
            case AchievementType.SeoWin: singlePlayer.seoTier = value; break;
            case AchievementType.ReonWin: singlePlayer.reonTier = value; break;
            case AchievementType.CloeWin: singlePlayer.cloeTier = value; break;
            default: singlePlayer.hpTier = value; break;
        }
    }

    private static int GetProgress(AchievementType type)
    {
        switch (type)
        {
            case AchievementType.SeoWin: return singlePlayer.seoWin;
            case AchievementType.ReonWin: return singlePlayer.reonWin;
            case AchievementType.CloeWin: return singlePlayer.cloeWin;
            default: return singlePlayer.useHp;
        }
    }

    private static void SetProgress(AchievementType type, int value)
    {
        switch (type)
        {
            case AchievementType.SeoWin: singlePlayer.seoWin = value; break;
            case AchievementType.ReonWin: singlePlayer.reonWin = value; break;
            case AchievementType.CloeWin: singlePlayer.cloeWin = value; break;
            default: singlePlayer.useHp = value; break;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 세오 승수를 목표까지 채우기")]
    private void DebugFillSeo() => singlePlayer.seoWin = GetTarget(AchievementType.SeoWin);

    [ContextMenu("테스트: 체력 사용량을 목표까지 채우기")]
    private void DebugFillHp() => singlePlayer.useHp = GetTarget(AchievementType.UseHp);
#endif
}