using UnityEngine;

/// <summary>Keeps the entire orthographic camera view inside a rectangular area.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraBounds : MonoBehaviour
{
    [Tooltip("An unrotated Box Collider 2D defining the visible world area.")]
    [SerializeField] private BoxCollider2D cameraBound = null;

    private Camera worldCamera;

    private void Awake() => worldCamera = GetComponent<Camera>();

    private void LateUpdate()
    {
        if (cameraBound == null || !worldCamera.orthographic)
            return;

        Bounds bounds = cameraBound.bounds;
        float maxSize = Mathf.Min(bounds.extents.y, bounds.extents.x / worldCamera.aspect);
        if (maxSize <= 0f)
            return;

        // Zoom in only if the current view is too large for the rectangle.
        worldCamera.orthographicSize = Mathf.Min(worldCamera.orthographicSize, maxSize);
        float halfHeight = worldCamera.orthographicSize;
        float halfWidth = halfHeight * worldCamera.aspect;

        Vector3 position = worldCamera.transform.position;
        position.x = Mathf.Clamp(position.x, bounds.min.x + halfWidth, bounds.max.x - halfWidth);
        position.y = Mathf.Clamp(position.y, bounds.min.y + halfHeight, bounds.max.y - halfHeight);
        worldCamera.transform.position = position;
    }
}
