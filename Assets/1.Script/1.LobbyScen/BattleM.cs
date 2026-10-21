using UnityEngine;
using UnityEngine.UI;

public class BattleM : MonoBehaviour
{
    [Header("이동 버튼")]
    public Button seoBtn;
    public Button reonBtn;
    public Button cloeBtn;

    [Header("잠금 장치")]
    public GameObject reonLock;
    public GameObject cloeLock;

    private void Start()
    {
        if (reonLock != null) reonLock.SetActive(!singlePlayer.isSeoWin);
        if (cloeLock != null) cloeLock.SetActive(!singlePlayer.isReonWin);

        if (seoBtn != null) { seoBtn.onClick.RemoveAllListeners(); seoBtn.onClick.AddListener(() => GoToBattle(0)); }
        if (reonBtn != null) { reonBtn.onClick.RemoveAllListeners(); reonBtn.onClick.AddListener(() => GoToBattle(1)); }
        if (cloeBtn != null) { cloeBtn.onClick.RemoveAllListeners(); cloeBtn.onClick.AddListener(() => GoToBattle(2)); }
    }

    public void GoToBattle(int characterId)
    {
        singlePlayer.selectedCharacterId = characterId;

        if(ScriptM.TryGet<HP>(out HP hp))
        {
            hp.HidePanel();
            hp.UseHp();
        }

        if(ScriptM.TryGet<ScenesM>(out ScenesM scenesM))
        {
            scenesM.LoadScene(SceneNumber.Battle);
        }
    }
}
