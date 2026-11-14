using System.Diagnostics;

// 순수하게 Debug.Log를 에디터 안에서만 돌아가게 만들기 위한 스크립트
// 사용법 : using Debug = DebugManager<T>;

/*
 Conditional 방식(어트리뷰트) : using System.Diagnostics에 붙어 있는 특성으로 
이 특성이 적용된 메서드의 호출은 컴파일 시점에 조건 심볼이 정해져 있지 않으면 호출 자체를 거절한다.


#if UNITY_EDITOR #endif 방식 : 전처리기 지시문으로 해당 코드 블록 전체를 컴파일시 포함할 지 말지 정하는 것으로
조건이 충족되지 않으면 빌드 결과에 포함되지 않는다.
 
 
 */

#region 제네릭 클래스 Dubug
public static class DebugM<T>
{
    
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object message)
    {

        UnityEngine.Debug.Log($"[{typeof(T).Name}] {message}");

    }
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object message)
    {

        UnityEngine.Debug.LogWarning($"[{typeof(T).Name}] {message}");

    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(object message)
    {

        UnityEngine.Debug.LogError($"[{typeof(T).Name}] {message}");

    }
   
}
#endregion

#region 일반 클래스 Debug
public static class DebugM
{
   
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWithTag(string tag, object message)
    {

        UnityEngine.Debug.Log($"[{tag}] {message}");

    }
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarningWithTag(string tag, object message)
    {

        UnityEngine.Debug.LogWarning($"[{tag}] {message}");

    }
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogErrorWithTag(string tag, object message)
    {

        UnityEngine.Debug.LogError($"[{tag}] {message}");

    }
 
}
#endregion