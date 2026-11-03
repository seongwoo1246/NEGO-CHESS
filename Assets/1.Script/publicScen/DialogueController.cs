using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct CharacterDialogue
{
    public CharacterType characterType;
    [TextArea(2, 4)] public string dialogueText; // 여러 줄로 편하게 입력할 수 있는 대사 필드

    [Header("날짜 조건 유형")]
    public bool isBirthdayCondition; // 생일 맞춤 대사 여부
    public int targetMonth;        // 특정 월 (예: 2)
    public int targetDay;          // 특정 일 (예: 14)

    [Header("성별 조건")]
    public bool requireGirl;
    public bool requireMan;

    [Header("협상력에 따른 결과")]
    public bool NegoPowerOver;

    [Header("대사에 맞는 이미지 넣기")]
    public Sprite icon;
}

/// <summary>
/// 대사 튜토리얼 글자등 언어 전달을 담당 하는  스크립트
/// </summary>
public class DialogueController : MonoBehaviour
{
    private const string DefaultLine = "반가워, 체스 한 판 두어볼까?";

    public TextMeshProUGUI characterText; // 대사 출력용 텍스트 (첫 번째 이미지 참고)
    public Image CharIcon;

    [Header("일상 파트 대사 모음")]
    public List<CharacterDialogue> dailyDialogues_Seo;
    public List<CharacterDialogue> dailyDialogues_Reon;
    public List<CharacterDialogue> dailyDialogues_Cloe;
    [Header("배틀 파트 대사 모음")]
    public List<CharacterDialogue> battleDialogues_Seo;
    public List<CharacterDialogue> battleDialogues_Reon;
    public List<CharacterDialogue> battleDialogues_Cloe;

    private Sequence _fadeSeq;
    private string _lastLine = string.Empty;

    // 매번 새 리스트를 만들지 않도록 재사용
    private readonly List<CharacterDialogue> _birthdayCandidates = new List<CharacterDialogue>();
    private readonly List<CharacterDialogue> _normalCandidates = new List<CharacterDialogue>();

    public void Awake()
    {
        ScriptM.Register<DialogueController>(this, UseSpace.Global);
    }

    private void OnDestroy()
    {
        if (_fadeSeq != null) _fadeSeq.Kill();
    }

    // 대사를 출력하는 함수
    public void ShowDialogue(string message)
    {
        if (characterText == null) return;

        // 1. 이전 연출이 남아 있다면 끊는다.
        //    (시퀀스 안의 트윈은 DOKill(target)으로 안 끊기므로, 시퀀스를 직접 보관해서 끊어야 겹치지 않는다)
        if (_fadeSeq != null) _fadeSeq.Kill();
        characterText.DOKill();

        // 2. 새 대사를 넣고 투명하게 시작
        characterText.text = message;
        characterText.alpha = 0f;

        // 3. 나타남 → 유지 → 사라짐
        _fadeSeq = DOTween.Sequence()
            .Append(characterText.DOFade(1f, 0.5f))
            .AppendInterval(2.0f)
            .Append(characterText.DOFade(0f, 0.5f))
            .SetLink(gameObject);
    }

    /// <summary>
    /// 플레이어의 정보를 받아 대사를 골라주는 함수.
    /// playerNego / enemyNego를 넘기면 협상력 조건(NegoPowerOver / NegoPowerUnder)이 적용됩니다.
    /// 넘기지 않으면(일상 파트 등) 협상력 조건이 붙은 대사는 후보에서 빠집니다.
    /// </summary>
    public string GetMatchingDialogue(CharacterType characterType, bool isBattle)
    {
        int currentMonth = System.DateTime.Now.Month;
        int currentDay = System.DateTime.Now.Day;

        List<CharacterDialogue> targetList = SelectList(characterType, isBattle);
        if (targetList == null) return DefaultLine;

        _birthdayCandidates.Clear();
        _normalCandidates.Clear();

        foreach (var d in targetList)
        {
            // 날짜 조건
            bool dateOk;
            if (d.isBirthdayCondition)
            {
                dateOk = singlePlayer.birthMonth == currentMonth && singlePlayer.birthDay == currentDay;
            }
            else
            {
                bool matchMonth = d.targetMonth == 0 || d.targetMonth == currentMonth;
                bool matchDay = d.targetDay == 0 || d.targetDay == currentDay;
                dateOk = matchMonth && matchDay;
            }
            if (!dateOk) continue;

            // 성별 조건
            bool matchGender = (!d.requireGirl && !d.requireMan) ||
                               (d.requireGirl && singlePlayer.isGirl) ||
                               (d.requireMan && !singlePlayer.isGirl);
            if (!matchGender) continue;

           

            if (d.isBirthdayCondition) _birthdayCandidates.Add(d);
            else _normalCandidates.Add(d);
        }

        // 생일 대사가 있으면 오늘은 그것만 나옵니다 (일반 대사에 묻히지 않도록)
        List<CharacterDialogue> pool = _birthdayCandidates.Count > 0 ? _birthdayCandidates : _normalCandidates;
        if (pool.Count == 0) return DefaultLine;

        CharacterDialogue selected = PickWeighted(pool);

        // 아이콘은 있을 때만 바꿉니다 (비어 있는 대사가 그림을 지워 버리지 않도록)
        if (CharIcon != null && selected.icon != null) CharIcon.sprite = selected.icon;

        _lastLine = selected.dialogueText;
        return FormatLine(selected.dialogueText);
    }

    // 후보 중 하나를 랜덤으로 고르되, 후보가 둘 이상이면 방금 나온 대사는 피합니다.
    private CharacterDialogue PickWeighted(List<CharacterDialogue> pool)
    {
        if (pool.Count > 1)
        {
            int start = Random.Range(0, pool.Count);
            for (int k = 0; k < pool.Count; k++)
            {
                CharacterDialogue c = pool[(start + k) % pool.Count];
                if (c.dialogueText != _lastLine) return c;
            }
        }
        return pool[Random.Range(0, pool.Count)];
    }
    private List<CharacterDialogue> SelectList(CharacterType characterType, bool isBattle)
    {
        if (!isBattle)
        {
            switch (characterType)
            {
                case CharacterType.seo: return dailyDialogues_Seo;
                case CharacterType.reon: return dailyDialogues_Reon;
                case CharacterType.cloe: return dailyDialogues_Cloe;
            }
        }
        else
        {
            switch (characterType)
            {
                case CharacterType.seo: return battleDialogues_Seo;
                case CharacterType.reon: return battleDialogues_Reon;
                case CharacterType.cloe: return battleDialogues_Cloe;
            }
        }
        return null;
    }

    // string.Format은 대사에 { } 가 하나라도 섞이면 예외가 나므로, {0}만 안전하게 바꿉니다.
    private static string FormatLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return DefaultLine;
        string name = string.IsNullOrEmpty(singlePlayer.playerName) ? string.Empty : singlePlayer.playerName;
        return text.Replace("{0}", name);
    }
}