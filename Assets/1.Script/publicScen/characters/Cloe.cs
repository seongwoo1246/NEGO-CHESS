using Cysharp.Threading.Tasks;
using UnityEngine;

public class Cloe : Characters
{
    private void Awake()
    {
        ScriptM.Register<Cloe>(this, UseSpace.Local);
        // 1. 부모가 가진 변수(characterId, isAIPlayer 등)에 코드로 값을 직접 지정합니다!
        characterId = 2;
        searchDepth = 7;
        isUnlocked = true;
       
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<Cloe>();
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
