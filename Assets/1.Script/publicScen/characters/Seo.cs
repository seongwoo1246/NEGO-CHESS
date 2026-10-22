
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;



public class Seo : Characters
{
    private void Awake()
    {
        ScriptM.Register<Seo>(this,UseSpace.Local);
        // 1. 부모가 가진 변수(characterId, isAIPlayer 등)에 코드로 값을 직접 지정합니다!
        characterId = 0;
        searchDepth = 4;
        isUnlocked = false;
        Debug.Log("서오 캐릭터가 코드로 기본 데이터를 성공적으로 세팅했습니다!");
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<Seo>();
    }

    public override void SetupBoard(CheseGameM gameM)
    {
        base.SetupBoard(gameM);
    }

    public override UniTask OnAITurnAsync()
    {
        return base.OnAITurnAsync();
    }

}

