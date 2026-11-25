using System.Collections.Generic;
using UnityEngine;

// Keeps the sprite and tap collider in sync with the owned building level.
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingTapTarget), typeof(SpriteRenderer))]
public sealed class BuildingLevelVisuals : MonoBehaviour
{
    [Header("Scene reference")]
    [SerializeField] private GameController gameController;

    [Header("Building level sprites")]
    [SerializeField] private Sprite level1Sprite;
    [SerializeField] private Sprite level2Sprite;
    [SerializeField] private Sprite level3Sprite;

    private BuildingTapTarget tapTarget;
    private SpriteRenderer spriteRenderer;
    private PolygonCollider2D tapCollider;

    private void Awake()
    {
        tapTarget = GetComponent<BuildingTapTarget>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        tapCollider = GetComponent<PolygonCollider2D>();
    }

    private void OnEnable()
    {
        if (gameController == null)
            gameController = FindFirstObjectByType<GameController>();

        if (gameController != null)
            gameController.BuildingUpgraded += OnBuildingUpgraded;

        SyncOwnedLevel();
    }

    private void Start() => SyncOwnedLevel();

    private void OnDisable()
    {
        if (gameController != null)
            gameController.BuildingUpgraded -= OnBuildingUpgraded;
    }

    private void OnBuildingUpgraded(BuildingType type)
    {
        if (tapTarget != null && tapTarget.Type == type)
            SyncOwnedLevel();
    }
    
    
    public Sprite GetSpriteForLevel(int level)
    {
        return level switch
        {
            1 => level1Sprite,
            2 => level2Sprite,
            3 => level3Sprite,
            _ => null
        };
    }


    // Public so BuildingUnlockVisuals can restore the correct owned level
    // after switching from a construction plot to a finished building.
    public void ApplyLevel(int level)
    {
        Sprite sprite = GetSpriteForLevel(level);

        if (sprite == null)
        {
            Debug.LogWarning($"No sprite assigned for {tapTarget.Type} Level {level}.", this);
            return;
        }

        if (spriteRenderer.sprite == sprite) return;
        spriteRenderer.sprite = sprite;
        UpdateCollider(sprite);
    }

    private void SyncOwnedLevel()
    {
        if (gameController == null || !gameController.IsInitialized || tapTarget == null)
            return;

        BuildingData data = gameController.Data.GetBuilding(tapTarget.Type);
        if (data != null && data.owned)
            ApplyLevel(data.level);
    }

    private void UpdateCollider(Sprite sprite)
    {
        if (tapCollider == null) return;

        int count = sprite.GetPhysicsShapeCount();
        tapCollider.offset = Vector2.zero;
        tapCollider.pathCount = Mathf.Max(1, count);

        if (count == 0)
        {
            Bounds bounds = sprite.bounds;
            tapCollider.SetPath(0, new[]
            {
                new Vector2(bounds.min.x, bounds.min.y),
                new Vector2(bounds.min.x, bounds.max.y),
                new Vector2(bounds.max.x, bounds.max.y),
                new Vector2(bounds.max.x, bounds.min.y)
            });
            return;
        }

        var points = new List<Vector2>();
        for (int i = 0; i < count; i++)
        {
            points.Clear();
            sprite.GetPhysicsShape(i, points);
            tapCollider.SetPath(i, points);
        }
    }
}
