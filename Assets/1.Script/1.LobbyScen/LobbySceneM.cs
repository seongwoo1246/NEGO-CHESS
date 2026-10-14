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
    [SerializeField] Button SaveBtn;
    [SerializeField] Button LoadBtn;
    [Header("판넬들")]
    [SerializeField] GameObject BattlePanel;
    [SerializeField] GameObject PracticePanel;
    [SerializeField] GameObject ReinforcePanel;
    [SerializeField] GameObject AchievementPanel;
    [SerializeField] GameObject CharacterPanel;
   



    public void ActiveSelfBattle()
    {
        BattlePanel.SetActive(!BattlePanel.activeSelf);

    }
    public void ActiveSelfPractice()
    {
        PracticePanel.SetActive(!PracticePanel.activeSelf);

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
   



}
