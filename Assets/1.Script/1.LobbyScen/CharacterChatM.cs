using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 대화 버튼.
///  - 버튼을 누르면 그 캐릭터가 나오고, 등장할 때 대사를 한 번 합니다.
///  - 그 뒤로는 일정 시간마다 랜덤으로 대사를 합니다.
///  - 같은 대사는 모두 한 번씩 나온 뒤에야 다시 나오고, 다 떨어지면 처음부터 다시 랜덤으로 돌립니다.
/// 대사 데이터와 화면 출력은 DialogueController가 담당합니다.
/// </summary>
public class CharacterChatM : MonoBehaviour
{
    [Header("캐릭터 선택 버튼")]
    [SerializeField] private Button seoButton;
    [SerializeField] private Button reonButton;
    [SerializeField] private Button cloeButton;
    [Tooltip("대화를 끝내는 버튼 (선택)")]
    [SerializeField] private Button closeButton;

    [Header("캐릭터 그림 (선택) - 선택된 캐릭터만 켜집니다")]
    [SerializeField] private GameObject seoObject;
    [SerializeField] private GameObject reonObject;
    [SerializeField] private GameObject cloeObject;

    [Header("대사 타이밍")]
    [Tooltip("대사가 다 나타난 뒤 화면에 머무는 시간(초)")]
    [SerializeField, Min(0.5f)] private float holdSeconds = 3f;
    [Tooltip("대사가 사라진 뒤 다음 대사까지 쉬는 시간(초). 이 범위 안에서 매번 랜덤입니다.")]
    [SerializeField] private Vector2 pauseRange = new Vector2(3f, 8f);

    [Header("설정")]
    [Tooltip("꺼 두면 일상 파트 대사, 켜면 배틀 파트 대사를 씁니다.")]
    [SerializeField] private bool useBattleDialogues = false;
    [Tooltip("잠겨 있는 캐릭터(업적으로 해금 전)는 선택할 수 없게 합니다.")]
    [SerializeField] private bool onlyUnlocked = true;

    private CancellationTokenSource _cts;
    private CharacterType _current;
    private bool _hasCurrent;

    // 캐릭터마다 "이미 나온 대사"를 기억합니다 (다 나오면 비우고 처음부터)
    private readonly Dictionary<CharacterType, HashSet<string>> _used = new Dictionary<CharacterType, HashSet<string>>();
    private readonly Dictionary<CharacterType, string> _lastText = new Dictionary<CharacterType, string>();
    private readonly List<CharacterDialogue> _pool = new List<CharacterDialogue>();
    private readonly List<CharacterDialogue> _fresh = new List<CharacterDialogue>();

    // ───────────── 켜기 / 끄기 ─────────────

    private void OnEnable()
    {
        if (seoButton != null) seoButton.onClick.AddListener(OnClickSeo);
        if (reonButton != null) reonButton.onClick.AddListener(OnClickReon);
        if (cloeButton != null) cloeButton.onClick.AddListener(OnClickCloe);
        if (closeButton != null) closeButton.onClick.AddListener(EndChat);

        RefreshCharacterObjects(false, default);
    }

    private void OnDisable()
    {
        if (seoButton != null) seoButton.onClick.RemoveListener(OnClickSeo);
        if (reonButton != null) reonButton.onClick.RemoveListener(OnClickReon);
        if (cloeButton != null) cloeButton.onClick.RemoveListener(OnClickCloe);
        if (closeButton != null) closeButton.onClick.RemoveListener(EndChat);

        StopLoop();
    }

    private void OnDestroy()
    {
        StopLoop();
    }

    // ───────────── 버튼 ─────────────

    private void OnClickSeo() => StartChat(CharacterType.seo);
    private void OnClickReon() => StartChat(CharacterType.reon);
    private void OnClickCloe() => StartChat(CharacterType.cloe);

    /// <summary>이 캐릭터와 대화를 시작합니다. (이미 대화 중인 캐릭터를 다시 누르면 새 대사로 다시 시작)</summary>
    public void StartChat(CharacterType character)
    {
        SoundService.Current.playSFX("버튼");
        if (onlyUnlocked && !IsUnlocked(character)) return;

        StopLoop();

        _current = character;
        _hasCurrent = true;
        RefreshCharacterObjects(true, character);

        _cts = new CancellationTokenSource();
        ChatLoopAsync(character, _cts.Token).Forget();
    }

    /// <summary>대화를 끝내고 캐릭터를 숨깁니다.</summary>
    public void EndChat()
    {
        SoundService.Current.playSFX("버튼");
        StopLoop();
        _hasCurrent = false;
        RefreshCharacterObjects(false, default);
    }

    // ───────────── 대화 루프 ─────────────

    private async UniTaskVoid ChatLoopAsync(CharacterType character, CancellationToken ct)
    {
        // 등장할 때 한 번 말하고
        SayNext(character);

        // 그 뒤로는 (대사가 나타나고 사라지는 시간 + 쉬는 시간)마다 말한다
        while (!ct.IsCancellationRequested)
        {
            float fadeAndHold = holdSeconds + 1f;   // 나타남 0.5 + 머묾 + 사라짐 0.5
            float pause = UnityEngine.Random.Range(Mathf.Min(pauseRange.x, pauseRange.y), Mathf.Max(pauseRange.x, pauseRange.y));

            bool canceled = await UniTask
                .Delay(TimeSpan.FromSeconds(fadeAndHold + pause), ignoreTimeScale: true, cancellationToken: ct)
                .SuppressCancellationThrow();
            if (canceled) return;

            SayNext(character);
        }
    }

    private void SayNext(CharacterType character)
    {
        if (!ScriptM.TryGet<DialogueController>(out DialogueController dialogue) || dialogue == null) return;

        dialogue.GetCandidatePool(character, useBattleDialogues, _pool);
        if (_pool.Count == 0) return;   // 이 캐릭터에게 오늘 쓸 수 있는 대사가 없음

        if (!_used.TryGetValue(character, out HashSet<string> used))
        {
            used = new HashSet<string>();
            _used[character] = used;
        }
        _lastText.TryGetValue(character, out string last);

        // 아직 안 나온 대사만 후보로
        _fresh.Clear();
        for (int i = 0; i < _pool.Count; i++)
        {
            if (!used.Contains(_pool[i].dialogueText)) _fresh.Add(_pool[i]);
        }

        // 전부 나왔다면 기록을 비우고 처음부터 다시 (방금 한 대사는 가능하면 피함)
        if (_fresh.Count == 0)
        {
            used.Clear();
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool.Count > 1 && _pool[i].dialogueText == last) continue;
                _fresh.Add(_pool[i]);
            }
            if (_fresh.Count == 0) _fresh.AddRange(_pool);
        }

        CharacterDialogue pick = _fresh[UnityEngine.Random.Range(0, _fresh.Count)];
        used.Add(pick.dialogueText);
        _lastText[character] = pick.dialogueText;

       
        dialogue.ShowLine(pick, holdSeconds);
    }

    private void StopLoop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    // ───────────── 도우미 ─────────────

    private void RefreshCharacterObjects(bool anyChosen, CharacterType chosen)
    {
        if (seoObject != null) seoObject.SetActive(anyChosen && chosen == CharacterType.seo);
        if (reonObject != null) reonObject.SetActive(anyChosen && chosen == CharacterType.reon);
        if (cloeObject != null) cloeObject.SetActive(anyChosen && chosen == CharacterType.cloe);
    }

    // AchievementM이 쓰는 해금 키와 같은 규칙: 레온 = 1, 클로에 = 2, 세오는 처음부터 사용 가능
    private static bool IsUnlocked(CharacterType character)
    {
        switch (character)
        {
            case CharacterType.reon: return PlayerPrefs.GetInt("Character_Unlock_1", 0) == 1;
            case CharacterType.cloe: return PlayerPrefs.GetInt("Character_Unlock_2", 0) == 1;
            default: return true;
        }
    }
}