using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// "글자를 입력하는 중인가?"를 알려 주는 도우미.
/// 프리셋 이름, 방 이름, 비밀번호, 닉네임 등 어떤 입력칸이든 포커스가 있으면 true 입니다.
/// 단축키(턴 넘기기/항복/준비/시작 등)를 처리하는 곳 맨 앞에서 이렇게 확인하세요:
///     if (KeyTypingGuard.IsTyping) return;
/// </summary>
public static class KeyTypingGuard
{
    private static int _lastTypingFrame = -10;

    public static bool IsTyping
    {
        get
        {
            bool now = IsInputFocusedNow();
            if (now) _lastTypingFrame = Time.frameCount;

            // Enter 로 입력을 끝내는 프레임에도 단축키가 같이 눌린 것으로 처리되지 않도록 한 프레임 여유를 둡니다.
            return now || Time.frameCount - _lastTypingFrame <= 1;
        }
    }

    private static bool IsInputFocusedNow()
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        GameObject go = es.currentSelectedGameObject;
        if (go == null) return false;

        TMP_InputField tmp = go.GetComponent<TMP_InputField>();
        if (tmp != null) return tmp.isFocused;

        InputField legacy = go.GetComponent<InputField>();
        return legacy != null && legacy.isFocused;
    }
}