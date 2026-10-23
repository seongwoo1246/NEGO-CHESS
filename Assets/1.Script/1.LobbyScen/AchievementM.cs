using System;
using UnityEngine;


public class AchievementM : MonoBehaviour
{
    [SerializeField] GameObject LockedReon;
    [SerializeField] GameObject LockedCloe;

    
    public int MaxSeoWin = 50; 

    
    public event Action<int> OnSeoWinChanged;

    public int SeoWin
    {
        get =>singlePlayer.seoWin;
        set
        {
            if (singlePlayer.seoWin != value)
            {
                singlePlayer.seoWin = value;
                
                OnSeoWinChanged?.Invoke(singlePlayer.seoWin);
            }
        }
    }


    
    public int MaxReonWin = 50;

    public event Action<int> OnReonWinChanged;

    public int ReonWin
    {
        get => singlePlayer.reonWin;
        set
        {
            if (singlePlayer.reonWin != value)
            {
                singlePlayer.reonWin = value;
                OnReonWinChanged?.Invoke(singlePlayer.reonWin);
            }
        }
    }


   
    public int MaxCloeWin = 50;

    public event Action<int> OnCloeWinChanged;

    public int CloeWin
    {
        get => singlePlayer.cloeWin;
        set
        {
            if (singlePlayer.cloeWin != value)
            {
                singlePlayer.cloeWin = value;
                OnCloeWinChanged?.Invoke(singlePlayer.cloeWin);
            }
        }
    }


   
    public int MaxUseHp = 3000;

    public event Action<int> OnUseHpChanged;

    public int UseHp
    {
        get => singlePlayer.useHp;
        set
        {
            if (singlePlayer.useHp != value)
            {
                singlePlayer.useHp = value;
                OnUseHpChanged?.Invoke(singlePlayer.useHp);
            }
        }
    }

    [Header("UI References")]
    [SerializeField] private CanvasGroup panelCanvasGroup; // 패널에 붙은 CanvasGroup
    [SerializeField] private PopupUI popupUIObject;     // ESC로 꺼지는 팝업 UI 오브젝트
    [SerializeField] private GameObject AchievementPanel;

    public void ReturnFromBattle()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;             // 다시 선명하게 보이도록 복구
            panelCanvasGroup.interactable = true;     // 상호작용 활성화
            panelCanvasGroup.blocksRaycasts = true;    // 마우스 클릭 차단 활성화
        }

        // 팝업 UI 복구 (다시 ESC로 제어 가능하게)
        if (popupUIObject != null)
        {
            popupUIObject.enabled = true;
        }

        AchievementPanel.SetActive(false);

        Debug.Log("로비 복귀 완료: 패널 및 팝업 복구");
    }

    private void Awake()
    {
        ScriptM.Register<AchievementM>(this, UseSpace.Global);

        bool isReonUnlocked = PlayerPrefs.GetInt("Character_Unlock_1", 0) == 1;
        bool isCloeUnlocked = PlayerPrefs.GetInt("Character_Unlock_2", 0) == 1;

        if (LockedReon != null) LockedReon.SetActive(!isReonUnlocked); // 해제되었다면 자물쇠 비활성화(!isReonUnlocked -> false)
        if (LockedCloe != null) LockedCloe.SetActive(!isCloeUnlocked);
    }

    private void OnDisable()
    {
        ScriptM.Unregister<AchievementM>();
    }

    

    public void AddSeoWin(int amount)
    {
        SeoWin += amount;
    }
    public void AddReonWin(int amount)
    {
        ReonWin += amount;
    }
    public void AddCloeWin(int amount)
    {
        CloeWin += amount;
    }
    public void AddUseHp(int amount)
    {
        UseHp += amount;
    }
}
