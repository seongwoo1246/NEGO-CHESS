using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>책자의 한 쪽: 글 + (선택) 그림</summary>
[Serializable]
public class BookPage
{
    [TextArea(3, 12)] public string body;
    [Tooltip("비워 두면 그림 없이 글만 보여 줍니다.")]
    public Sprite image;
    [Tooltip("Resources/Manual 폴더의 그림 파일 이름 (확장자 없이). image가 비어 있을 때만 사용합니다.")]
    public string imageName;

    public static implicit operator BookPage(string body) => new BookPage { body = body };
}

/// <summary>책자의 한 장(목차 항목): 제목 + 여러 쪽</summary>
[Serializable]
public class BookChapter
{
    public string title;
    public List<BookPage> pages = new List<BookPage>();
}

/// <summary>
/// 게임 설명서(튜토리얼 책자).
///  - 내용은 인스펙터의 Chapters(장) → Pages(쪽) 목록에 적습니다. 멀티 설명도 장만 더 추가하면 됩니다.
///  - 목차 버튼은 장 목록대로 자동으로 만들어집니다.
///  - 이전/다음 버튼은 쪽을 넘기다가 장이 끝나면 다음 장으로 이어집니다.
/// 주의: 이 스크립트는 항상 켜져 있는 오브젝트에 붙이고, Book Root(책자 패널)는 별도의 자식 오브젝트로 두세요.
/// </summary>
public class TutorialBookM : MonoBehaviour
{
    [Header("내용 (장 → 쪽)")]
    [SerializeField] private List<BookChapter> chapters = new List<BookChapter>();

    [Header("열기 / 닫기")]
    [Tooltip("책자 패널 전체. 이 스크립트가 붙은 오브젝트와 달라야 합니다.")]
    [SerializeField] private GameObject bookRoot;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("목차")]
    [SerializeField] private List<Button> _chapterButtons = new List<Button>();

    [Header("본문")]
    [SerializeField] private TMP_Text chapterTitleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image pageImage;
    [SerializeField] private TMP_Text pageIndicatorText;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;

   
    private int _chapter;
    private int _page;

    // ───────────── 켜기 ─────────────

    private void Awake()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (prevButton != null) prevButton.onClick.AddListener(Prev);
        if (nextButton != null) nextButton.onClick.AddListener(Next);

        FillSingleDefaults();
        RebuildChapterList();
        if (bookRoot != null && bookRoot != gameObject) bookRoot.SetActive(false);
    }

    // ───────────── 열기 / 닫기 ─────────────

    public void Open()
    {
        if (bookRoot != null) bookRoot.SetActive(true);
        Show(0, 0);
    }

    public void Close()
    {
        if (bookRoot != null && bookRoot != gameObject) bookRoot.SetActive(false);
    }

    // ───────────── 넘기기 ─────────────

    public void Next()
    {
        if (chapters.Count == 0) return;

        int count = chapters[_chapter].pages != null ? chapters[_chapter].pages.Count : 0;
        if (_page < count - 1) Show(_chapter, _page + 1);
        else if (_chapter < chapters.Count - 1) Show(_chapter + 1, 0);
    }

    public void Prev()
    {
        if (chapters.Count == 0) return;

        if (_page > 0)
        {
            Show(_chapter, _page - 1);
        }
        else if (_chapter > 0)
        {
            List<BookPage> prevPages = chapters[_chapter - 1].pages;
            int prevCount = prevPages != null ? prevPages.Count : 0;
            Show(_chapter - 1, Mathf.Max(0, prevCount - 1));
        }
    }

    // ───────────── 화면 갱신 ─────────────

    private void Show(int chapter, int page)
    {
        if (chapters.Count == 0)
        {
            if (chapterTitleText != null) chapterTitleText.text = "";
            if (bodyText != null) bodyText.text = "아직 내용이 없어요.";
            if (pageImage != null) pageImage.gameObject.SetActive(false);
            if (pageIndicatorText != null) pageIndicatorText.text = "";
            if (prevButton != null) prevButton.interactable = false;
            if (nextButton != null) nextButton.interactable = false;
            return;
        }

        _chapter = Mathf.Clamp(chapter, 0, chapters.Count - 1);
        BookChapter ch = chapters[_chapter];
        int pageCount = ch.pages != null ? ch.pages.Count : 0;
        _page = pageCount == 0 ? 0 : Mathf.Clamp(page, 0, pageCount - 1);

        BookPage current = pageCount > 0 ? ch.pages[_page] : null;

        if (chapterTitleText != null) chapterTitleText.text = ch.title;
        if (bodyText != null) bodyText.text = current != null ? current.body : "";

        Sprite sprite = ResolveSprite(current);
        if (pageImage != null)
        {
            bool hasImage = sprite != null;
            pageImage.gameObject.SetActive(hasImage);
            if (hasImage)
            {
                pageImage.sprite = sprite;
                pageImage.preserveAspect = true;
            }
        }

        if (pageIndicatorText != null)
            pageIndicatorText.text = pageCount > 0 ? $"{_page + 1} / {pageCount}" : "";

        if (prevButton != null) prevButton.interactable = _chapter > 0 || _page > 0;
        if (nextButton != null) nextButton.interactable = _chapter < chapters.Count - 1 || _page < pageCount - 1;

        // 지금 보고 있는 장의 목차 버튼은 눌러도 소용없으니 잠가서 "선택됨"처럼 보이게 합니다.
        for (int i = 0; i < _chapterButtons.Count; i++)
        {
            if (_chapterButtons[i] != null) _chapterButtons[i].interactable = i != _chapter;
        }
    }

    private void RebuildChapterList()
    {
        for (int i = 0; i < _chapterButtons.Count; i++)
        {
            int index = i;   // 람다가 올바른 번호를 기억하도록 복사
            _chapterButtons[i].onClick.AddListener(() => Show(index, 0));
        }
    }

    private static Sprite ResolveSprite(BookPage page)
    {
        if (page == null) return null;
        if (page.image != null) return page.image;
        if (string.IsNullOrEmpty(page.imageName)) return null;
        return Resources.Load<Sprite>("Manual/" + page.imageName);
    }
    // ───────────── 에디터 도우미 ─────────────

    /// <summary>비어 있을 때만 싱글 설명 초안을 채웁니다. (이미 입력한 내용은 절대 덮어쓰지 않습니다)</summary>
    [ContextMenu("싱글 설명 초안 채우기")]
    private void FillSingleDefaults()
    {
        if (chapters.Count > 0)
        {
            Debug.LogWarning("[TutorialBook] 이미 내용이 있어서 덮어쓰지 않았어요. 채우려면 Chapters 목록을 먼저 비워 주세요.");
            return;
        }

        TutorialBookDefaults.FillSingle(chapters);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log("[TutorialBook] 싱글 설명 초안을 채웠어요. 실제 게임과 다른 부분은 인스펙터에서 고쳐 주세요.");
    }
}