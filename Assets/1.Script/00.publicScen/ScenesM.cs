using Fusion;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = DebugM<ScenesM>;


public class ScenesM : MonoBehaviour
{
    private NetworkRunner runner;

    
    public void SetRunner(NetworkRunner runner)
    {
        this.runner = runner;
    }


    /// <summary>
    /// 씬 이동은 이걸 통해서 흘러간다.
    /// </summary>
    /// <param name="scene"></param>
    public void LoadScene(SceneNumber scene)
    {
        // 씬 enum을 문자열로 변환 한다.
        string sceneName = scene.ToString();

        //멀티로 이동 할때는 runner를 통해서 이동을 한다.
        if(scene == SceneNumber.multiBattle)
        {
            // Build Settings에서 sceneName("multiBattle")에 해당하는 전체 Path("Assets/Scenes/multiBattle.unity")를 자동으로 찾아옴
            string scenePath = SceneUtility.GetScenePathByBuildIndex(SceneUtility.GetBuildIndexByScenePath(sceneName));

            if (!string.IsNullOrEmpty(scenePath))
            {
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






    private void Awake()
    {
        ScriptM.Register<ScenesM>(this, UseSpace.Network_Global);
    }

    private void OnDestroy()
    {
        ScriptM.Unregister<ScenesM>();
    }


}
