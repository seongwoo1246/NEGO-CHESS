using TMPro;
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
    [SerializeField] GameObject ReinforcePanel;
    [SerializeField] GameObject AchievementPanel;
    [SerializeField] GameObject CharacterPanel;

    [SerializeField] TextMeshProUGUI SaveText;


    private void Start()
    {
        if (BattlePanel != null) BattleBtn.onClick.AddListener(ActiveSelfBattle);
        if (ReinforceBtn != null) ReinforceBtn.onClick.AddListener(ActiveSelfReinforce);
        if (AchievementBtn != null) AchievementBtn.onClick.AddListener(ActiveSelfAchievement);
        if (CharacterBtn != null) CharacterBtn.onClick.AddListener(ActiveSelfCharacter);
        if (PracticeBtn != null) PracticeBtn.onClick.AddListener(GoToPractice);
        if (SaveBtn != null) SaveBtn.onClick.AddListener(SaveData);
        if (LoadBtn != null) LoadBtn.onClick.AddListener(LoadData);
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
  
   
    public void SaveData()
    {
        SaveM.Save(singlePlayer.CreateSaveData());
        SaveText.text = $"오늘까지의 일은 적어두도록 하겠습니다. {singlePlayer.playerName}";
        if (ScriptM.TryGet<DialogueController>(out DialogueController dialogueController))
        {
            dialogueController.ShowDialogue(SaveText.text);
        }
        
    }

    public void LoadData()
    {
        
        singlePlayer.ApplySaveData(SaveM.Load());
        SaveText.text = $"적었던 내용이 여기 있습니다.";
        if (ScriptM.TryGet<DialogueController>(out DialogueController dialogueController))
        {
            dialogueController.ShowDialogue(SaveText.text);
        }
    }


    public void GoToPractice()
    {
        if (ScriptM.TryGet<HP>(out HP hp))
            hp.HidePanel();

        if (ScriptM.TryGet<ScenesM>(out ScenesM scenesM))
            scenesM.LoadScene(SceneNumber.BattleSingle);
    }
}
