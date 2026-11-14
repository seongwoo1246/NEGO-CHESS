using UnityEngine;

public static class CharacterDB
{
    private static characterData _db;

    public static characterData Db
    {
        get
        {
            if (_db == null)
                _db = Resources.Load<characterData>("Characters/CharacterDatabase");
            return _db;
        }
    }
}