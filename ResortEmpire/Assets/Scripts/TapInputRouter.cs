using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>Routes taps to 2D world objects and drags to the orthographic camera.</summary>
public sealed class TapInputRouter : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask targetLayers = Physics2D.AllLayers;

    [Header("Camera dragging")]
    [Tooltip("Movement in screen pixels before a press becomes a camera drag.")]
    [SerializeField, Min(0f)] private float dragThresholdPixels = 12f;

    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private bool isTracking;
    private bool isDragging;
    private bool trackingTouch;
    private int activeTouchId;
    private Vector2 pressPosition;
    private Vector2 previousPosition;

    private void OnEnable() => EnhancedTouchSupport.Enable();

    private void OnDisable()
    {
        ResetGesture();
        EnhancedTouchSupport.Disable();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            ResetGesture();
    }

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void Update()
    {
        if (worldCamera == null)
            return;

        var touches = Touch.activeTouches;
        if (touches.Count > 0)
        {
            if (!trackingTouch)
                ResetGesture();

            // Cancel multi-finger gestures until a fresh press, avoiding camera jumps.
            if (touches.Count != 1)
            {
                ResetGesture();
                return;
            }

            Touch touch = touches[0];
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                BeginGesture(touch.screenPosition);
                trackingTouch = true;
                activeTouchId = touch.touchId;
            }

            if (!trackingTouch || touch.touchId != activeTouchId || !isTracking)
                return;

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                ResetGesture();
            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
                EndGesture(touch.screenPosition);
            else
                MoveGesture(touch.screenPosition);

            // Touch takes priority over mouse input, including simulated touches.
            return;
        }

        if (trackingTouch)
            ResetGesture();

        // Mouse input provides a convenient Editor testing path.
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            ResetGesture();
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame)
            BeginGesture(mousePosition);

        if (!isTracking)
            return;

        if (mouse.leftButton.wasReleasedThisFrame)
            EndGesture(mousePosition);
        else if (mouse.leftButton.isPressed)
            MoveGesture(mousePosition);
        else
            ResetGesture();
    }

    private void BeginGesture(Vector2 screenPosition)
    {
        ResetGesture();
        if (IsOverUI(screenPosition))
            return;

        isTracking = true;
        pressPosition = screenPosition;
        previousPosition = screenPosition;
    }

    private void MoveGesture(Vector2 screenPosition)
    {
        if (!isDragging && (screenPosition - pressPosition).sqrMagnitude > dragThresholdPixels * dragThresholdPixels)
            isDragging = true;

        if (isDragging)
        {
            // Convert both points with the same camera pose. Moving opposite the
            // finger makes the world follow it, at the current zoom level.
            Vector3 previousWorld = worldCamera.ScreenToWorldPoint(previousPosition);
            Vector3 currentWorld = worldCamera.ScreenToWorldPoint(screenPosition);
            Vector3 position = worldCamera.transform.position;
            position.x += previousWorld.x - currentWorld.x;
            position.y += previousWorld.y - currentWorld.y;

            worldCamera.transform.position = position;
        }

        previousPosition = screenPosition;
    }

    private void EndGesture(Vector2 screenPosition)
    {
        // Include movement reported on the release frame before deciding to tap.
        MoveGesture(screenPosition);
        bool shouldTap = !isDragging && !IsOverUI(screenPosition);
        ResetGesture();
        if (shouldTap)
            TryTap(screenPosition);
    }

    private void ResetGesture()
    {
        isTracking = false;
        isDragging = false;
        trackingTouch = false;
    }

    private bool IsOverUI(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var pointer = new PointerEventData(eventSystem) { position = screenPosition };
        uiHits.Clear();
        eventSystem.RaycastAll(pointer, uiHits);
        foreach (RaycastResult hit in uiHits)
        {
            // The camera also has a Physics2DRaycaster; world hits must stay draggable.
            if (hit.module is GraphicRaycaster)
                return true;
        }

        return false;
    }

    private void TryTap(Vector2 screenPosition)
    {
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);
        Collider2D hit = Physics2D.OverlapPoint(
            new Vector2(worldPosition.x, worldPosition.y), targetLayers);
        if (hit == null)
            return;

        // Search up the hierarchy so child colliders can use a parent behaviour.
        for (Transform current = hit.transform; current != null; current = current.parent)
        {
            foreach (MonoBehaviour behaviour in current.GetComponents<MonoBehaviour>())
            {
                if (behaviour is ITapTarget target)
                {
                    target.OnTapped();
                    return;
                }
            }
        }
    }
}
