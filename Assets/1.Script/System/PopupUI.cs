using UnityEngine;
using UnityEngine.InputSystem;

public class PopupUI : MonoBehaviour
{
    public bool isOpen = false;

    [Header("Input Action Asset")]
    [SerializeField] private InputActionAsset inputActions;

    private InputAction openMenuAction;

    private void Awake()
    {
        if (inputActions != null)
        {
            openMenuAction = inputActions.FindAction("OpenMenu");
        }
    }

    private void OnEnable()
    {
        if (openMenuAction != null)
        {
            openMenuAction.Enable();
            // 💡 핵심: 키가 눌린 순간(performed) 실행될벤트를 등록합니다.
            openMenuAction.performed += OnOpenMenuPerformed;
        }
    }

    private void OnDisable()
    {
        if (openMenuAction != null)
        {
            // 💡 메모리 누수 방지를 위해 비활성화될 때 이벤트를 꼭 해제해 줍니다.
            openMenuAction.performed -= OnOpenMenuPerformed;
            openMenuAction.Disable();
        }
    }

    // 키가 눌린 순간에만 단 한 번 호출되는 함수
    private void OnOpenMenuPerformed(InputAction.CallbackContext context)
    {
        ClosePopup();
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
