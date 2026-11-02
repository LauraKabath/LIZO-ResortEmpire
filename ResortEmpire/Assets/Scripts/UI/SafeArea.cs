using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class SafeArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        if (Screen.safeArea != lastSafeArea ||
            new Vector2Int(Screen.width, Screen.height) != lastScreenSize)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        Rect safe = Screen.safeArea;

        if (Screen.width <= 0 || Screen.height <= 0)
            return;

        rectTransform.anchorMin = new Vector2(
            safe.xMin / Screen.width,
            safe.yMin / Screen.height
        );

        rectTransform.anchorMax = new Vector2(
            safe.xMax / Screen.width,
            safe.yMax / Screen.height
        );

        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        lastSafeArea = safe;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }
}