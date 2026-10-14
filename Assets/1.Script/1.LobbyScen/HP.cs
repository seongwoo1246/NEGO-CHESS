using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = DebugM<HP>;

public class HP : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI HpText;
    [SerializeField] public Slider hpBar;
    [SerializeField] private int currentHp;
    public int maxHp;
    public int useHp;

  

    // 값이 바뀔 때 현재 체력(int)을 인자로 전달해 주는 이벤트 선언
    public event Action<int> OnHpChanged;

    // 최대 체력 대비 비율(float)로 필요할 때를 위한 이벤트 (선택사항)
    public event Action<float> OnHpRatioChanged;

    private CancellationTokenSource cts;

    // 프로퍼티를 통해 hp 값이 변경될 때 자동으로 이벤트가 발동되도록 설정
    public int Hp
    {
        get => currentHp;
        set
        {
            int clampedValue = Mathf.Clamp(value, 0, maxHp);
            if (currentHp != clampedValue)
            {
                currentHp = clampedValue;

                // 값이 실제로 바뀔 때 이벤트 발행 (구독하고 있는 모든 곳에 알림)
                OnHpChanged?.Invoke(currentHp);
                if (maxHp > 0)
                {
                    OnHpRatioChanged?.Invoke((float)currentHp / maxHp);
                }
            }
        }
    }

    private void Awake()
    {
        ScriptM.Register<HP>(this, UseSpace.Global);
    }

    private void Start()
    {
        CheckOfflineHpRegen(5, 600f);
        StartRegenerateHP(5, 600f).Forget();
    }

    private void OnEnable()
    {
       OnHpRatioChanged += UpdateHpBar;
        OnHpChanged += UpdateHpText;
    }

    private void OnDisable()
    {
        OnHpChanged -= UpdateHpText;
        OnHpRatioChanged -= UpdateHpBar;
        
    }

    private void UpdateHpText(int currentHp)
    {
        if (HpText != null )
        {
            HpText.text = $"{currentHp} / {maxHp}";
        }
    }
    private void UpdateHpBar(float ratio)
    {
        if (hpBar != null)
        {
            hpBar.value = ratio;
        }
    }

    public void UseHp()
    {
        // 프로퍼티(Hp)를 통해 값을 변경하므로 자동으로 이벤트가 발생함
        Hp -= useHp;
    }

    // 유니테스크를 이용한 일정 시간마다 체력 증가 로직
    public async UniTaskVoid StartRegenerateHP(int healAmount, float intervalSeconds)
    {
        cts = new CancellationTokenSource();

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken: cts.Token);

                // 프로퍼티를 통해 체력 증가 (최대 체력 자동 클램프 및 이벤트 발생)
                Hp += healAmount;
                if (Hp >= maxHp)
                {
                    Debug.Log("체력이 가득 차서 회복을 멈춥니다.");
                    break; 
                }

            }
        }
        catch (OperationCanceledException)
        {
            // 정상 취소 처리
        }
    }

    #region 넘어갈 때 위치 변경

    [SerializeField] private RectTransform hpPanelRect; // 숨길 패널의 RectTransform

    // 패널을 숨길 때 (화면 밖 10000픽셀 지점으로 이동)
    public void HidePanel()
    {
        if (hpPanelRect != null)
        {
            hpPanelRect.anchoredPosition = new Vector2(10000f, 10000f);
        }
    }

    // 패널을 다시 보여줄 때 (원래 위치인 (0, 0)으로 복원)
    public void ShowPanel()
    {
        if (hpPanelRect != null)
        {
            hpPanelRect.anchoredPosition = Vector2.zero;
        }
    }

    #endregion

    #region 로그아웃 후에도 체력을 회복하기 위한 부분

    private void OnApplicationQuit()
    {
        // 현재 시간을 이진수(Binary) 문자열로 변환하여 PlayerPrefs에 저장
        string timeString = DateTime.Now.ToBinary().ToString();
        PlayerPrefs.SetString("LastExitTime", timeString);
        PlayerPrefs.Save();
    }

    private void CheckOfflineHpRegen(int healAmountPerTick, float intervalSeconds)
    {
        // 저장된 나간 시간이 있다면
        if (PlayerPrefs.HasKey("LastExitTime"))
        {
            string timeString = PlayerPrefs.GetString("LastExitTime");
            long binaryTime = Convert.ToInt64(timeString);
            DateTime lastExitTime = DateTime.FromBinary(binaryTime);

            // 현재 시간과 나간 시간의 차이 계산 (초 단위)
            double elapsedSeconds = (DateTime.Now - lastExitTime).TotalSeconds;

            // 경과 시간 동안 회복이 몇 번 일어났어야 하는지 계산
            int healCount = Mathf.FloorToInt((float)(elapsedSeconds / intervalSeconds));
            int totalHeal = healCount * healAmountPerTick;

            if (totalHeal > 0)
            {
                // 체력 반영 (최대 체력 넘지 않도록 자동 클램프)
                Hp = Mathf.Min(Hp + totalHeal, maxHp);
                Debug.Log($"오프라인 동안 {totalHeal} 만큼 체력이 회복되었습니다!");
            }
        }
    }
    #endregion

}
