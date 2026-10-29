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
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private BuildingPopupView popupView;
    [SerializeField, Min(0)] private float buildingOffset = 12;

    private sealed class Slot
    {
        public VisitorSlotView view;
        public NeedUnit unit;
    }

    private readonly List<Slot> slots = new List<Slot>();
    private readonly Dictionary<NeedUnit, Sprite> guestPortraits = new Dictionary<NeedUnit, Sprite>();
    private readonly System.Random portraitRandom = new System.Random();
    private BuildingTapTarget selected;
    private WaveData displayedWave;
    private Vector3 popupWorldAnchor;

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
        Vector3 screen = worldCamera.WorldToScreenPoint(building.AnchorPosition);
        if (screen.z <= 0) return;
        if (selected != building)
        {
            selected = building;
            ClearBindings();
        }
        // Convert the UI gap once; the resulting anchor stays fixed in the world.
        screen.y += buildingOffset * canvasRect.lossyScale.y;
        popupWorldAnchor = worldCamera.ScreenToWorldPoint(screen);
        RefreshPopup();
    }

    public void Hide()
    {
        selected = null;
        ClearBindings();
        if (popupView != null) popupView.gameObject.SetActive(false);
    }

    private void DismissOutside(Vector2 screenPosition)
    {
        if (selected != null && !RectTransformUtility.RectangleContainsScreenPoint(
            popupView.PopupRect, screenPosition, null)) Hide();
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
        // The overlay stays above the game while following this fixed world location as the camera pans.
        bool visible = Project(popupWorldAnchor, out Vector2 position);
        popupView.gameObject.SetActive(visible);
        if (visible) popupView.PopupRect.anchoredPosition = position;
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
