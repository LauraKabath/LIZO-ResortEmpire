using UnityEngine;
using UnityEngine.UI;

// An editable UI graphic, with no generated textures or additional objects.
[AddComponentMenu("UI/Service Progress Ring")]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ServiceProgressRing : MaskableGraphic
{
    [SerializeField, Range(0, 1)] private float fillAmount;
    [SerializeField, Range(0.01f, 0.25f)] private float thickness = 0.05f;
    [SerializeField] private Color trackColor = new Color(0.40f, 0.30f, 0.15f, 0.23f);

    public float FillAmount
    {
        get => fillAmount;
        set
        {
            float next = Mathf.Clamp01(value);
            if (Mathf.Approximately(fillAmount, next)) return;
            fillAmount = next;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        Rect rect = GetPixelAdjustedRect();
        float outer = Mathf.Min(rect.width, rect.height) * 0.5f;
        float inner = outer * (1 - Mathf.Clamp(thickness, 0.01f, 0.25f) * 2);
        Color track = trackColor;
        track.a *= color.a;
        AddArc(vertices, rect.center, inner, outer, 1, track);
        AddArc(vertices, rect.center, inner, outer, Mathf.Clamp01(fillAmount), color);
    }

    private static void AddArc(VertexHelper vertices, Vector2 center, float inner,
        float outer, float amount, Color tint)
    {
        if (amount <= 0 || outer <= 0) return;
        int steps = Mathf.Max(1, Mathf.CeilToInt(96 * amount));
        for (int index = 0; index < steps; index++)
        {
            float start = Mathf.PI * 0.5f - Mathf.PI * 2 * amount * index / steps;
            float end = Mathf.PI * 0.5f - Mathf.PI * 2 * amount * (index + 1) / steps;
            Vector2 a = new Vector2(Mathf.Cos(start), Mathf.Sin(start));
            Vector2 b = new Vector2(Mathf.Cos(end), Mathf.Sin(end));
            int first = vertices.currentVertCount;
            vertices.AddVert(center + a * outer, tint, Vector2.zero);
            vertices.AddVert(center + b * outer, tint, Vector2.zero);
            vertices.AddVert(center + b * inner, tint, Vector2.zero);
            vertices.AddVert(center + a * inner, tint, Vector2.zero);
            vertices.AddTriangle(first, first + 1, first + 2);
            vertices.AddTriangle(first + 2, first + 3, first);
        }
    }
}
