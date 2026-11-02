using System.Collections.Generic;
using UnityEngine;

public static class singlePlayer 
{
     public static string playerName;
     public static int birthMonth;
     public static int birthDay;
     public static bool isGirl = true;
     public static string gender = "여";
    public static int maxCost = 49;
     public static int Gold = 0;
    public static Sprite icon;
    public static float bonusNegoPower = 0;
    public static int selectedCharacterId;
    public static int seoWin =0;
    public static int reonWin =0;
    public static int cloeWin = 0;
    public static int useHp = 0;
    public static int currentHp =100;
    


    #region 강화를 위한 데이터와 함수
    public static List<PieceData> pieceDataList = new List<PieceData>()
    {
        new PieceData(pieceType.Pawn, PieceColor.white, 1, 8, "pawn"),
        new PieceData(pieceType.Bishop, PieceColor.white, 5, 2, "bishop"),
        new PieceData(pieceType.Knight, PieceColor.white, 5, 2, "knight"),
        new PieceData(pieceType.Rook, PieceColor.white, 5, 2, "rook"),
        new PieceData(pieceType.Queen, PieceColor.white, 10, 1, "queen")
    };
    public static void AddMaxPiece(pieceType type, int amount)
    {
        for (int i = 0; i < pieceDataList.Count; i++)
        {
            if (pieceDataList[i].Type == type)
            {
                PieceData data = pieceDataList[i];
                data.maxPiece += amount;
                pieceDataList[i] = data; // 구조체 값 갱신

                Debug.Log($"{type} 최대 개수가 {amount}만큼 증가했습니다! 현재 최대치: {data.maxPiece}");
                return;
            }
        }
        Debug.LogWarning($"해당 기물 데이터를 찾지 못했습니다: {type}");

    }

    private static readonly Dictionary<UpgradeType, int> upgradeLevels = new Dictionary<UpgradeType, int>();

    public static int GetUpgradeLevel(UpgradeType type)
        => upgradeLevels.TryGetValue(type, out int v) ? v : 0;

    public static void SetUpgradeLevel(UpgradeType type, int level)
        => upgradeLevels[type] = level;

    public static void ExportUpgrades(List<UpgradeSaveData> output)
    {
        output.Clear();
        foreach (var kv in upgradeLevels)
            output.Add(new UpgradeSaveData { upgradeType = kv.Key, currentLevel = kv.Value });
    }

    public static void ImportUpgrades(List<UpgradeSaveData> input)
    {
        upgradeLevels.Clear();   // 새 게임이면 null을 넘겨서 초기화
        if (input == null) return;
        foreach (var d in input) upgradeLevels[d.upgradeType] = d.currentLevel;
    }


    #endregion

    #region 저장과 불러오기를 하기 위한 부분
    public static SaveData CreateSaveData()
    { 
        SaveData data = new SaveData();
        data.gold = Gold;
        data.name = playerName;
        data.month = birthMonth;
        data.day = birthDay;
        data.IsGirl = isGirl;
        data.Gender = gender;
        data.maxcost = maxCost;
        data.pulsNego = bonusNegoPower;
        data.seoWin = seoWin;
        data.reonWin = reonWin;
        data.cloeWin = cloeWin;
        data.useHp = useHp;
        data.Hp = currentHp;
      

        data.upgradeSaveList.Clear();
        foreach (var upgrade in ReinforceM.Instance.upgradeList)
        {
            if (upgrade != null)
            {
                data.upgradeSaveList.Add(new UpgradeSaveData
                {
                    upgradeType = upgrade.upgradeType,
                    currentLevel = upgrade.currentLevel,
                    price = upgrade.price
                });
            }
        }


        return data;
    }

    public static void ApplySaveData(SaveData data)
    {
        // 1. 기본 데이터 복구 (골드, 승리 횟수, 체력 등)
        Gold =data.gold;
        playerName = data.name;
        birthMonth = data.month;
        birthDay = data.day;
        isGirl = data.IsGirl;
        gender = data.Gender;
        maxCost = data.maxcost;
        bonusNegoPower = data.pulsNego;
        seoWin = data.seoWin;
        reonWin = data.reonWin;
        cloeWin = data.cloeWin;
        useHp = data.useHp;
        currentHp = data.Hp;
        

        // 2. 업그레이드 리스트를 순회하며 저장된 레벨을 찾아 꽂아주기
        foreach (var savedUpgrade in data.upgradeSaveList)
        {
            // 내 업그레이드 리스트에서 저장된 타입과 일치하는 항목 찾기
            Upgrade target =ReinforceM.Instance.upgradeList.Find(u => u.upgradeType == savedUpgrade.upgradeType);

            if (target != null)
            {
                target.currentLevel = savedUpgrade.currentLevel; // 레벨 복구
                target.price = savedUpgrade.price;               // 가격 복구 (필요한 경우)
                target.UpdateUI();                               // 💡 UI 텍스트(레벨, 비용) 갱신!
            }
        }
    }

    #endregion
}


