using UnityEngine;

// Temporary manual smoke-test component. Do not include it in the production PR.
public sealed class EnergyBoostSmokeTest : MonoBehaviour
{
    [SerializeField] private GameController gameController;
    [SerializeField] private BuildingType building = BuildingType.Reception;

    [ContextMenu("SCRUM-205 / Activate boost")]
    private void ActivateBoost()
    {
        if (gameController == null)
        {
            Debug.LogWarning("Assign GameController before testing.", this);
            return;
        }

        bool success = gameController.TryActivateEnergyBoost(building, out string error);
        Debug.Log($"[SCRUM-205 Test] Activate {building}: success={success}, " +
                  $"error={error ?? "none"}, Energy={gameController.Data.energy}, " +
                  $"used={gameController.HasUsedEnergyBoost(building)}, " +
                  $"active={gameController.IsEnergyBoostActive(building)}, " +
                  $"remaining={gameController.GetEnergyBoostRemainingSeconds(building):F1}s", this);
    }

    [ContextMenu("SCRUM-205 / Inspect boost")]
    private void InspectBoost()
    {
        if (gameController == null || gameController.Data == null)
        {
            Debug.LogWarning("Start Play mode and assign GameController.", this);
            return;
        }

        Debug.Log($"[SCRUM-205 Test] {building}: phase={gameController.Phase}, " +
                  $"Energy={gameController.Data.energy}, " +
                  $"used={gameController.HasUsedEnergyBoost(building)}, " +
                  $"active={gameController.IsEnergyBoostActive(building)}, " +
                  $"remaining={gameController.GetEnergyBoostRemainingSeconds(building):F1}s, " +
                  $"blocked={gameController.GetEnergyBoostBlockReason(building) ?? "no"}", this);
    }
}
