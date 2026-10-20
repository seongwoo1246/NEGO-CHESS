
using System.Collections.Generic;
using UnityEngine;



public class Seo : Characters
{
    private void Awake()
    {
        // 1. 부모가 가진 변수(characterId, isAIPlayer 등)에 코드로 값을 직접 지정합니다!
        characterId = 101;
        searchDepth = 3;   
        Debug.Log("서오 캐릭터가 코드로 기본 데이터를 성공적으로 세팅했습니다!");
    }
    public override void SetupBoard(CheseGameM gameM)
    {
        base.SetupBoard(gameM);
    }

    public override void OnAITurn()
    {
        base.OnAITurn();
    }

}

