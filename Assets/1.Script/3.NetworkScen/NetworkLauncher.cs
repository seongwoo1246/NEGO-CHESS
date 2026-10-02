

using System.Text;
using Fusion;
using UnityEngine;
using Debug = DebugM<NetworkLauncher>;

/// <summary>
/// 네크워크 상에서 게임에 참가하거나 방을 생성할 때 움직이는 스크립트
/// </summary>
public class NetworkLauncher : MonoBehaviour
{
    private NetworkRunner _runner;

    private void Awake()
    {
        //네크워크 관련된 항목이 아닌 서비스 가입 만을 위한 것이여서 Spawned가 아닌 Awake로 한다.
        ScriptM.Register<NetworkLauncher>(this, UseSpace.Network_Global);
    }
    private void OnDestroy()
    {
        ScriptM.Unregister<NetworkLauncher>();
    }

    // 내 클라이언트의 고유 토큰 (재접속 시 동일 인물인지 확인하는 용도)
    private static string LocalCustomAuthToken => SystemInfo.deviceUniqueIdentifier;

    // 게임 세션 생성 또는 참가
    public async void JoinSession(string sessionName)
    {
        if (_runner == null)
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
        }

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

        if (result.Ok)
        {
            if(ScriptM.TryGet<ReconnectionM>(out ReconnectionM reconnectionM))
            {
                reconnectionM.InitRunner(_runner, sessionName, GameMode.Shared);
            }
            Debug.Log("[Network] 성공적으로 세션에 접속했습니다.");
        }
    }
}

