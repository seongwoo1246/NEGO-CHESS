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
    public void Awake()
    {
        ScriptM.Register<DialogueController>(this, UseSpace.Global);
    }

    // 대사를 출력하는 함수
    public void ShowDialogue(string message)
    {
        // 1. 기존 트윈 애니메이션이 있다면 겹치지 않게 초기화
        characterText.DOKill();

        // 2. 투명도를 0으로 맞추고 시작
        characterText.alpha = 0f;
        characterText.text = "";

        // 3. DOTween 시퀀스(연속 애니메이션) 생성
        Sequence seq = DOTween.Sequence();

        characterText.text = message; // 텍스트를 먼저 대입하고
      
        seq.Append(characterText.DOFade(1f, 0.5f)); // 0.5초 동안 부드럽게 나타남
        seq.AppendInterval(2.0f);
        seq.Append(characterText.DOFade(0f, 0.5f)); // 0.5초 동안 사라짐
    }

    // 플레이어의 정보를 받아 대사를 골라주는 함수
    public string GetMatchingDialogue(CharacterType characterType, bool isBattle)
    {
        int currentMonth = System.DateTime.Now.Month;
        int currentDay = System.DateTime.Now.Day;

        // 💡 핵심: 상황에 맞는 리스트를 통째로 골라 담을 변수
        List<CharacterDialogue> targetList = null;

        // 1. 일상 / 배틀 여부와 캐릭터 타입에 따라 알맞은 리스트 선택
        if (!isBattle) // 일상 파트인 경우
        {
            switch (characterType)
            {
                case CharacterType.seo: targetList = dailyDialogues_Seo; break;
                case CharacterType.reon: targetList = dailyDialogues_Reon; break;
                case CharacterType.cloe: targetList = dailyDialogues_Cloe; break;
            }
        }
        else // 배틀 파트인 경우
        {
            switch (characterType)
            {
                case CharacterType.seo: targetList = battleDialogues_Seo; break;
                case CharacterType.reon: targetList = battleDialogues_Reon; break;
                case CharacterType.cloe: targetList = battleDialogues_Cloe; break;
            }
        }

        // 만약 리스트가 비어있거나 잘못 연결되었다면 안전하게 기본 처리
        if (targetList == null) return "반가워, 체스 한 판 두어볼까?";

        List<CharacterDialogue> candidateList = new List<CharacterDialogue>();

        // 2. 골라낸 리스트를 가지고 기존처럼 조건 검사 진행
        foreach (var d in targetList)
        {
            bool isMatch = false;

            if (d.isBirthdayCondition)
            {
                if (singlePlayer.birthMonth == currentMonth && singlePlayer.birthDay == currentDay)
                    isMatch = true;
            }
            else
            {
                bool matchMonth = (d.targetMonth == 0 || d.targetMonth == currentMonth);
                bool matchDay = (d.targetDay == 0 || d.targetDay == currentDay);
                if (matchMonth && matchDay) isMatch = true;
            }

            bool matchGender = (!d.requireGirl && !d.requireMan) ||
                               (d.requireGirl && singlePlayer.isGirl) ||
                               (d.requireMan && !singlePlayer.isGirl);

            if (isMatch && matchGender)
            {
                candidateList.Add(d);
            }
        }

        // 3. 후보 중 랜덤 선택 후 텍스트 및 아이콘 반환
        if (candidateList.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, candidateList.Count);
            CharacterDialogue selected = candidateList[randomIndex];

            CharIcon.sprite = selected.icon; // 아이콘 저장 (스크린샷 변수명 기준)
            return string.Format(selected.dialogueText, singlePlayer.playerName);
        }

       
        return "반가워, 체스 한 판 두어볼까?";
    }
}
