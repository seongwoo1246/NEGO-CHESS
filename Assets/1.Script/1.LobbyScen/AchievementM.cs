using System;
using UnityEngine;


public class AchievementM : MonoBehaviour
{
    [SerializeField] GameObject LockedReon;
    [SerializeField] GameObject LockedCloe;

    
    public int MaxSeoWin = 50; 

    public event Action<int> OnSeoWinChanged;

    public int MaxReonWin = 50;

    public event Action<int> OnReonWinChanged;

    public int MaxCloeWin = 50;

    public event Action<int> OnCloeWinChanged;

    public int MaxUseHp = 3000;

    public event Action<int> OnUseHpChanged;

    

    [Header("UI References")]
    [SerializeField] private CanvasGroup panelCanvasGroup; // 패널에 붙은 CanvasGroup
    [SerializeField] private PopupUI popupUIObject;     // ESC로 꺼지는 팝업 UI 오브젝트
    [SerializeField] private GameObject AchievementPanel;

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

    private void Awake()
    {
        ScriptM.Register<AchievementM>(this, UseSpace.Local);
        ReturnFromBattle();
        bool isReonUnlocked = PlayerPrefs.GetInt("Character_Unlock_1", 0) == 1;
        bool isCloeUnlocked = PlayerPrefs.GetInt("Character_Unlock_2", 0) == 1;

        if (LockedReon != null) LockedReon.SetActive(!isReonUnlocked); // 해제되었다면 자물쇠 비활성화(!isReonUnlocked -> false)
        if (LockedCloe != null) LockedCloe.SetActive(!isCloeUnlocked);
    }

    private void OnEnable()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        OnSeoWinChanged?.Invoke(singlePlayer.seoWin);
        OnReonWinChanged?.Invoke(singlePlayer.reonWin);
        OnCloeWinChanged?.Invoke(singlePlayer.cloeWin);
        OnUseHpChanged?.Invoke(singlePlayer.useHp);
    }
   
}
