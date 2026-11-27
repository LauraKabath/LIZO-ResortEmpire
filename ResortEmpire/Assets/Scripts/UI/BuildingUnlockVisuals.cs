using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BuildingUnlockVisuals : MonoBehaviour
{
    [Serializable]
    private sealed class Plot
    {
        [Tooltip("Zamknutá oblasť, kým sa budova nedá kúpiť.")]
        public GameObject lockedVisual;
        public BuildingTapTarget tapTarget;

        private BuildingLevelVisuals levelVisuals;
        private SpriteRenderer spriteRenderer;
        private PolygonCollider2D tapCollider;
        private Sprite finishedSprite;
        private Vector2[][] finishedColliderPaths;
        private Vector2 finishedColliderOffset;

        public void Initialize()
        {
            levelVisuals = tapTarget.GetComponent<BuildingLevelVisuals>();
            spriteRenderer = tapTarget.GetComponent<SpriteRenderer>();
            tapCollider = tapTarget.GetComponent<PolygonCollider2D>();
            finishedSprite = spriteRenderer.sprite;
            finishedColliderPaths = new Vector2[tapCollider.pathCount][];
            for (int i = 0; i < finishedColliderPaths.Length; i++)
                finishedColliderPaths[i] = tapCollider.GetPath(i);
            finishedColliderOffset = tapCollider.offset;
        }

        public void Show(BuildingData building, Sprite constructionSprite)
        {
            bool locked = !building.owned && !building.availableToPurchase;
            if (lockedVisual != null) lockedVisual.SetActive(locked);

            tapTarget.gameObject.SetActive(!locked);
            if (locked) return;

            // An owned building must use its current level, not the cached Lv1 sprite.
            if (building.owned && levelVisuals != null)
            {
                levelVisuals.ApplyLevel(building.level);
                return;
            }

            // Preserve the original construction-site and fallback behavior.
            bool showConstruction = !building.owned && constructionSprite != null;
            Sprite sprite = showConstruction ? constructionSprite : finishedSprite;
            if (spriteRenderer.sprite != sprite)
            {
                spriteRenderer.sprite = sprite;
                if (showConstruction) SetConstructionCollider(sprite);
                else RestoreFinishedCollider();
            }
        }

        private void SetConstructionCollider(Sprite sprite)
        {
            int shapeCount = sprite.GetPhysicsShapeCount();
            tapCollider.offset = Vector2.zero;
            tapCollider.pathCount = Mathf.Max(1, shapeCount);
            if (shapeCount == 0)
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

            for (int i = 0; i < shapeCount; i++)
                tapCollider.SetPath(i, sprite.GetPhysicsShape(i).ToArray());
        }

        private void RestoreFinishedCollider()
        {
            tapCollider.offset = finishedColliderOffset;
            tapCollider.pathCount = finishedColliderPaths.Length;
            for (int i = 0; i < finishedColliderPaths.Length; i++)
                tapCollider.SetPath(i, finishedColliderPaths[i]);
        }
    }

    [SerializeField] private GameController gameController;
    [SerializeField] private BuildingPurchasePopup purchasePopup;
    [SerializeField] private Plot[] plots;

    private void Awake()
    {
        foreach (Plot plot in plots) plot.Initialize();
    }

    private void OnEnable()
    {
        gameController.PhaseChanged += OnPhaseChanged;
        if (purchasePopup != null) purchasePopup.Purchased += OnPurchased;
        Refresh();
    }

    private void OnDisable()
    {
        gameController.PhaseChanged -= OnPhaseChanged;
        if (purchasePopup != null) purchasePopup.Purchased -= OnPurchased;
    }

    private void OnPhaseChanged(ResortPhase phase) => Refresh();
    private void OnPurchased(BuildingType type) => Refresh();

    private void Refresh()
    {
        if (!gameController.IsInitialized) return;

        foreach (Plot plot in plots)
        {
            BuildingType type = plot.tapTarget.Type;
            BuildingData building = gameController.Data.GetBuilding(type);
            if (building == null) continue;

            plot.Show(building, gameController.GetBuildingDefinition(type).ConstructionSiteSprite);
        }
    }
}
