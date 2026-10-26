using UnityEngine;

public class settingUi : MonoBehaviour
{
    [SerializeField] private CanvasGroup panelCanvasGroup; // 패널에 붙은 CanvasGroup
    [SerializeField] private PopupUI popupUIObject;     // ESC로 꺼지는 팝업 UI 오브젝트
   
    private bool isOpen = false;

    

    public void open()
    {
        isOpen = !isOpen;

        if(isOpen)
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
        }
        else
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
        }

    }

   
}
