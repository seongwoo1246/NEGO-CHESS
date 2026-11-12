using System;
using DG.Tweening;
using UnityEngine;

public class PieceAnimator : MonoBehaviour
{
    [Tooltip("위쪽 방향. 보드가 XY 평면에 누워 있으면 Vector3.back 등으로 바꾸세요")]
    [SerializeField] private Vector3 up = Vector3.up;
    [SerializeField] private float dropHeight = 0.6f, dropTime = 0.3f;
    [SerializeField] private float hopHeight = 0.15f, hopTime = 0.35f;
    [SerializeField] private float slideTime = 0.25f;
    [SerializeField] private float removeRise = 0.5f, removeTime = 0.35f;

    private SpriteRenderer[] sprites;
    private Vector3 baseScale;
    private Vector3 home;                 // 이 기물이 있어야 할 월드 좌표
    private Tween hop, slide, drop;
    private Sequence remove;
    private Collider2D col;
    private void Awake()
    {
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        baseScale = transform.localScale;
        col = GetComponent<Collider2D>();
    }

    // Despawn(SetActive false)될 때 전부 정리 → 풀에서 재사용돼도 안전
    private void OnDisable() => KillAll();

    private void KillAll()
    {
        hop?.Kill(); slide?.Kill(); drop?.Kill(); remove?.Kill();
        hop = slide = drop = null; remove = null;
    }

    private void ResetVisual()
    {
        transform.localScale = baseScale;
        SetAlpha(1f);
        if (col != null) col.enabled = true;
    }

    private void SetAlpha(float a)
    {
        foreach (var s in sprites) { var c = s.color; c.a = a; s.color = c; }
    }

    /// <summary>소환: 위에서 살짝 내려옴</summary>
    public void PlaySpawn(Vector3 worldPos)
    {
        KillAll(); ResetVisual();
        home = worldPos;
        transform.position = home + up * dropHeight;
        drop = transform.DOMove(home, dropTime).SetEase(Ease.OutQuad).SetLink(gameObject);
    }

    /// <summary>선택: 제자리 점프 반복 / 해제: 원위치</summary>
    public void SetSelected(bool on)
    {
        hop?.Kill(); hop = null;
        if (on)
        {
            hop = transform.DOMove(home + up * hopHeight, hopTime)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        }
        else if (slide == null || !slide.IsActive())   // 이동 중이면 이동 트윈이 마무리하게 둠
        {
            slide = transform.DOMove(home, 0.1f).SetLink(gameObject);
        }
    }

    /// <summary>이동: 쭉 미끄러짐 (논리 좌표는 이미 갱신된 상태)</summary>
    public void SlideTo(Vector3 worldPos)
    {
        hop?.Kill(); hop = null;
        slide?.Kill();
        home = worldPos;
        slide = transform.DOMove(home, slideTime).SetEase(Ease.OutCubic).SetLink(gameObject);
    }

    /// <summary>제거: 위로 올라가며 서서히 사라진 뒤 onDone(풀 반환)</summary>
    public void PlayRemove(Action onDone)
    {
        KillAll();
        if (col != null) col.enabled = false;
        remove = DOTween.Sequence().SetLink(gameObject);
        remove.Append(transform.DOMove(transform.position + up * removeRise, removeTime).SetEase(Ease.OutQuad));
        if (sprites.Length > 0)
            remove.Join(DOTween.To(() => 1f, SetAlpha, 0f, removeTime));          // 2D: 서서히 투명
        else
            remove.Join(transform.DOScale(baseScale * 0.2f, removeTime));         // 3D 불투명 재질: 작아지며 사라짐
        remove.OnComplete(() => { ResetVisual(); onDone?.Invoke(); });
    }

    /// <summary>애니메이션 없이 즉시 이동. home도 같이 갱신해야 선택/해제 때 옛 자리로 돌아가지 않습니다.</summary>
    public void SnapTo(Vector3 worldPos)
    {
        KillAll();
        ResetVisual();
        home = worldPos;
        transform.position = worldPos;
    }
}