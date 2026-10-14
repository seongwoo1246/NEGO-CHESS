using UnityEngine;
using UnityEngine.UI;

public class LobbySceneM : MonoBehaviour
{
    [Header("로비UI버튼들")]
    [SerializeField] Button BattleBtn;
    [SerializeField] Button PracticeBtn;
    [SerializeField] Button ReinforceBtn;
    [SerializeField] Button AchievementBtn;
    [SerializeField] Button CharacterBtn;
    [SerializeField] Button SettingBtn;
   
    [Header("판넬들")]
    [SerializeField] GameObject BattlePanel;
    [SerializeField] GameObject ReinforcePanel;
    [SerializeField] GameObject AchievementPanel;
    [SerializeField] GameObject CharacterPanel;
    [SerializeField] GameObject SettingPanel;


    private void Start()
    {
        if (BattlePanel != null) BattleBtn.onClick.AddListener(ActiveSelfBattle);
        if (ReinforceBtn != null) ReinforceBtn.onClick.AddListener(ActiveSelfReinforce);
        if (AchievementBtn != null) AchievementBtn.onClick.AddListener(ActiveSelfAchievement);
        if (CharacterBtn != null) CharacterBtn.onClick.AddListener(ActiveSelfCharacter);
        if (SettingBtn != null) SettingBtn.onClick.AddListener(ActiveSelfSetting);
    }

    public void ActiveSelfBattle()
    {
        BattlePanel.SetActive(!BattlePanel.activeSelf);

    }
    public void ActiveSelfReinforce()
    {
        ReinforcePanel.SetActive(!ReinforcePanel.activeSelf);

    }
    public void ActiveSelfAchievement()
    {
        AchievementPanel.SetActive(!AchievementPanel.activeSelf);

    }
    public void ActiveSelfCharacter()
    {
        CharacterPanel.SetActive(!CharacterPanel.activeSelf);

    }
    public void ActiveSelfSetting()
    {
        SettingPanel.SetActive(!SettingPanel.activeSelf);

    }
   



}
