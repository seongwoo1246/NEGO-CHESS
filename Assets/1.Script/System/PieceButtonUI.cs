using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PieceButtonUI : MonoBehaviour
{
    public PieceData pieceData; 
    private Button button;

    [SerializeField] private TextMeshProUGUI countText;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClickThisButton);
    }

    void OnClickThisButton()
    {
        if(ScriptM.TryGet<CheseGameM>(out CheseGameM gameM))
        gameM.OnClickPieceButton(pieceData);
    }



    // 남은 개수를 받아와서 UI 텍스트를 갱신해 주는 함수
    public void UpdateCountUI(int remainingCount)
    {
        if (countText != null)
        {
            countText.text = $"{pieceData.pieceName} x {remainingCount}";
        }
    }

}
