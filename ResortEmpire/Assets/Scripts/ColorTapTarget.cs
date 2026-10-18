using UnityEngine;

/// <summary>Example tap behaviour: toggle a SpriteRenderer between two colors.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ColorTapTarget : MonoBehaviour, ITapTarget
{
    [SerializeField] private Color tappedColor = Color.green;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isTapped;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;
    }

    public void OnTapped()
    {
        isTapped = !isTapped;
        spriteRenderer.color = isTapped ? tappedColor : originalColor;
    }
}
