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

        if (seoBtn != null) { seoBtn.onClick.RemoveAllListeners(); seoBtn.onClick.AddListener(() => GoToBattle(0)); }
        if (reonBtn != null) { reonBtn.onClick.RemoveAllListeners(); reonBtn.onClick.AddListener(() => GoToBattle(1)); }
        if (cloeBtn != null) { cloeBtn.onClick.RemoveAllListeners(); cloeBtn.onClick.AddListener(() => GoToBattle(2)); }

        // 1. PlayerPrefs에서 저장된 해제 데이터를 불러와서 자물쇠 UI 적용
        // 예: "Character_Unlock_1" 값이 1 이상이면 레온 해제 (자물쇠 끄기)
        bool isReonUnlocked = PlayerPrefs.GetInt("Character_Unlock_1", 0) == 1;
        bool isCloeUnlocked = PlayerPrefs.GetInt("Character_Unlock_2", 0) == 1;

        if (reonLock != null) reonLock.SetActive(!isReonUnlocked); // 해제되었다면 자물쇠 비활성화(!isReonUnlocked -> false)
        if (cloeLock != null) cloeLock.SetActive(!isCloeUnlocked);
    }

    public void GoToBattle(int characterId)
    {
        singlePlayer.selectedCharacterId = characterId;

        if (ScriptM.TryGet<HP>(out HP hp))
        {
            hp.HidePanel();


            if (ScriptM.TryGet<ScenesM>(out ScenesM scenesM) && hp.TryStartGame())
            {
                ScriptM.ClearSceneLocalServices();
                scenesM.LoadScene(SceneNumber.Battle);
            }
        }

    }
}
