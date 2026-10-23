using System.Collections.Generic;
using TMPro;
using UnityEngine;

[System.Serializable]
public class Upgrade
{
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
   
    [SerializeField] private TextMeshProUGUI GoldText;

    [SerializeField] public List<Upgrade> upgradeList = new List<Upgrade>();


    private void Awake()
    {
        ScriptM.Register<ReinforceM>(this, UseSpace.Local);
        InitAllUpgrades();
        GoldText.text = $"현재 소유 금액 {singlePlayer.Gold}";
    }

    private void OnDisable()
    {
        ScriptM.Unregister<ReinforceM>();
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
        if (stat.currentLevel >= stat.maxLevel)
        {
            Debug.Log("최대 레벨에 도달했습니다!");
            return;
        }

        if (singlePlayer.Gold >= stat.price)
        {
            singlePlayer.Gold -= stat.price;
            stat.currentLevel++;
            ApplyStatEffect(stat);
            stat.UpdateUI();
            Debug.Log($"강화 완료! 현재 레벨: {stat.currentLevel}, 다음 비용: {stat.price}");
        }
        else
        {
            Debug.Log("골드가 부족합니다!");
        }
    }

    private void ApplyStatEffect(Upgrade stat)
    {
        if (ScriptM.TryGet<HP>(out HP hp))
           

            switch (stat.upgradeType)
        {
            case UpgradeType.MaxHp:  hp.bonusMaxHp += stat.TotalValue;  break;

            case UpgradeType.UseHp: hp.bonusUseHp += stat.TotalValue; break;

            case UpgradeType.MaxCost: singlePlayer.maxCost += stat.TotalValue; break;

            case UpgradeType.Negotiation: singlePlayer.bonusNegoPower += stat.TotalValue; break;

            case UpgradeType.SpawnPawn: singlePlayer.AddMaxPiece(pieceType.Pawn, stat.TotalValue); break;

            case UpgradeType.SpawnBishop: singlePlayer.AddMaxPiece(pieceType.Bishop, stat.TotalValue); break;

            case UpgradeType.SpawnKnight: singlePlayer.AddMaxPiece(pieceType.Knight, stat.TotalValue); break;

            case UpgradeType.SpawnRook: singlePlayer.AddMaxPiece(pieceType.Rook, stat.TotalValue); break;

            case UpgradeType.SpawnQueen: singlePlayer.AddMaxPiece(pieceType.Queen, stat.TotalValue); break;
        }
    }

}
