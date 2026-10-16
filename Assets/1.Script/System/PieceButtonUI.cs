using UnityEngine;
using UnityEngine.UI;

public class PieceButtonUI : MonoBehaviour
{
    public PieceData pieceData; 
    private Button button;

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
}
