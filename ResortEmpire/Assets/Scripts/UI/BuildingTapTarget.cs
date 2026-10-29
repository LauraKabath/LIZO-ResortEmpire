using UnityEngine;

[DisallowMultipleComponent]
public sealed class BuildingTapTarget : MonoBehaviour, ITapTarget
{
    [SerializeField] private BuildingType buildingType;
    [SerializeField] private BuildingDetailsUI detailsUI;
    [SerializeField] private Renderer buildingRenderer;
    [SerializeField] private Vector3 anchorOffset = new Vector3(0, 0.12f, 0);

    public BuildingType Type => buildingType;
    public Vector3 AnchorPosition
    {
        get
        {
            if (buildingRenderer == null) return transform.position + anchorOffset;
            Bounds bounds = buildingRenderer.bounds;
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + anchorOffset;
        }
    }

    private void Awake()
    {
        if (buildingRenderer == null) buildingRenderer = GetComponent<Renderer>();
    }

    public void OnTapped()
    {
        if (detailsUI != null) detailsUI.ShowBuilding(this);
    }
}
