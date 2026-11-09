using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기물 배치 프리셋 (싱글 플레이 배치 단계용).
///  - "프리셋 N" 버튼: 이름 입력 창이 열리고, 확인을 누르면 지금 배치가 저장됩니다.
///  - "변경" 버튼: 저장된 배치를 불러옵니다.
/// 실제 판 조작은 CheseGameM(CollectMyPieces / ApplyPreset)이, 저장은 PiecePresetStore가 맡습니다.
/// </summary>
public class PiecePresetM : MonoBehaviour
{
    [Header("프리셋 버튼 3개 (위에서부터 순서대로)")]
    [Tooltip("'프리셋 1' 같은 버튼. 누르면 이름 입력 창 → 저장")]
    [SerializeField] private Button[] saveButtons = new Button[3];
    [Tooltip("옆의 '변경' 버튼. 누르면 저장된 배치를 불러옴")]
    [SerializeField] private Button[] loadButtons = new Button[3];
    [Tooltip("프리셋 버튼의 글자. 비워 두면 버튼 자식에서 자동으로 찾습니다.")]
    [SerializeField] private TMP_Text[] saveLabels = new TMP_Text[3];

    [Header("이름 입력 창")]
    [SerializeField] private GameObject namePopup;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button confirmButton;
    [Tooltip("취소 버튼 (선택)")]
    [SerializeField] private Button cancelButton;
    [SerializeField, Min(1)] private int maxNameLength = 8;

    [Header("안내 글자 (선택)")]
    [SerializeField] private TMP_Text noticeText;
    [SerializeField, Min(0.5f)] private float noticeSeconds = 2f;

    private int _pendingSlot = -1;
    private readonly List<PresetPiece> _buffer = new List<PresetPiece>(32);

    // ───────────── 켜기 ─────────────

    private void Awake()
    {
        for (int i = 0; i < PiecePresetStore.SlotCount; i++)
        {
            int index = i;   // 람다가 올바른 번호를 기억하도록 복사

            if (i < saveButtons.Length && saveButtons[i] != null)
            {
                saveButtons[i].onClick.AddListener(() => OnClickSave(index));
                if (saveLabels[i] == null) saveLabels[i] = saveButtons[i].GetComponentInChildren<TMP_Text>(true);
            }
            if (i < loadButtons.Length && loadButtons[i] != null)
                loadButtons[i].onClick.AddListener(() => OnClickLoad(index));
        }

        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmName);
        if (cancelButton != null) cancelButton.onClick.AddListener(ClosePopup);
        if (nameInput != null) nameInput.characterLimit = maxNameLength;

        ClosePopup();
        ClearNotice();
    }

    private void OnEnable()
    {
        RefreshLabels();
    }

    // ───────────── 버튼 ─────────────

    private void OnClickSave(int index)
    {
        SoundService.Current.playSFX("버튼");
        if (!TryGetGame(out CheseGameM game) || !game.IsSetupPhase) return;   // 배치 단계에서만

        game.CollectMyPieces(_buffer);
        if (_buffer.Count == 0)
        {
            Notice("배치된 기물이 없어요.");
            return;
        }

        _pendingSlot = index;
        if (namePopup != null) namePopup.SetActive(true);
        if (nameInput != null)
        {
            PresetSlot slot = PiecePresetStore.Get(index);
            nameInput.text = slot.hasData ? slot.name : "";   // 이미 있으면 이름을 미리 채워 둠
            nameInput.ActivateInputField();
        }
    }

    private void OnConfirmName()
    {
        SoundService.Current.playSFX("버튼");
        int index = _pendingSlot;
        if (index < 0 || !TryGetGame(out CheseGameM game) || !game.IsSetupPhase)
        {
            ClosePopup();
            return;
        }

        // 창이 열려 있는 동안 바뀌었을 수 있으니 확인 시점의 배치를 저장
        game.CollectMyPieces(_buffer);
        if (_buffer.Count == 0)
        {
            ClosePopup();
            Notice("배치된 기물이 없어요.");
            return;
        }

        string presetName = nameInput != null ? nameInput.text.Trim() : "";
        if (presetName.Length == 0) presetName = $"프리셋 {index + 1}";

        PiecePresetStore.Set(index, presetName, _buffer);

        RefreshLabels();
        ClosePopup();
        Notice($"'{presetName}' 저장 완료!");
    }

    private void OnClickLoad(int index)
    {
        SoundService.Current.playSFX("버튼");
        if (!TryGetGame(out CheseGameM game) || !game.IsSetupPhase) return;

        PresetSlot slot = PiecePresetStore.Get(index);
        if (!slot.hasData)
        {
            Notice("비어 있는 프리셋이에요.");
            return;
        }

        int placed = game.ApplyPreset(slot.pieces);
        if (placed == slot.pieces.Count) Notice($"'{slot.name}' 불러오기 완료!");
        else Notice($"'{slot.name}': {slot.pieces.Count}개 중 {placed}개만 배치됐어요.");
    }

    // ───────────── 도우미 ─────────────

    private void RefreshLabels()
    {
        for (int i = 0; i < PiecePresetStore.SlotCount && i < saveLabels.Length; i++)
        {
            if (saveLabels[i] == null) continue;
            PresetSlot slot = PiecePresetStore.Get(i);
            saveLabels[i].text = slot.hasData && !string.IsNullOrEmpty(slot.name) ? slot.name : $"프리셋 {i + 1}";
        }
    }

    private void ClosePopup()
    {
        _pendingSlot = -1;
        if (namePopup != null) namePopup.SetActive(false);
    }

    private static bool TryGetGame(out CheseGameM game)
    {
        if (ScriptM.TryGet<CheseGameM>(out game) && game != null) return true;
        game = null;
        return false;
    }

    private void Notice(string message)
    {
       
        if (noticeText == null) return;

        noticeText.text = message;
        CancelInvoke(nameof(ClearNotice));
        Invoke(nameof(ClearNotice), noticeSeconds);
    }

    private void ClearNotice()
    {
        if (noticeText != null) noticeText.text = "";
    }
}