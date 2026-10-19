using DG.Tweening;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
    [Header("협상력에 따른 결과")]
    public bool NegoPowerOver;
}

/// <summary>
/// 대사 튜토리얼 글자등 언어 전달을 담당 하는  스크립트
/// </summary>
public class DialogueController : MonoBehaviour
{
    public TextMeshProUGUI characterText; // 대사 출력용 텍스트 (첫 번째 이미지 참고)

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

    public List<CharacterDialogue> dialogues;

    // 플레이어의 정보를 받아 대사를 골라주는 함수
    public string GetMatchingDialogue(CharacterType characterType)
    {
        int currentMonth = System.DateTime.Now.Month;
        int currentDay = System.DateTime.Now.Day;

        // 조건에 맞는 대사들을 담아둘 임시 후보 리스트
        List<string> candidateDialogues = new List<string>();

        foreach (var d in dialogues)
        {

            if (d.characterType != characterType) continue;
            bool isMatch = false;

            // 1. 생일 조건 판정
            if (d.isBirthdayCondition)
            {
                if (singlePlayer.birthMonth == currentMonth && singlePlayer.birthDay == currentDay)
                {
                    isMatch = true;
                }
            }
            // 2. 일반 기념일 조건 판정
            else
            {
                bool matchMonth = (d.targetMonth == 0 || d.targetMonth == currentMonth);
                bool matchDay = (d.targetDay == 0 || d.targetDay == currentDay);
                if (matchMonth && matchDay)
                {
                    isMatch = true;
                }
            }

            // 성별 조건 판정
            bool matchGender = (!d.requireGirl || singlePlayer.isGirl);

            // 조건이 일치한다면 후보 리스트에 추가 (이름 포맷팅 적용)
            if (isMatch && matchGender)
            {
                string formattedText = string.Format(d.dialogueText, singlePlayer.playerName);
                candidateDialogues.Add(formattedText);
            }
        }

        // 만약 조건에 맞는 대사가 하나라도 있다면 그중에서 무작위로 하나 반환!
        if (candidateDialogues.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, candidateDialogues.Count);
            return candidateDialogues[randomIndex];
        }

        // 조건에 맞는 특별 대사가 전혀 없다면 기본 대사 반환
        return "반가워, 체스 한 판 두어볼까?";
    }
}
