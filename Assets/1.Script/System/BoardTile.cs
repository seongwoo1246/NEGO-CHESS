using UnityEngine;

public class BoardTile : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // 초기 색상 설정 및 저장
    public void SetBaseColor(Color color)
    {
        originalColor = color;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }

    // 하이라이트 색상으로 변경
    public void ChangeColor(Color color)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
            Debug.Log($"[성공] {gameObject.name} 색상 변경됨: {color}");
        }
        else
        {
            Debug.LogWarning($"[실패] {gameObject.name} SpriteRenderer가 비어있음!");
        }
    }

    // 원래 색상으로 원복
    public void ResetColor()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
    }
}
