using Cysharp.Threading.Tasks;
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
        SoundService.Current.playSFX("버튼");
        BattlePanel.SetActive(!BattlePanel.activeSelf);

    }
    public void ActiveSelfReinforce()
    {
        SoundService.Current.playSFX("버튼");
        ReinforcePanel.SetActive(!ReinforcePanel.activeSelf);

    }
    public void ActiveSelfAchievement()
    {
        SoundService.Current.playSFX("버튼");
        AchievementPanel.SetActive(!AchievementPanel.activeSelf);

    }
    public void ActiveSelfCharacter()
    {
        SoundService.Current.playSFX("버튼");
        CharacterPanel.SetActive(!CharacterPanel.activeSelf);
    }
  
   
    public void SaveData()
    {
        SoundService.Current.playSFX("버튼");
        SaveM.Save(singlePlayer.CreateSaveData());
        SaveText.text = $"오늘까지의 일은 적어두도록 하겠습니다. {singlePlayer.playerName}";
        if (ScriptM.TryGet<DialogueController>(out DialogueController dialogueController))
        {
            dialogueController.ShowDialogue(SaveText.text);
        }
        
    }

    public void LoadData()
    {
        SoundService.Current.playSFX("버튼");
        singlePlayer.ApplySaveData(SaveM.Load());
        SaveText.text = $"적었던 내용이 여기 있습니다.";
        if (ScriptM.TryGet<DialogueController>(out DialogueController dialogueController))
        {
            dialogueController.ShowDialogue(SaveText.text);
        }
        singlePlayer.currentHp += singlePlayer.keepHp;
        if (ScriptM.TryGet<HP>(out HP hp) && hp != null) hp.ApplyKeptHp();

    }


    public void GoToPractice()
    {
        SoundService.Current.playSFX("버튼");
        SoundService.Current.FadeSound("전투", 3f).Forget();

        if (ScriptM.TryGet<HP>(out HP hp))
            hp.HidePanel();

        if (ScriptM.TryGet<ScenesM>(out ScenesM scenesM))
            scenesM.LoadScene(SceneNumber.BattleSingle);
    }
}
