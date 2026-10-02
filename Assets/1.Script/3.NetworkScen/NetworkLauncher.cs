

using Cysharp.Threading.Tasks;
using Fusion;
using System.Text;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using Debug = DebugM<NetworkLauncher>;

/// <summary>
/// 네크워크 상에서 게임에 참가하거나 방을 생성할 때 움직이는 스크립트
/// </summary>
public class NetworkLauncher : MonoBehaviour
{
    private NetworkRunner _runner;

    // 내 클라이언트의 고유 토큰 (재접속 시 동일 인물인지 확인하는 용도)
    private static string LocalCustomAuthToken => SystemInfo.deviceUniqueIdentifier;

    // 게임 세션 생성 또는 참가
    public async UniTask JoinOrCreateRoomAsync(string sessionName)
    {
        // 1. 네트워크용 서비스 동적 생성 및 서비스 로케이터 등록
        var reconnectService = new GameObject("ReconnectionM").AddComponent<ReconnectionM>();
        ScriptM.Register<ReconnectionM>(reconnectService);

        // 2. NetworkRunner 생성 및 StartGame 실행
        _runner = gameObject.AddComponent<NetworkRunner>();

        // 3. 재접속 서비스 초기화
        reconnectService.InitRunner(_runner, sessionName, GameMode.Shared);

        // 재접속을 위해 세션 이름 저장
        PlayerPrefs.SetString("LastSessionName", sessionName);
        PlayerPrefs.Save();

        // 인증 토큰 생성 (byte 배열 변환)
        byte[] token = Encoding.UTF8.GetBytes(LocalCustomAuthToken);

        var result = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared, // 또는 Host / AutoHostClient
            SessionName = sessionName,
            ConnectionToken = token, // 핵심: 고유 토큰 전달
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
        {
            // 접속 실패 시 서비스 해제 및 정리
            CleanupNetworkServices();
        }
    }

    // 멀티플레이 종료 또는 로비 퇴장 시 호출
    public void CleanupNetworkServices()
    {
        if (ScriptM.TryGet<ReconnectionM>(out var reconnectService))
        {
            ScriptM.Unregister<ReconnectionM>();
            Destroy(reconnectService.gameObject);
        }
        // 전역이 아닌 모든 서비스를 해제한다.
        ScriptM.ClearSceneLocalServices();
    }

}

