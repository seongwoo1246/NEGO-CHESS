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
                PrepareForBattle();
                ScriptM.ClearSceneLocalServices();
                scenesM.LoadScene(SceneNumber.Battle);
            }
        }

    }

    [Header("UI References")]
    [SerializeField] private CanvasGroup panelCanvasGroup; // 패널에 붙은 CanvasGroup
    [SerializeField] private PopupUI popupUIObject;     // ESC로 꺼지는 팝업 UI 오브젝트
    [SerializeField] private GameObject AchievementPanel;
    // 1. 배틀 씬으로 넘어가기 직전에 호출할 함수 (다른 매니저나 외부에서 호출)
    public void PrepareForBattle()
    {
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;             // 완전히 투명하게 설정
            panelCanvasGroup.interactable = false;    // 버튼 클릭 등 상호작용 차단
            panelCanvasGroup.blocksRaycasts = false;   // 마우스 클릭이 뒤로 투과되도록 설정
        }

        // 중간에 ESC를 눌러도 팝업이 꺼지지 않도록 비활성화
        if (popupUIObject != null)
        {
            popupUIObject.enabled = false;
        }

        AchievementPanel.SetActive(true);

        Debug.Log("배틀 씬 이동 준비 완료: 패널 숨김 및 팝업 잠금");
    }
}
