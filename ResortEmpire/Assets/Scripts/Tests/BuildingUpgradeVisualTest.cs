using UnityEngine;

public sealed class BuildingUpgradeVisualTest : MonoBehaviour
{
    [SerializeField] private GameController gameController;

    [SerializeField] private BuildingType buildingType = BuildingType.Reception;

    [ContextMenu("SCRUM-195/Upgrade Building")]
    public void UpgradeBuilding()
    {
        if (!Application.isPlaying || gameController == null)
        {
            Debug.LogWarning("Start Play Mode and assign GameController.");
            return;
        }

        if (gameController.TryUpgradeBuilding(buildingType, out string error))
        {
            BuildingData building = gameController.Data.GetBuilding(buildingType);

            Debug.Log(
                $"[SCRUM-195] {buildingType} upgraded to Level {building.level}.");
        }
        else
        {
            Debug.LogWarning($"[SCRUM-195] Upgrade failed: {error}");
        }
    }
}