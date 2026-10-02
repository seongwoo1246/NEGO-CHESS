using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Debug = DebugM<ReconnectionM>;


/// <summary>
/// 게임에서 중간에 팅겨져 나갔을 때 다시 들어가게 하기 위한 스크립트
/// </summary>
public class ReconnectionM : MonoBehaviour, INetworkRunnerCallbacks
{
    private NetworkRunner _runner;
    private string _lastSessionName; // 마지막에 있던 방 이름
    private GameMode _lastGameMode; // 마지막에 하고 있던 게임 상태
    private bool _isReconnecting = false;


    private void Awake()
    {
        //네크워크 관련된 항목이 아닌 서비스 가입 만을 위한 것이여서 Spawned가 아닌 Awake로 한다.
        ScriptM.Register<ReconnectionM>(this, UseSpace.Network_Global);
    }
    private void OnDestroy()
    {
        ScriptM.Unregister<ReconnectionM>();
    }
    public void InitRunner(NetworkRunner runner, string sessionName, GameMode mode)
    {
        _runner = runner;
        _lastSessionName = sessionName;
        _lastGameMode = mode;

        // 콜백 등록
        _runner.AddCallbacks(this);
    }

    // 서버와의 연결이 끊어졌을 때 포톤 퓨전이 자동 호출
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($" 서버 연결 끊김: {reason}");
  
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.LogWarning($" 연결이 끊어졌습니다. 사유: {shutdownReason}");

        if( !_isReconnecting )
        {
            // 인터넷 문제나 타임아웃으로 튕긴 경우
            if (shutdownReason == ShutdownReason.ConnectionTimeout ||
               shutdownReason == ShutdownReason.DisconnectedByPluginLogic ||
                shutdownReason == ShutdownReason.Error)
            {

            }
        }
        
    }
    // 재접속 코루틴
    private async UniTaskVoid ReconnectAsync(CancellationToken cancellationToken = default)
    {
        _isReconnecting = true;

        try
        {
            // 1. 기존 러너 셧다운 및 정리 (Shutdown 작업 완료까지 await)
            if (_runner != null && _runner.IsRunning)
            {
                await _runner.Shutdown();
            }

            // Shutdown 후 1초 대기 (TimeSpan 활용)
            await UniTask.Delay(TimeSpan.FromSeconds(1.0f), cancellationToken: cancellationToken);

            Debug.Log($"[Network] 세션 '{_lastSessionName}'에 재접속 시도 중...");

            // 2. 새 NetworkRunner 생성 및 재연결 요청
            GameObject runnerObj = new GameObject("NetworkRunner_Reconnecting");
            _runner = runnerObj.AddComponent<NetworkRunner>();
            _runner.AddCallbacks(this);

            // Fusion의 StartGame(Task<StartGameResult>)을 UniTask로 바로 await
            var result = await _runner.StartGame(new StartGameArgs()
            {
                GameMode = _lastGameMode,
                SessionName = _lastSessionName,
                ConnectionToken = SystemInfo.deviceUniqueIdentifier != null ?
                    System.Text.Encoding.UTF8.GetBytes(SystemInfo.deviceUniqueIdentifier) : null,
                EnableClientSessionCreation = false // 기존 세션에 참여만 시도
            });

            if (result.Ok)
            {
                Debug.Log("[Network] 재접속 성공!");
            }
            else
            {
                Debug.LogError($"[Network] 재접속 실패: {result.ShutdownReason}");
                // TODO: 재접속 실패 시 로비 씬 이동 등 후속 처리
            }
        }
        catch (OperationCanceledException)
        {
            // CancellationToken으로 취소되었을 때 처리
            Debug.Log("[Network] 재접속 작업이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Network] 재접속 중 예외 발생: {ex.Message}");
        }
        finally
        {
            // 성공/실패/예외 발생 여부와 상관없이 항상 재접속 플래그 해제
            _isReconnecting = false;
        }
    }

    public async void ReconnectToLastSession()
    {
        // 1. 저장된 세션 이름 확인
        string lastSession = PlayerPrefs.GetString("LastSessionName", "");

        if (string.IsNullOrEmpty(lastSession))
        {
            Debug.LogWarning("[Network] 이전 세션 정보가 없습니다.");
            return;
        }

        // 2. 기존 Runner가 동작 중이라면 정리(Shutdown)
        if (_runner != null)
        {
            await _runner.Shutdown();
            Destroy(_runner);
        }

        // 3. 새로운 Runner 생성 후 동일한 세션 & 동일한 AuthToken으로 재접속
        _runner = gameObject.AddComponent<NetworkRunner>();

        byte[] token = Encoding.UTF8.GetBytes(SystemInfo.deviceUniqueIdentifier);

        Debug.Log($"[Network] 이전 세션({lastSession})으로 재접속을 시도합니다...");

        var result = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared, // 기존 설정했던 GameMode와 동일하게
            SessionName = lastSession,
            ConnectionToken = token, // 기존과 동일한 토큰 전달
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
        {
            Debug.Log("[Network] 재접속 성공!");
        }
        else
        {
            Debug.LogError($"[Network] 재접속 실패: {result.ShutdownReason}");
        }
    }



    #region 사용하지 않는 인터페이스 구현

    // --- 사용하지 않는 INetworkRunnerCallbacks 기본 인터페이스 구현부 ---
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        throw new NotImplementedException();
    }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
    {
        throw new NotImplementedException();
    }

  

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
    {
        throw new NotImplementedException();
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        throw new NotImplementedException();
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
    {
        throw new NotImplementedException();
    }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
    {
        throw new NotImplementedException();
    }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
    {
        throw new NotImplementedException();
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
    {
        throw new NotImplementedException();
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        throw new NotImplementedException();
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        throw new NotImplementedException();
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
    {
        throw new NotImplementedException();
    }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
    {
        throw new NotImplementedException();
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        throw new NotImplementedException();
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        throw new NotImplementedException();
    }

    #endregion  

    //    Photon Fusion에서 플레이어가 인터넷 끊김 등으로 튕겼다가 복귀하는 **재접속(Reconnect / Rejoin)** 기능은 Host-Server와 Shared Topology 중 어떤 방식을 쓰느냐에 따라 약간 차이가 있지만, 기본 흐름은 동일

    //핵심은
    //1) 이전에 있던 세션 이름(Session Name)과 내 고유 ID(Token)를 기억,
    //2) `StartGame` 메서드로 동일한 세션에 재접속

   

    //### 1. 재접속 기능의 핵심 개념

    //1. **Authentication Token (인증 토큰 사용)**:
    //   * 퓨전은 클라이언트가 튕겼다가 들어왔을 때 "내가 아까 그 1번 플레이어다"라는 것을 식별하기 위해 `AuthenticationToken`을 사용.
    //2. **Session Name 저장**:
    //   * 튕기기 직전의 Room/Session 이름을 로컬(`PlayerPrefs` 등)에 저장.
    //3. **Rejoin / StartGame 호출**:
    //   * 동일한 세션 이름과 동일한 토큰을 넘겨서 `NetworkRunner.StartGame()`을 다시 실행

   

    //### 2. 구현 예시 코드

    //#### A. 연결 설정 및 토큰 부여 (`NetworkLauncher.cs`)

    //게임 시작 시 내 클라이언트에 고유 토큰(Guid)을 부여하고 세션 이름을 저장해 둡니다.

}