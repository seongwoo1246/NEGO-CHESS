using TMPro;
using UnityEngine;

public class BoardNotationM : MonoBehaviour
{
    [Header("UI 또는 TextMeshPro 프리팹")]
    [SerializeField] private TextMeshProUGUI textPrefab;

    [Header("라벨 부모 트랜스폼")]
    [SerializeField] private Transform fileContainer; // 열 라벨 (a~h) 부모
    [SerializeField] private Transform rankContainer; // 행 라벨 (1~8) 부모

    private TextMeshProUGUI[] fileTexts = new TextMeshProUGUI[8];
    private TextMeshProUGUI[] rankTexts = new TextMeshProUGUI[8];

    private readonly string[] filesWhite = { "a", "b", "c", "d", "e", "f", "g", "h" };
    private readonly string[] filesBlack = { "h", "g", "f", "e", "d", "c", "b", "a" };

    private readonly string[] ranksWhite = { "1", "2", "3", "4", "5", "6", "7", "8" };
    private readonly string[] ranksBlack = { "8", "7", "6", "5", "4", "3", "2", "1" };

    private void Awake()
    {
        InitLabels();
      if(ScriptM.TryGet<ChessBoardM>(out var boardM))
        {
            SetupNotation(boardM.IsFlipped);
        }
    }

    // 1. 최초 1회 텍스트 오브젝트 동적 생성
    private void InitLabels()
    {
        for (int i = 0; i < 8; i++)
        {
            fileTexts[i] = Instantiate(textPrefab, fileContainer);
            rankTexts[i] = Instantiate(textPrefab, rankContainer);
        }
    }

    // 2. 플레이어 진영(White / Black)에 따라 글자 텍스트 갱신
    public void SetupNotation(bool IsFlipped)
    {
        string[] currentFiles = IsFlipped ? filesBlack : filesWhite; 
        string[] currentRanks = IsFlipped ? ranksBlack : ranksWhite; 

        for (int i = 0; i < 8; i++)
        {
            fileTexts[i].text = currentFiles[i];
            rankTexts[i].text = currentRanks[i];
        }
    }
}
