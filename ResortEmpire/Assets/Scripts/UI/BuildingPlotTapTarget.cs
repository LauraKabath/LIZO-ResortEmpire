using UnityEngine;

[DisallowMultipleComponent]
public sealed class BuildingPlotTapTarget : MonoBehaviour, ITapTarget
{
    [SerializeField] private BuildingType buildingType;
    [SerializeField] private BuildingPurchasePopup popup;
    [SerializeField] private GameController gameController;
    [Tooltip("Hotová budova (napr. BungalowsLv1_0), zapne sa po kúpe.")]
    [SerializeField] private GameObject builtBuilding;
    [SerializeField] private Renderer buildingRenderer;
    [SerializeField] private Vector3 anchorOffset = new Vector3(0, 0.12f, 0);

    public BuildingType Type => buildingType;
    public Vector3 AnchorPosition
    {
        get
        {
            if (buildingRenderer == null) return transform.position + anchorOffset;
            Bounds b = buildingRenderer.bounds;
            return new Vector3(b.center.x, b.max.y, b.center.z) + anchorOffset;
        }
    }
    public Bounds VisualBounds => buildingRenderer != null
        ? buildingRenderer.bounds
        : new Bounds(transform.position, Vector3.zero);

    private void Awake()
    {
        if (buildingRenderer == null) buildingRenderer = GetComponent<Renderer>();
        if (gameController == null) gameController = FindFirstObjectByType<GameController>();
    }

    private void OnEnable()  { if (popup != null) popup.Purchased += OnPurchased; }
    private void OnDisable() { if (popup != null) popup.Purchased -= OnPurchased; }

    public void OnTapped()
    {
        if (!isActiveAndEnabled || popup == null || gameController == null) return;
        BuildingDefinition def = gameController.GetBuildingDefinition(buildingType);
        if (def == null)
        {
            Debug.LogWarning($"Chýba BuildingDefinition pre {buildingType}", this);
            return;
        }
        popup.Show(def, gameController);
    }

    private void OnPurchased(BuildingType type)
    {
        if (type != buildingType) return;
        if (builtBuilding != null) builtBuilding.SetActive(true);
        gameObject.SetActive(false);
    }
}