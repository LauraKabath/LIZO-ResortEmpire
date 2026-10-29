using UnityEngine;
using UnityEngine.UI;

// The prefab owns appearance; only the active slot count and grid arrangement change in play.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BuildingPopupView : MonoBehaviour
{
    [SerializeField] private Text buildingName;
    [SerializeField] private RectTransform slotsContainer;
    [SerializeField] private VisitorSlotView[] visitorSlots = new VisitorSlotView[0];
    [SerializeField] private Sprite[] guestPortraitSprites = new Sprite[0];

    [Header("Slot layout")]
    [SerializeField, Min(1)] private float maximumSlotSize = 86;
    [SerializeField, Min(0)] private float slotSpacing = 12;

    [Header("Editor preview")]
    [SerializeField] private string previewBuildingName = "Reception";
    [SerializeField, Min(1)] private int previewCapacity = 7;

    private int activeSlotCount = -1;
    private bool layoutDirty = true;

    public RectTransform PopupRect => (RectTransform)transform;
    public VisitorSlotView[] VisitorSlots => visitorSlots;
    public Sprite[] GuestPortraits => guestPortraitSprites;
    public bool IsConfigured
    {
        get
        {
            if (buildingName == null || slotsContainer == null || visitorSlots == null || visitorSlots.Length == 0)
                return false;
            foreach (VisitorSlotView slot in visitorSlots) if (slot == null || !slot.IsConfigured) return false;
            return true;
        }
    }

    private void OnEnable() => layoutDirty = true;
    private void OnValidate() => layoutDirty = true;
    private void OnRectTransformDimensionsChange() => layoutDirty = true;

    private void Update()
    {
        if (!layoutDirty || !IsConfigured) return;
        if (!Application.IsPlaying(gameObject))
        {
            SetCapacity(Mathf.Clamp(previewCapacity, 1, visitorSlots.Length));
            SetBuildingName(previewBuildingName);
            foreach (VisitorSlotView slot in visitorSlots) slot.ShowEmpty();
        }
        else if (activeSlotCount > 0) LayoutSlots(activeSlotCount);
    }

    public void SetBuildingName(string value)
    {
        if (buildingName.text != value) buildingName.text = value;
    }

    public bool SetCapacity(int capacity)
    {
        if (!IsConfigured || capacity < 1 || capacity > visitorSlots.Length) return false;
        if (activeSlotCount == capacity && !layoutDirty) return true;
        activeSlotCount = capacity;
        for (int index = 0; index < visitorSlots.Length; index++)
        {
            bool visible = index < capacity;
            if (visitorSlots[index].gameObject.activeSelf != visible)
                visitorSlots[index].gameObject.SetActive(visible);
        }
        LayoutSlots(capacity);
        return true;
    }

    private void LayoutSlots(int capacity)
    {
        slotsContainer.ForceUpdateRectTransforms();
        float width = slotsContainer.rect.width;
        float height = slotsContainer.rect.height;
        int columns = capacity <= 3 ? capacity : Mathf.Min(4, Mathf.CeilToInt(capacity / 2f));
        int rows = Mathf.CeilToInt(capacity / (float)columns);
        float diameter = Mathf.Max(1, Mathf.Min(maximumSlotSize,
            (width - (columns - 1) * slotSpacing) / columns,
            (height - (rows - 1) * slotSpacing) / rows));
        float usedHeight = rows * diameter + (rows - 1) * slotSpacing;
        float top = (height - usedHeight) * 0.5f;
        for (int index = 0; index < capacity; index++)
        {
            int row = index / columns;
            int count = Mathf.Min(columns, capacity - row * columns);
            RectTransform slot = visitorSlots[index].SlotRect;
            slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 1);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.sizeDelta = Vector2.one * diameter;
            slot.anchoredPosition = new Vector2(((index % columns) - (count - 1) * 0.5f) * (diameter + slotSpacing),
                -top - diameter * 0.5f - row * (diameter + slotSpacing));
        }
        layoutDirty = false;
    }
}
