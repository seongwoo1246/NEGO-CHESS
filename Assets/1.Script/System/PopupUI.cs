using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PopupUI : MonoBehaviour
{
    public bool isOpen = false;

    private void Update()
    {
        // 2. 플레이어가 ESC 키를 누르면 창이 닫히도록 처리
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ClosePopup();
        }
    }

    public void ClosePopup()
    {

        if (isOpen == true)
        {
            gameObject.SetActive(false);
            isOpen = false;
        }
        else
        {
            gameObject.SetActive(true);
            isOpen = true;
        }
        
    }
}
