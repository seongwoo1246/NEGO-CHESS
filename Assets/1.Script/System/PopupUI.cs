using UnityEngine;

public class PopupUI : MonoBehaviour
{
    public bool isOpen = false;

    private void OnEnable()
    {
        if (KeySettingM.Instance != null)
        {
            KeySettingM.Instance.OnOpenMenuTriggered -= OnOpenMenuPerformed;
            KeySettingM.Instance.OnOpenMenuTriggered += OnOpenMenuPerformed;
        }
    }

    private void OnDisable()
    {
        if (KeySettingM.Instance != null)
        {
            KeySettingM.Instance.OnOpenMenuTriggered -= OnOpenMenuPerformed;
        }
    }

    // 키가 눌린 순간에만 단 한 번 호출되는 함수
    public void OnOpenMenuPerformed()
    {
        isOpen = !isOpen;

        if (isOpen)
        {
            gameObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
   
}
