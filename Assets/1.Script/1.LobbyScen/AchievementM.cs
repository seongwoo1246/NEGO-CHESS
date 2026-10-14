using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AchievementM : MonoBehaviour
{
    [SerializeField] GameObject LockedReon;
    [SerializeField] GameObject LockedCloe;

    [SerializeField] private int seoWin;
    public int MaxSeoWin = 50; 

    
    public event Action<int> OnSeoWinChanged;

    public int SeoWin
    {
        get => seoWin;
        set
        {
            if (seoWin != value)
            {
                seoWin = value;
                
                OnSeoWinChanged?.Invoke(seoWin);
            }
        }
    }


    [SerializeField] private int reonWin;
    public int MaxReonWin = 50;

    public event Action<int> OnReonWinChanged;

    public int ReonWin
    {
        get => reonWin;
        set
        {
            if (reonWin != value)
            {
                reonWin = value;
                OnReonWinChanged?.Invoke(reonWin);
            }
        }
    }


    [SerializeField] private int cloeWin;
    public int MaxCloeWin = 50;

    public event Action<int> OnCloeWinChanged;

    public int CloeWin
    {
        get => cloeWin;
        set
        {
            if (cloeWin != value)
            {
                cloeWin = value;
                OnCloeWinChanged?.Invoke(cloeWin);
            }
        }
    }


    [SerializeField] private int useHp;
    public int MaxUseHp = 3000;

    public event Action<int> OnUseHpChanged;

    public int UseHp
    {
        get => useHp;
        set
        {
            if (useHp != value)
            {
                useHp = value;
                OnUseHpChanged?.Invoke(useHp);
            }
        }
    }



    private void Awake()
    {
        ScriptM.Register<AchievementM>(this, UseSpace.Local);
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
