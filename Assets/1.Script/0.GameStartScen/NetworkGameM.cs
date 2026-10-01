using Fusion;
using UnityEngine;

public class NetworkGameM : NetworkBehaviour
{
    [Networked] public PlayerRef WhitePlayer { get; set; }
    [Networked] public PlayerRef BlackPlayer { get; set; }
    [Networked] public PlayerRef CurrentTurn { get; set; }
    [Networked] public GameState State { get; set; }
    // 각 플레이어의 남은 메인 시간 (초 단위, 5분 = 300초)
    [Networked] public float WhiteMainTime { get; set; } = 300f;
    [Networked] public float BlackMainTime { get; set; } = 300f;
    // 현재 턴 플레이어의 이번 턴 남은 초읽기 시간 (5초)
    [Networked] public float CurrentBonusTime { get; set; } = 5f;
    // 현재 턴 유저 구분 (true: 백, false: 흑)
    [Networked] public bool IsWhiteTurn { get; set; } = true;



    public override void Spawned()
    {
        // 서버/호스트일 경우 초기화 설정
        if (Object.HasStateAuthority)
        {
            State = GameState.WaitingForPlayers;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 서버/호스트만 시간 차감을 계산하여 동기화
        if (!Object.HasStateAuthority) return;

        // 게임 진행 중일 때만 차감
        float dt = Runner.DeltaTime;

        if (IsWhiteTurn)
        {
            UpdatePlayerTime(ref WhiteMainTime, dt);
        }
        else
        {
            UpdatePlayerTime(ref BlackMainTime, dt);
        }
    }

    private void UpdatePlayerTime(ref float mainTime, float dt)
    {
        if (mainTime > 0f)
        {
            // 1. 메인 시간(5분)이 남아있다면 메인 시간부터 차감
            mainTime -= dt;
            if (mainTime < 0f) mainTime = 0f;
        }
        else
        {
            // 2. 메인 시간을 다 썼다면 5초 초읽기 차감
            CurrentBonusTime -= dt;

            // 5초마저 다 쓰면 패배 처리
            if (CurrentBonusTime <= 0f)
            {
                CurrentBonusTime = 0f;
                OnTimeOut();
            }
        }
    }

    // 턴이 교체될 때 호출
    public void SwitchTurn()
    {
        if (!Object.HasStateAuthority) return;

        IsWhiteTurn = !IsWhiteTurn;

        // 턴이 넘어갈 때마다 5초 초읽기 시간을 리셋해줌
        CurrentBonusTime = 5f;
    }

    private void OnTimeOut()
    {
      
        // 게임 종료 로직 실행
    }

}


/*

1. 플레이어 데이터 및 진영 관리

2. 턴 및 타이머 제어

3.게임 승패 및 상태 동기화

4.기물 이동 RPC및 승인 수조
 
5. 항복 기권 재접속 처리



 */