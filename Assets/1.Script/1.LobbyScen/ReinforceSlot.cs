using UnityEngine;
using UnityEngine.UI;

public class ReinforceSlot : MonoBehaviour
{
    [SerializeField] private Upgrade targetStat; // 이 버튼이 담당할 강화 데이터 직접 연결
    [SerializeField] private Button upgradeBtn;

    private void Start()
    {
        upgradeBtn.onClick.AddListener(() => {
            if(ScriptM.TryGet<ReinforceM>(out ReinforceM manager))
            manager.TryUpgrade(targetStat);
        });
    }


}
