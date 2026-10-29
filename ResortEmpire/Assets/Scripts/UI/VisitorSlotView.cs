using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class VisitorSlotView : MonoBehaviour
{
    [SerializeField] private Image portraitImage;
    [SerializeField] private Sprite emptySlotSprite;
    [SerializeField] private ServiceProgressRing progressRing;

    public RectTransform SlotRect => (RectTransform)transform;
    public bool IsConfigured => portraitImage != null && emptySlotSprite != null && progressRing != null;

    public void ShowGuest(Sprite portrait, float progress)
    {
        SetPortrait(portrait != null ? portrait : emptySlotSprite);
        if (!progressRing.gameObject.activeSelf) progressRing.gameObject.SetActive(true);
        progressRing.FillAmount = progress;
    }

    public void ShowEmpty()
    {
        SetPortrait(emptySlotSprite);
        progressRing.FillAmount = 0;
        if (progressRing.gameObject.activeSelf) progressRing.gameObject.SetActive(false);
    }

    private void SetPortrait(Sprite sprite)
    {
        if (portraitImage.sprite != sprite) portraitImage.sprite = sprite;
        portraitImage.enabled = sprite != null;
    }
}
