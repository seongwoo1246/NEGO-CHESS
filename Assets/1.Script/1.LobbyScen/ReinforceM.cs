using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class Upgrade
{
    [SerializeField] public Button button;
    public UpgradeType upgradeType;
    [SerializeField]private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI priceText;
    public int currentLevel =0 ;
    public int maxLevel;    // 최대 강화 횟수
    public int price;       // 업그레이드 비용
    public int pricestep;
    public int pulse;       // 증가하는 수치 (체력 +10, 폰 +1 등)

    public int FinalPrice => price + (currentLevel * pricestep);
    public int TotalValue => currentLevel * pulse;

    public void UpdateUI()
    {
        if (levelText != null) levelText.text = $"{currentLevel} / {maxLevel}";
        if (priceText != null) priceText.text = FinalPrice.ToString();
    }

}




public class ReinforceM : MonoBehaviour
{
    public static ReinforceM Instance;

    [SerializeField] private TextMeshProUGUI GoldText;

    [SerializeField] public List<Upgrade> upgradeList = new List<Upgrade>();

    [SerializeField] private TextMeshProUGUI notion;


    private void Awake()
    {
        if(Instance == null) { Instance = this; }
        // 리스트의 각 항목에 구매 버튼 연결 (한 번만)
        foreach (var u in upgradeList)
        {
            Upgrade up = u;   // 람다용 복사
            if (up.button != null) up.button.onClick.AddListener(() => TryUpgrade(up));
        }
    }
    private void OnEnable()
    {
        ScriptM.Register<ReinforceM>(this, UseSpace.Local);
        foreach (var u in upgradeList)
            u.currentLevel = singlePlayer.GetUpgradeLevel(u.upgradeType);   // 저장된 레벨 반영
        InitAllUpgrades();
        RefreshGold();
    }
    private void OnDisable()
    {
        ScriptM.Unregister<ReinforceM>();
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    private void RefreshGold()
    {
        SoundService.Current.playSFX("돈변화");
        if (GoldText != null) GoldText.text = $"현재 소유 금액 {singlePlayer.Gold}";
    }
    public void InitAllUpgrades()
    {
        foreach (var upgrade in upgradeList)
        {
            // 각 업그레이드 초기화 로직 실행
            upgrade.UpdateUI();
        }
    }
    public void TryUpgrade(Upgrade stat)
    {
        SoundService.Current.playSFX("강화");
        if (stat.currentLevel >= stat.maxLevel)
        {
            notion.text = "최대 레벨에 도달했습니다!";
            return;
        }

        int cost = stat.FinalPrice;
        if (singlePlayer.Gold < cost)
        {
            notion.text = "골드가 부족합니다!";
            return;
        }

        singlePlayer.Gold -= cost;
        stat.currentLevel++;
        singlePlayer.SetUpgradeLevel(stat.upgradeType, stat.currentLevel);
        ApplyStatEffect(stat);
        stat.UpdateUI();
        RefreshGold();
        
    }

    private void ApplyStatEffect(Upgrade stat)
    {
      

        switch (stat.upgradeType)
        {
            case UpgradeType.MaxHp: singlePlayer.bonusMaxHp += stat.pulse;  break;
            case UpgradeType.UseHp: singlePlayer.bonusUseHp += stat.pulse; break;
            case UpgradeType.MaxCost: singlePlayer.maxCost += stat.pulse; break;
            case UpgradeType.Negotiation: singlePlayer.bonusNegoPower += stat.pulse; break;
            case UpgradeType.SpawnPawn: singlePlayer.AddMaxPiece(pieceType.Pawn, stat.pulse); break;
            case UpgradeType.SpawnBishop: singlePlayer.AddMaxPiece(pieceType.Bishop, stat.pulse); break;
            case UpgradeType.SpawnKnight: singlePlayer.AddMaxPiece(pieceType.Knight, stat.pulse); break;
            case UpgradeType.SpawnRook: singlePlayer.AddMaxPiece(pieceType.Rook, stat.pulse); break;
            case UpgradeType.SpawnQueen: singlePlayer.AddMaxPiece(pieceType.Queen, stat.pulse); break;
        }
    }

}
