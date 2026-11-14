using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PieceButtonUI : MonoBehaviour
{
    public PieceData pieceData;
    private Button button;

    [SerializeField] private TextMeshProUGUI countText;

    [Tooltip("멀티 씬에서만 체크하세요. 체크하면 방 옵션(강화 적용)이 켜졌을 때만 강화 수치를 쓰고, 꺼져 있으면 기본 개수를 씁니다. 싱글/AI전 씬은 체크하지 않으면 항상 강화가 적용됩니다.")]
    [SerializeField] private bool multiplayerScene = false;

    private int _baseMax;          // 인스펙터에 적어 둔 기본 최대 개수
    private bool _baseCaptured;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClickThisButton);

        CaptureBase();
        SyncMaxFromPlayer();
    }

    private void OnEnable()
    {
        MatchOptions.Changed += OnOptionsChanged;
        SyncMaxFromPlayer();
    }

    private void OnDisable()
    {
        MatchOptions.Changed -= OnOptionsChanged;
    }

    private void OnOptionsChanged()
    {
        SyncMaxFromPlayer();
        UpdateCountUI(pieceData.maxPiece);   // 옵션은 대국 시작 전에 정해지므로 남은 개수 = 최대 개수
    }

    private void CaptureBase()
    {
        if (_baseCaptured) return;
        _baseMax = pieceData.maxPiece;
        _baseCaptured = true;
    }

    /// <summary>강화로 늘어난 최대 개수를 이 버튼의 pieceData에 반영합니다. (멀티는 옵션이 꺼져 있으면 기본 개수)</summary>
    public void SyncMaxFromPlayer()
    {
        CaptureBase();

        bool useUpgrades = !multiplayerScene || MatchOptions.UpgradesActive;
        if (!useUpgrades)
        {
            pieceData.maxPiece = _baseMax;
            return;
        }

        foreach (PieceData p in singlePlayer.pieceDataList)
        {
            if (p.Type == pieceData.Type)
            {
                pieceData.maxPiece = p.maxPiece;   // 구조체라 필드만 덮어씀 (이름/코스트 등은 그대로)
                break;
            }
        }
    }

    void OnClickThisButton()
    {
        if (ScriptM.TryGet<CheseGameM>(out CheseGameM gameM))
            gameM.OnClickPieceButton(pieceData);
        else if (ScriptM.TryGet<SingleChessGameM>(out SingleChessGameM single))
            single.OnClickPieceButton(pieceData);
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