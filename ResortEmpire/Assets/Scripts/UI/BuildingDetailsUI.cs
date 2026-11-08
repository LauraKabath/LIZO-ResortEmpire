using System;
using System.Collections.Generic;
using UnityEngine;

// Scene controller for the authored popup. No UI objects or textures are created at runtime.
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class BuildingDetailsUI : MonoBehaviour
{
    [SerializeField] private GameController gameController;
    [SerializeField] private TapInputRouter tapInputRouter;
    [SerializeField] private Camera worldCamera;
    [Tooltip("The popup's parent. It stays inside this area, so use the Safe Area when the scene has one.")]
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private BuildingPopupView popupView;
    [SerializeField, Min(0)] private float buildingOffset = 12;
    [Tooltip("UI the popup must not cover. Inactive objects are ignored.")]
    [SerializeField] private RectTransform[] avoidedUI = new RectTransform[0];

    private sealed class Slot
    {
        public VisitorSlotView view;
        public NeedUnit unit;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private readonly Dictionary<NeedUnit, Sprite> guestPortraits = new Dictionary<NeedUnit, Sprite>();
    private readonly System.Random portraitRandom = new System.Random();
    private readonly List<Rect> avoidedRects = new List<Rect>();
    private BuildingTapTarget selected;
    private WaveData displayedWave;
    private Vector2 popupOffset;

    // Lets panels such as Worker Allocation react to the building chosen by a tap.
    public event Action<BuildingType> BuildingSelected;

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        if (canvasRect == null || popupView == null || !popupView.IsConfigured)
        {
            Debug.LogError("Assign the scene Canvas and configured Building Popup prefab to Building Details UI.", this);
            enabled = false;
            return;
        }
        Hide();
    }

    private void OnEnable()
    {
        if (tapInputRouter == null) return;
        tapInputRouter.WorldTapped += DismissOutside;
        tapInputRouter.UiPressed += DismissOutside;
    }

    private void OnDisable()
    {
        if (tapInputRouter != null)
        {
            tapInputRouter.WorldTapped -= DismissOutside;
            tapInputRouter.UiPressed -= DismissOutside;
        }
        Hide();
    }

    private void LateUpdate()
    {
        if (gameController == null || !gameController.IsInitialized || worldCamera == null) return;
        if (selected != null) RefreshPopup();
    }

    public void ShowBuilding(BuildingTapTarget building)
    {
        if (!isActiveAndEnabled || building == null || gameController == null ||
            !gameController.IsInitialized || worldCamera == null) return;
        BuildingData state = gameController.Data.GetBuilding(building.Type);
        if (state == null) return;
        // Locked plots stay selectable for future unlocking, but offer no guest service to show.
        if (!state.owned)
        {
            Hide();
            BuildingSelected?.Invoke(building.Type);
            return;
        }
        if (selected != building)
        {
            selected = building;
            ClearBindings();
        }
        ChoosePopupOffset();
        RefreshPopup();
        if (selected != null) BuildingSelected?.Invoke(selected.Type);
    }

    public void Hide()
    {
        selected = null;
        ClearBindings();
        if (popupView != null) popupView.gameObject.SetActive(false);
    }

    private void DismissOutside(Vector2 screenPosition)
    {
        // A popup hidden by panning still has a rect, but must not swallow the tap that closes it.
        if (selected != null && (!popupView.gameObject.activeSelf ||
            !RectTransformUtility.RectangleContainsScreenPoint(
                popupView.PopupRect, screenPosition, null))) Hide();
    }

    private void RefreshPopup()
    {
        BuildingData state = gameController.Data.GetBuilding(selected.Type);
        if (state == null) { Hide(); return; }
        BuildingDefinition definition = gameController.GetBuildingDefinition(selected.Type);
        int capacity = definition.GetCapacity(state.level);
        if (!popupView.SetCapacity(capacity))
        {
            Debug.LogError("The Building Popup prefab needs at least " + capacity + " visitor slots.", this);
            Hide();
            return;
        }
        if (slots.Count != capacity)
        {
            slots.Clear();
            for (int index = 0; index < capacity; index++)
                slots.Add(new Slot { view = popupView.VisitorSlots[index] });
        }
        if (displayedWave != gameController.ActiveWave)
        {
            displayedWave = gameController.ActiveWave;
            guestPortraits.Clear();
            ClearBindings();
        }
        BindUnits(displayedWave?.GetNeed(definition.ServicedNeed));
        popupView.SetBuildingName(definition.DisplayName);
        foreach (Slot slot in slots)
        {
            if (state.owned && slot.unit != null)
                slot.view.ShowGuest(GetGuestPortrait(slot.unit), (float)slot.unit.serviceProgress);
            else slot.view.ShowEmpty();
        }
        // The overlay stays above the game while following the building as the camera pans.
        bool visible = TryFollowBuilding(out Vector2 position);
        popupView.gameObject.SetActive(visible);
        if (visible) popupView.PopupRect.anchoredPosition = position;
    }

    // Picks the popup's side once per tap, so it keeps its position relative to the building.
    private void ChoosePopupOffset()
    {
        if (!TryGetBuildingRect(out Rect target)) return;
        CollectAvoidedUI();
        Rect placed = PopupPlacement.Place(popupView.PopupRect.rect.size, target, canvasRect.rect,
            avoidedRects, buildingOffset);
        popupOffset = placed.position - target.center;
    }

    // Returns false while the popup's spot leaves the Safe Area or meets other UI, or the building
    // is off screen. Hiding keeps the popup from jumping; it comes back when the camera does.
    private bool TryFollowBuilding(out Vector2 position)
    {
        position = default;
        Rect area = canvasRect.rect;
        if (!TryGetBuildingRect(out Rect target) || !target.Overlaps(area)) return false;
        RectTransform popup = popupView.PopupRect;
        var placed = new Rect(target.center + popupOffset, popup.rect.size);
        CollectAvoidedUI();
        if (!PopupPlacement.Fits(placed, area, avoidedRects)) return false;

        Vector2 parentAnchor = area.min + Vector2.Scale(area.size, popup.anchorMin);
        position = placed.min + Vector2.Scale(placed.size, popup.pivot) - parentAnchor;
        return true;
    }

    private bool TryGetBuildingRect(out Rect target)
    {
        target = default;
        Bounds bounds = selected.VisualBounds;
        if (!Project(bounds.min, out Vector2 min) || !Project(bounds.max, out Vector2 max) ||
            !Project(selected.AnchorPosition, out Vector2 top)) return false;
        target = Rect.MinMaxRect(min.x, min.y, max.x, Mathf.Max(max.y, top.y));
        return true;
    }

    private void CollectAvoidedUI()
    {
        avoidedRects.Clear();
        foreach (RectTransform ui in avoidedUI)
        {
            if (ui == null || !ui.gameObject.activeInHierarchy) continue;
            Bounds uiBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, ui);
            avoidedRects.Add(new Rect(uiBounds.min, uiBounds.size));
        }
    }

    private void BindUnits(WaveNeedData need)
    {
        foreach (Slot slot in slots)
        {
            if (need == null || (slot.unit != null && !need.inServiceUnits.Contains(slot.unit)))
                slot.unit = null;
        }
        if (need == null) return;
        foreach (NeedUnit unit in need.inServiceUnits)
        {
            bool bound = false;
            foreach (Slot slot in slots) if (slot.unit == unit) { bound = true; break; }
            if (bound) continue;
            foreach (Slot slot in slots)
            {
                if (slot.unit != null) continue;
                slot.unit = unit;
                break;
            }
        }
    }

    private Sprite GetGuestPortrait(NeedUnit unit)
    {
        if (guestPortraits.TryGetValue(unit, out Sprite portrait)) return portrait;
        Sprite[] choices = popupView.GuestPortraits;
        if (choices == null) return null;
        int available = 0;
        foreach (Sprite candidate in choices) if (candidate != null) available++;
        if (available == 0) return null;
        int choice = portraitRandom.Next(available);
        foreach (Sprite candidate in choices)
        {
            if (candidate == null || choice-- != 0) continue;
            guestPortraits.Add(unit, candidate);
            return candidate;
        }
        return null;
    }

    private void ClearBindings()
    {
        foreach (Slot slot in slots)
            slot.unit = null;
    }

    private bool Project(Vector3 world, out Vector2 point)
    {
        Vector3 screen = worldCamera.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out point);
        return screen.z > 0;
    }
}
