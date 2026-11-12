using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BuildingUnlockVisuals : MonoBehaviour
{
    [Serializable]
    private sealed class Plot
    {
        public GameObject lockedVisual;
        public BuildingTapTarget unlockedBuilding;
    }

    [SerializeField] private GameController gameController;
    [SerializeField] private Plot[] plots;

    private void OnEnable()
    {
        gameController.PhaseChanged += RefreshPlots;
        RefreshPlots(gameController.Phase);
    }

    private void OnDisable() => gameController.PhaseChanged -= RefreshPlots;

    private void RefreshPlots(ResortPhase phase)
    {
        if (!gameController.IsInitialized) return;

        foreach (Plot plot in plots)
        {
            BuildingData building = gameController.Data.GetBuilding(plot.unlockedBuilding.Type);
            bool showBuilding = building.owned || building.availableToPurchase;
            plot.lockedVisual.SetActive(!showBuilding);
            plot.unlockedBuilding.gameObject.SetActive(showBuilding);
        }
    }
}
