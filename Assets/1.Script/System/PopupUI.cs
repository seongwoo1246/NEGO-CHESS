using UnityEngine;

public class PopupUI : MonoBehaviour
{
    public bool isOpen = false;

    private void OnEnable()
    {
       
            KeySettingM.OnOpenMenuTriggered -= OnOpenMenuPerformed;
            KeySettingM.OnOpenMenuTriggered += OnOpenMenuPerformed;
        
    }

    private void OnDisable()
    {
          KeySettingM.OnOpenMenuTriggered -= OnOpenMenuPerformed;
        
    }

    // 키가 눌린 순간에만 단 한 번 호출되는 함수
    public void OnOpenMenuPerformed()
    {
        SoundService.Current.playSFX("버튼");
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
