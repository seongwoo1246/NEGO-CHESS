using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>프리셋에 저장되는 기물 한 개 (내 진영 기준 좌표와 종류)</summary>
[Serializable]
public class PresetPiece
{
    public int x;
    public int y;
    public int type;   // pieceType을 int로 저장
}

/// <summary>프리셋 한 칸 (이름 + 기물 목록)</summary>
[Serializable]
public class PresetSlot
{
    public string name = "";
    public bool hasData;
    public List<PresetPiece> pieces = new List<PresetPiece>();
}

[Serializable]
public class PresetSaveData
{
    public List<PresetSlot> slots = new List<PresetSlot>();
}

/// <summary>
/// 프리셋을 PlayerPrefs(JSON)에 저장하고 불러옵니다.
/// 게임을 껐다 켜도 남고, 세이브 슬롯(SaveM)과는 별개로 관리됩니다.
/// </summary>
public static class PiecePresetStore
{
    public const int SlotCount = 3;
    private const string Key = "PiecePresets_v1";

    private static PresetSaveData _cache;

    private static PresetSaveData Data
    {
        get
        {
            if (_cache == null) _cache = Load();
            return _cache;
        }
    }

    private static PresetSaveData Load()
    {
        PresetSaveData data = null;
        try
        {
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json)) data = JsonUtility.FromJson<PresetSaveData>(json);
        }
        catch (Exception e)
        {
           
        }

        if (data == null) data = new PresetSaveData();
        if (data.slots == null) data.slots = new List<PresetSlot>();
        while (data.slots.Count < SlotCount) data.slots.Add(new PresetSlot());
        return data;
    }

    public static PresetSlot Get(int index)
    {
        index = Mathf.Clamp(index, 0, SlotCount - 1);
        return Data.slots[index];
    }

    /// <summary>이 칸에 이름과 기물 목록을 저장합니다. (복사본을 저장하므로 원본 리스트는 계속 써도 됩니다)</summary>
    public static void Set(int index, string name, List<PresetPiece> pieces)
    {
        index = Mathf.Clamp(index, 0, SlotCount - 1);

        PresetSlot slot = Data.slots[index];
        slot.name = name ?? "";
        slot.hasData = pieces != null && pieces.Count > 0;
        slot.pieces = new List<PresetPiece>();
        if (pieces != null)
        {
            foreach (PresetPiece p in pieces)
                slot.pieces.Add(new PresetPiece { x = p.x, y = p.y, type = p.type });
        }

        Save();
    }

    public static void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }
}