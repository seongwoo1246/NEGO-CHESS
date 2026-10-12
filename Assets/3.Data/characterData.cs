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

    public List<CharacterEntry> characterList = new List<CharacterEntry>();


}
