using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무르기 협상 패널의 "조건 슬롯" 한 칸 (슬롯 프리팹에 붙입니다).
/// 구성: 이름 글자 / 숫자 글자 / 감소 버튼 / 증가 버튼 / 취소(X) 버튼
/// </summary>
public class UndoSlotView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button cancelButton;

    private int _value, _min, _max;
    private Func<int, string> _format;
    private Action<UndoSlotView> _onChanged, _onCancel;

    public int Value => _value;

    public void Setup(string label, int value, int min, int max, Func<int, string> format,
                      Action<UndoSlotView> onChanged, Action<UndoSlotView> onCancel)
    {
        _min = min;
        _max = max;
        _value = Mathf.Clamp(value, min, max);
        _format = format;
        _onChanged = onChanged;
        _onCancel = onCancel;

        if (nameText != null) nameText.text = label;

        Rebind(minusButton, () => Step(-1));
        Rebind(plusButton, () => Step(+1));
        Rebind(cancelButton, () => _onCancel?.Invoke(this));
        ShowValue();
    }

    /// <summary>상대가 보는 읽기 전용 모양 (버튼 숨김/비활성)</summary>
    public void SetInteractable(bool on)
    {
        if (minusButton != null) minusButton.gameObject.SetActive(on);
        if (plusButton != null) plusButton.gameObject.SetActive(on);
        if (cancelButton != null) cancelButton.gameObject.SetActive(on);
    }

    private void Step(int d)
    {
        int v = Mathf.Clamp(_value + d, _min, _max);
        if (v == _value) return;
        _value = v;
        ShowValue();
        _onChanged?.Invoke(this);
    }

    private void ShowValue()
    {
        if (valueText != null) valueText.text = _format != null ? _format(_value) : _value.ToString();
    }

    private static void Rebind(Button b, UnityEngine.Events.UnityAction a)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(a);
    }
}