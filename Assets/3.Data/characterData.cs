using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct CharacterEntry
{
    public int characterId;          // 키 역할 (예: 캐릭터 번호)
    public List<Sprite> sprites;     // 값 역할 (스프라이트 리스트)
}

[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Character Data/Character Database")]
public class characterData : ScriptableObject
{
    // 1. 유니티 인스펙터에서 눈으로 보며 채워 넣는 용도 (데이터 저장소)
    public List<CharacterEntry> characterList = new List<CharacterEntry>();

    // 2. 코드가 실행될 때 딕셔너리로 변환해서 빠르게 찾기 위한 캐시 딕셔너리
    private Dictionary<int, List<Sprite>> characterDictionary;

    // 딕셔너리를 초기화하고 빌드하는 함수
    public void InitializeDictionary()
    {
        if (characterDictionary != null) return;

        characterDictionary = new Dictionary<int, List<Sprite>>();
        foreach (var entry in characterList)
        {
            if (!characterDictionary.ContainsKey(entry.characterId))
            {
                characterDictionary.Add(entry.characterId, entry.sprites);
            }
        }
    }

    // 코드에서 편하게 딕셔너리처럼 꺼내 쓰는 함수
    public List<Sprite> GetSprites(int id)
    {
        

        if (characterDictionary.TryGetValue(id, out List<Sprite> sprites))
        {
            return sprites; // 딕셔너리 방식으로 빠르게 반환!
        }

        Debug.LogWarning("해당 ID를 찾을 수 없습니다: " + id);
        return null;
    }
}
