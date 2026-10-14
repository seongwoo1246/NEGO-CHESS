using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AchievementSlot : MonoBehaviour
{
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
        if(ScriptM.TryGet<AchievementM>(out AchievementM service))
        {
            service.OnReonWinChanged += UpdateReonUI;
            service.OnCloeWinChanged += UpdateCloeUI;
            service.OnSeoWinChanged += UpdateSeoUI;
            service.OnUseHpChanged += UpdateUseHpUI;
        }
    }
    private void OnDisable()
    {
        if (ScriptM.TryGet<AchievementM>(out AchievementM service))
        {
            service.OnReonWinChanged -= UpdateReonUI;
            service.OnCloeWinChanged -= UpdateCloeUI;
            service.OnSeoWinChanged -= UpdateSeoUI;
            service.OnUseHpChanged -= UpdateUseHpUI;
        }
    }

    private void UpdateSeoUI(int currentWin)
    {
        if (ScriptM.TryGet<AchievementM>(out AchievementM service))
        {
            float maxWin = service.MaxSeoWin;


            // 슬라이더 비율 갱신 (0 ~ 1 사이 값)
            if (seo != null)
            {
                seo.value = (float)currentWin / maxWin;
            }

            // 텍스트 갱신 (예: "0/50")[cite: 10]
            if (seoText != null)
            {
                seoText.text = $"{currentWin}/{maxWin}";
            }
        }
    }
    private void UpdateReonUI(int currentWin)
    {
        if (ScriptM.TryGet<AchievementM>(out AchievementM service))
        {
            float maxWin = service.MaxReonWin;


            // 슬라이더 비율 갱신 (0 ~ 1 사이 값)
            if (reon != null)
            {
                reon.value = (float)currentWin / maxWin;
            }

            // 텍스트 갱신 (예: "0/50")[cite: 10]
            if (reonText != null)
            {
                reonText.text = $"{currentWin}/{maxWin}";
            }
        }
    }
    private void UpdateCloeUI(int currentWin)
    {
        if (ScriptM.TryGet<AchievementM>(out AchievementM service))
        {
            float maxWin = service.MaxCloeWin;


            // 슬라이더 비율 갱신 (0 ~ 1 사이 값)
            if (cloe != null)
            {
                cloe.value = (float)currentWin / maxWin;
            }

            // 텍스트 갱신 (예: "0/50")[cite: 10]
            if (cloeText != null)
            {
                cloeText.text = $"{currentWin}/{maxWin}";
            }
        }
    }
    private void UpdateUseHpUI(int currentUseHp)
    {
        if (ScriptM.TryGet<AchievementM>(out AchievementM service))
        {
            float maxWin = service.MaxUseHp;


            // 슬라이더 비율 갱신 (0 ~ 1 사이 값)
            if (hpbar != null)
            {
                hpbar.value = (float)currentUseHp / maxWin;
            }

            // 텍스트 갱신 (예: "0/50")[cite: 10]
            if (hpbarText != null)
            {
                hpbarText.text = $"{currentUseHp}/{maxWin}";
            }
        }
    }
}
