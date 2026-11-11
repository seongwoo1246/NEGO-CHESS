using System.Collections.Generic;
using UnityEngine;


public static class singlePlayer 
{
     public static string playerName;
     public static int birthMonth;
     public static int birthDay;
     public static bool isGirl = true;
     public static string gender = "여";
    public static int maxCost = 48;
    public static float bonusNegoPower = 0;
    public static int selectedCharacterId;
    public static int seoWin =0;
    public static int seoTier =0;
    public static int reonWin =0;
    public static int reonTier = 0;
    public static int cloeWin = 0;
    public static int cloeTier = 0;
    public static int hpTier = 0;
    public static int bonusUseHp = 0;
    public static int useHp = 0;
    public static bool hpInitialized = false;
    public static int keepHp;

    public static event System.Action OnHpStatChanged;

    public static Sprite charIcon
    {
        get
        {
            var db = CharacterDB.Db;
            if (db == null) return null;
            var sprites = db.GetSprites(selectedCharacterId);
            int index = 0; // 필요에 따라 조절
            return (sprites != null && sprites.Count > index) ? sprites[index] : null;
        }
    }

    public static Sprite icon
    {
        get
        {
            var db = CharacterDB.Db;
            if (db == null) return null;
            var sprites = db.GetSprites(3);
            int index = isGirl ? 0 : 1;   // 0 = 여, 1 = 남 (기존 코드와 동일)
            return (sprites != null && sprites.Count > index) ? sprites[index] : null;
        }
    }
    private static int _bonusMaxHp = 0;
    public static int bonusMaxHp
    {
        get => _bonusMaxHp;
        set { _bonusMaxHp = value; OnHpStatChanged?.Invoke(); }
    }
    private static int _currentHp;
    public static int currentHp
    {
        get => _currentHp;
        set
        {
            if (_currentHp != value)
              
            _currentHp = value;
        }
    }
   
    public static bool isFristPlay
    {
        get => PlayerPrefs.GetInt("FirstPlayDone", 0) == 0;
        set { PlayerPrefs.SetInt("FirstPlayDone", value ? 0 : 1); PlayerPrefs.Save(); }

    }
            private static int _Gold =999999;
    public static int Gold
    {
        get => _Gold;
        set
        {
            if (_Gold != value)
           
            _Gold = value;
        }
    }


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

                
                return;
            }
        }
        

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

    private static int GetMaxPiece(pieceType type)
    {
        foreach (var p in pieceDataList)
            if (p.Type == type) return p.maxPiece;
        return 0;
    }

    private static void SetMaxPiece(pieceType type, int value)
    {
        if (value <= 0) return;   // 예전 저장 파일에는 이 값이 없어 0이므로 무시
        for (int i = 0; i < pieceDataList.Count; i++)
        {
            if (pieceDataList[i].Type == type)
            {
                PieceData data = pieceDataList[i];
                data.maxPiece = value;
                pieceDataList[i] = data;
                return;
            }
        }
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
        data.seoStep = seoTier;
        data.reonStep = reonTier;
        data.cloeStep = cloeTier;
        data.hpStep = hpTier;
      

        ExportUpgrades(data.upgradeSaveList);   // 강화 레벨 (ReinforceM 없이 저장)

        data.maxPawn = GetMaxPiece(pieceType.Pawn);
        data.maxBishop = GetMaxPiece(pieceType.Bishop);
        data.maxKnight = GetMaxPiece(pieceType.Knight);
        data.maxRook = GetMaxPiece(pieceType.Rook);
        data.maxQueen = GetMaxPiece(pieceType.Queen);

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
        bonusMaxHp = data.bonusMaxHp;
        bonusUseHp = data.bonusUseHp;
        seoTier =  data.seoStep;
        reonTier = data.reonStep;
        cloeTier =  data.cloeStep;
        hpTier =  data.hpStep;


        hpInitialized = true;


        ImportUpgrades(data.upgradeSaveList);

        SetMaxPiece(pieceType.Pawn, data.maxPawn);
        SetMaxPiece(pieceType.Bishop, data.maxBishop);
        SetMaxPiece(pieceType.Knight, data.maxKnight);
        SetMaxPiece(pieceType.Rook, data.maxRook);
        SetMaxPiece(pieceType.Queen, data.maxQueen);
    }

    #endregion
}


