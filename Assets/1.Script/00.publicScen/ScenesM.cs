using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = DebugM<ScenesM>;


public class ScenesM : MonoBehaviour
{
    private NetworkRunner runner;

    private void Awake()
    {
        ScriptM.Register<ScenesM>(this, UseSpace.Global);
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<ScenesM>();
    }

    /// <summary>
    /// 멀티 게임 시작할 때 안에 넣어주기
    /// </summary>
    public void SetRunner(NetworkRunner runner)
    {
        this.runner = runner;
    }


    /// <summary>
    /// 씬 이동은 이걸 통해서 흘러간다.
    /// </summary>
    public void LoadScene(SceneNumber scene)
    {
        // 씬 enum을 문자열로 변환 한다.
        string sceneName = scene.ToString();
        ScriptM.ClearSceneLocalServices();
        //멀티로 이동 할때는 runner를 통해서 이동을 한다.
        if (scene == SceneNumber.MultiLobby)
        {
            // Build Settings에서 sceneName("MultiLobby")에 해당하는 전체 Path("Assets/Scenes/MultiLobby.unity")를 자동으로 찾아옴
            string scenePath = SceneUtility.GetScenePathByBuildIndex(SceneUtility.GetBuildIndexByScenePath(sceneName));

            if (!string.IsNullOrEmpty(scenePath))
            {
                SetRunner(runner);
                runner.LoadScene(SceneRef.FromPath(scenePath));
            }
            else
            {
                Debug.LogError($"[ScenesM] Build Settings에서 {sceneName} 씬 경로를 찾을 수 없습니다.");
            }
            return;
        }


        // 일반적으로 이동 할 때는 이걸로 이동
        SceneManager.LoadScene(sceneName);

    }

}
