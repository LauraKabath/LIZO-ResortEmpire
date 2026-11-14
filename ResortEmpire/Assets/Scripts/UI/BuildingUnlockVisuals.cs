using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BuildingUnlockVisuals : MonoBehaviour
{
    [Serializable]
    private sealed class Plot
    {
        public BuildingType type;
        [Tooltip("Zamknutá oblasť, kým sa budova nedá kúpiť.")]
        public GameObject lockedVisual;
        [Tooltip("Stavenisko (pool0_0 / bungalow0_0), po kliknutí otvorí kúpu.")]
        public GameObject constructionSite;
        [Tooltip("Hotová budova (PoolLv1_0 / BungalowsLv1_0), po kúpe.")]
        public GameObject builtBuilding;
    }

    [SerializeField] private GameController gameController;
    [SerializeField] private BuildingPurchasePopup purchasePopup;
    [SerializeField] private Plot[] plots;

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
    private void OnPurchased(BuildingType type) => StartCoroutine(RefreshNextFrame());

private System.Collections.IEnumerator RefreshNextFrame()
{
    yield return null;   // počkaj, kým GameController zapíše owned = true
    Refresh();
}

    private void Refresh()
    {
        if (!gameController.IsInitialized) return;

        foreach (Plot plot in plots)
        {
            BuildingData building = gameController.Data.GetBuilding(plot.type);
            if (building == null) continue;

            bool owned = building.owned;
            bool site = !owned && building.availableToPurchase;
            bool locked = !owned && !site;

            if (plot.lockedVisual != null) plot.lockedVisual.SetActive(locked);
            if (plot.constructionSite != null) plot.constructionSite.SetActive(site);
            if (plot.builtBuilding != null) plot.builtBuilding.SetActive(owned);
        }
    }
}