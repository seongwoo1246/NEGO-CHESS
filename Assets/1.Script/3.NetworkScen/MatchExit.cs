using Cysharp.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 대국이 끝난 뒤 "로비로 돌아가기"를 안전하게 처리하는 도우미.
/// 순서가 중요해요: 재접속 기능 끄기 → 네트워크 연결 끊기 → 로비 씬 불러오기
/// (재접속 기능이 켜진 채 연결을 끊으면 "끊겼네?" 하고 다시 접속하려 하기 때문)
/// </summary>
public static class MatchExit
{
    private static bool _leaving;

    public static async UniTask ReturnToLobbyAsync(NetworkRunner runner, string lobbySceneName)
    {
        if (_leaving) return;   // 두 번 눌러도 한 번만
        _leaving = true;

        try
        {
            PlayerPrefs.DeleteKey(NetworkLauncher.LastSessionKey);   // 내가 직접 나가는 것이니 재접속 정보 삭제
            NetworkLauncher.CleanupNetworkServices();                // 재접속 서비스 등 정리
            MatchRoster.Clear();

            if (runner != null) await runner.Shutdown(destroyGameObject: true);

            SceneManager.LoadScene(lobbySceneName);
        }
        finally
        {
            _leaving = false;
        }
    }
}