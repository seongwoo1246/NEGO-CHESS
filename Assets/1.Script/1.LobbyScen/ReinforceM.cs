using UnityEngine;

[System.Serializable]
public class Upgrade
{
    public int maxLevel;    // 최대 강화 횟수
    public int price;       // 업그레이드 비용
    public int pulse;       // 증가하는 수치 (체력 +10, 폰 +1 등)
}




public class ReinforceM : MonoBehaviour
{
    [SerializeField] private int currentGold;

    [Header("체력관련")]
    [SerializeField] private Upgrade MaxHp;
    [SerializeField] private Upgrade UseHp;

    [Header("코스트관련")]
    [SerializeField] private Upgrade MaxCost;
    
    [Header("협상관련")]
    [SerializeField] private Upgrade NegoUp;

    [Header("체스 말 관련")]
    [SerializeField] private Upgrade MaxPawnUp;
    [SerializeField] private Upgrade MaxBishopUp;
    [SerializeField] private Upgrade MaxKnightUp;
    [SerializeField] private Upgrade MaxRookUp;
    [SerializeField] private Upgrade MaxQueenUp;


    private void Awake()
    {
        ScriptM.Register<ReinforceM>(this, UseSpace.Local);
    }

    private void OnDisable()
    {
        ScriptM.Unregister<ReinforceM>();
    }

    public void TryUpgrade(Upgrade stat)
    {
        if (currentGold >= stat.price)
        {
            currentGold -= stat.price;
            stat.maxLevel += stat.pulse; // 레벨이나 수치 상승
            Debug.Log("강화 완료!");
        }
        else
        {
            Debug.Log("골드가 부족합니다!");
        }
    }


}
