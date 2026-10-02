using Fusion;
using UnityEngine;
using Debug = DebugM<NetworkGameM>;

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
    [Networked] public bool IsWhiteTurn { get; set; } = false;

    //현제 게임 상태 (값이 변하면 OnStateChanged 메서드 자동실행)
    [Networked, OnChangedRender(nameof(OnStateChanged))]
    public GameState currentState { get; set; } = GameState.WaitingForPlayers;

    //승리한 플레이어 =None이면 무승부로 처리
    [Networked] public PlayerRef Winner {  get; set; } = PlayerRef.None;


    
    private void Awake()
    {
        //네크워크 관련된 항목이 아닌 서비스 가입 만을 위한 것이여서 Spawned가 아닌 Awake로 한다.
        ScriptM.Register<NetworkGameM>(this, UseSpace.Network_Global);
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<NetworkGameM>();
    }


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
            WhiteMainTime = UpdatePlayerTime( WhiteMainTime, dt);
        }
        else
        {
            BlackMainTime =  UpdatePlayerTime( BlackMainTime, dt);
        }
    }

    // 클라이언트가 진영 버튼(White/Black)을 눌렀을 때 서버로 요청하는 RPC (UI
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestSelectColor(PlayerRef requestPlayer, PieceColor color)
    {
        //1. 이미 누군가 선택한 진영인지 검사
        if (color == PieceColor.white && WhitePlayer == PlayerRef.None)
        {
            if (BlackPlayer == requestPlayer)
            {
                BlackPlayer = PlayerRef.None;
            }
            WhitePlayer = requestPlayer;
            Debug.Log($"{requestPlayer}님이 백을 선택했습니다.");
        }
        else if (color == PieceColor.black && BlackPlayer == PlayerRef.None)
        {
            if (color == PieceColor.white && WhitePlayer == PlayerRef.None)
            {
                if (WhitePlayer == requestPlayer)
                {
                    WhitePlayer = PlayerRef.None;
                }
                BlackPlayer = requestPlayer;
                Debug.Log($"{requestPlayer}님이 흑을 선택했습니다.");
            }
        }
        else
        {
            Debug.LogWarning("이미 선택한 진영입니다.");
        }
    }

    // 양쪽 모두 진영을 정했는지 확인 함수 (Ui
    public bool IsBothPlayerReady()
    {
        return WhitePlayer != PlayerRef.None && BlackPlayer != PlayerRef.None;
    }

    // 진영 선택을 미처 안 한 경우 랜덤으로 배치해 주는 함수 (게임 시작 시 호출)
    public void AutoAssignRemainingSides(PlayerRef player1, PlayerRef player2)
    {
        if (!Object.HasStateAuthority) return;

        // 아무도 안 골랐을 때
        if (WhitePlayer == PlayerRef.None && BlackPlayer == PlayerRef.None)
        {
            bool randomBool = Random.value > 0.5f;
            WhitePlayer = randomBool ? player1 : player2;
            BlackPlayer = randomBool ? player2 : player1;
        }
        // 한 명만 백을 골랐을 때
        else if (WhitePlayer != PlayerRef.None && BlackPlayer == PlayerRef.None)
        {
            BlackPlayer = (WhitePlayer == player1) ? player2 : player1;
        }
        // 한 명만 흑을 골랐을 때
        else if (BlackPlayer != PlayerRef.None && WhitePlayer == PlayerRef.None)
        {
            WhitePlayer = (BlackPlayer == player1) ? player2 : player1;
        }
    }

    private float UpdatePlayerTime( float mainTime, float dt)
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

        return mainTime;
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
        //시간이 다 떨어지면 게임 패배 처리를 하지 않고 턴을 넘겨버린다.
        SwitchTurn();
       
    }

    /// <summary>
    /// 서버에서 게임 종료 판정을 내릴때 필요한것
    /// </summary>
    public void EndGame(PlayerRef winnerPlayer)
    {
        if (!Object.HasStateAuthority) return; // 상태 변경 권한 검사를 넘긴다. (서버만 변경 가능)
        if (currentState == GameState.GameEnd) return; // 이미 종료된 게임이면 중복 방지

        Winner = winnerPlayer;

        // 상태를 GameEnd로 바꾸는 순간, 모든 클라이언트의 OnStateChanged가 살행된다.
        currentState = GameState.GameEnd;
    }

    /// <summary>
    /// 기권(Resign) 요청 RPC (클라이언트가 호출)
    /// </summary>
    /// <param name="resigningPlayer">항복을 한 사람</param>
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestResign(PlayerRef resigningPlayer)
    {
        //승리한 사람이 누구인지를 정하는 삼항 연산자
        PlayerRef Winner = (resigningPlayer == WhitePlayer) ? BlackPlayer : WhitePlayer;
        EndGame(Winner);
    }

    // CurrentState 값이 네트워크를 타고 갱신되었을 때 모든 기기에서 실행
    private static void OnStateChanged(NetworkGameM changed)
    {
        changed.HandleStateChange();
    }

    private void HandleStateChange()
    {
        switch (currentState)
        {
            case GameState.WaitingForPlayers:
                Debug.Log("플레이어 대기 중");
                // 대기 화면 UI 보여주기
                break;
            case GameState.GameStart:
                Debug.Log(" 진영 선택 단계");
                //진영 선택 UI 활성화
                break;
            case GameState.Playing:
                Debug.Log("게임 시작");
                //체스판 입력 활성화 타이머 가동
                break;
            case GameState.GameEnd:
                Debug.Log($" 게임 종료! 게임 승리자 {Winner}");
                ShowGameOverUI();
                break;
        }
    }

    private void ShowGameOverUI()
    {
        bool isLocalPlayerWinner = (Runner.LocalPlayer == Winner);
        bool isDraw = (Winner == PlayerRef.None);

        string massage;

        if(isDraw)
        {
            massage = "무승부";
        }
        else if(isLocalPlayerWinner)
        {
            massage = "승리했습니다. 축하합니다.";
        }
        else
        {
            massage = "패배했습니다.";
        }

        // TODO : Ui 매니저를 통해 결과 팝업창 띄우기
    }

   

}


/*

1. 플레이어 데이터 및 진영 관리

2. 턴 및 타이머 제어

3.게임 승패 및 상태 동기화

4.기물 이동 RPC및 승인 수조
 
5. 항복 기권 재접속 처리



 */