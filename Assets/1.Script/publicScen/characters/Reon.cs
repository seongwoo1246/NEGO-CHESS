using Cysharp.Threading.Tasks;
using UnityEngine;

public class Reon : Characters
{
    private void Awake()
    {
        ScriptM.Register<Reon>(this, UseSpace.Local);
        // 1. 부모가 가진 변수(characterId, isAIPlayer 등)에 코드로 값을 직접 지정합니다!
        characterId = 1;
        searchDepth = 6;
        isUnlocked = true;
       
       
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<Reon>();
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
