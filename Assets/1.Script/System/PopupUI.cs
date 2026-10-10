using UnityEngine;
using UnityEngine.UI;

public class PopupUI : MonoBehaviour
{
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (closeButton != null)
        {
            // 1. 닫기 버튼을 누르면 이 창이 닫히도록 연결
            closeButton.onClick.AddListener(ClosePopup);
        }
    }

    private void Update()
    {
        // 2. 플레이어가 ESC 키를 누르면 창이 닫히도록 처리
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClosePopup();
        }
    }

    public void ClosePopup()
    {
        // 팝업 오브젝트를 끄거나 파괴
        gameObject.SetActive(false);
    }
}
