using System.Collections.Generic;
using System.IO;
using TinyJSON;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public class UpgradeSaveData
{
    public UpgradeType upgradeType;
    public int currentLevel;
    public int price;
}

[System.Serializable]
public class SaveData
{
    public int gold;
    public string name;
    public int month;
    public int day;
    public bool IsGirl;
    public string Gender;
    public int maxcost;
    public float pulsNego;
    public int seoWin;
    public int reonWin;
    public int cloeWin;
    public int useHp;
    public int Hp;
    public bool fristPlay;

   

    public List<UpgradeSaveData> upgradeSaveList = new List<UpgradeSaveData>();

}

public static class SaveM
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "chess.json");

    // 1. 게임 데이터 저장하기
    public static void Save(SaveData data)
    {
        // C# 객체를 JSON 형식의 텍스트로 변환 (true를 넣으면 보기 좋게 들여쓰기 됨)
        string json = JsonUtility.ToJson(data, true);

        // 지정된 경로에 파일로 작성
        File.WriteAllText(SavePath, json);
        Debug.Log($"게임 데이터 저장 완료: {SavePath}");
    }

    // 2. 게임 데이터 불러오기
    public static SaveData Load()
    {
        // 파일이 존재하지 않으면 새로 만들어서 반환
        if (!File.Exists(SavePath))
        {
            Debug.Log("저장된 파일이 없습니다. 기본 데이터를 생성합니다.");
            return new SaveData();
        }

        // 파일의 텍스트를 읽어와서 객체로 역직렬화(Deserialize)
        string json = File.ReadAllText(SavePath);
        SaveData data = JsonUtility.FromJson<SaveData>(json);

        Debug.Log("게임 데이터 불러오기 성공!");
        return data;
    }


}
