using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UpgradeSuggestionPanelUI : MonoBehaviour
{
    [Serializable]
    private class BuildingVisual
    {
        public BuildingType type;
        public BuildingLevelVisuals levelVisuals;
    }

    [Header("Game")] [SerializeField] private GameController gameController;
    [Header("Related UI")] [SerializeField] private WaveForecastPanelUI waveForecastPanel;
    
    [Header("Other decision panels")]
    [SerializeField] private BuildingUnlockNotificationUI unlockNotification;
    [SerializeField] private BuildingPurchasePopup buildingPurchasePopup;

    [Header("Panel")] [SerializeField] private GameObject panelRoot;

    [Header("Header")] [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text recommendationText;

    [Header("Building information")] [SerializeField]
    private Image buildingImage;

    [SerializeField] private TMP_Text buildingNameText;
    [SerializeField] private TMP_Text capacityText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text missingCoinsText;
    [SerializeField] private TMP_Text resortValueGainText;

    [Header("Buttons")] [SerializeField] private Button laterButton;
    [SerializeField] private Button upgradeNowButton;

    [Header("Building visuals")] [SerializeField]
    private BuildingVisual[] buildingVisuals;

    private UpgradeSuggestion currentSuggestion;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (laterButton != null)
            laterButton.onClick.AddListener(Close);

        if (upgradeNowButton != null)
            upgradeNowButton.onClick.AddListener(UpgradeNow);
    }

    
    private void OnEnable()
    {
        if (gameController != null)
            gameController.UpgradeSuggestionReady += Show;

        if (unlockNotification != null)
            unlockNotification.Closed += TryPresentPending;

        if (buildingPurchasePopup != null)
            buildingPurchasePopup.Closed += TryPresentPending;
    }

    private void OnDisable()
    {
        if (gameController != null)
            gameController.UpgradeSuggestionReady -= Show;

        if (unlockNotification != null)
            unlockNotification.Closed -= TryPresentPending;

        if (buildingPurchasePopup != null)
            buildingPurchasePopup.Closed -= TryPresentPending;

        Close();
    }


    private void OnDestroy()
    {
        if (laterButton != null)
            laterButton.onClick.RemoveListener(Close);

        if (upgradeNowButton != null)
            upgradeNowButton.onClick.RemoveListener(UpgradeNow);
    }

    
    public void Show(UpgradeSuggestion suggestion)
    {
        if (suggestion == null ||
            gameController == null ||
            !gameController.IsInitialized ||
            !gameController.CanUpgradeBuildings)
            return;

        // Keep the recommendation until other decision panels close.
        currentSuggestion = suggestion;

        gameController.SetUpgradeSuggestionOpen(true);

        TryPresentPending();
    }
    
    
    private void TryPresentPending()
    {
        if (currentSuggestion == null ||
            gameController == null)
            return;

        // Wait while the unlock notification is visible.
        if (unlockNotification != null &&
            unlockNotification.IsOpen)
            return;

        // Wait while the purchase popup is visible.
        if (buildingPurchasePopup != null &&
            buildingPurchasePopup.IsOpen)
            return;

        // Do not show an outdated recommendation after the game leaves the preparation phase.
        if (!gameController.CanUpgradeBuildings)
        {
            Close();
            return;
        }

        // Refresh using the latest Coins and building level.
        if (!Refresh())
        {
            Close();
            return;
        }

        if (waveForecastPanel != null)
            waveForecastPanel.SetExpanded(false);

        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    
    private bool Refresh()
    {
        if (currentSuggestion == null || gameController == null)
            return false;

        BuildingType type = currentSuggestion.Building;

        BuildingData building = gameController.Data.GetBuilding(type);

        if (building == null || !building.owned)
            return false;

        BuildingDefinition definition =
            gameController.GetBuildingDefinition(type);

        if (definition == null ||
            building.level >= definition.MaxLevel)
            return false;

        int currentLevel = building.level;
        int nextLevel = currentLevel + 1;

        int currentCapacity = definition.GetCapacity(currentLevel);

        int nextCapacity = definition.GetCapacity(nextLevel);

        int cost = definition.GetCostCoins(nextLevel);

        int resortValueGain =
            gameController.GetResortValueGainForInvestment(cost);

        // Header
        if (titleText != null)
            titleText.text = "UPGRADE SUGGESTION!";

        if (recommendationText != null)
        {
            recommendationText.text = "Increase capacity to reduce guest queues.";
        }

        // Building information
        if (buildingNameText != null)
            buildingNameText.text = definition.DisplayName;

        if (capacityText != null)
        {
            capacityText.text =
                $"Capacity: {currentCapacity} \u2192 {nextCapacity}";
        }

        if (costText != null)
            costText.text = $"{cost:N0} Coins";
        
        if (missingCoinsText != null)
        {
            int availableCoins = gameController.Data.coins;
            int missingCoins = Mathf.Max(0, cost - availableCoins);

            bool hasInsufficientCoins = missingCoins > 0;

            missingCoinsText.gameObject.SetActive(hasInsufficientCoins);

            if (hasInsufficientCoins)
            {
                missingCoinsText.text =
                    $"Missing: {missingCoins:N0} Coins";
            }
        }


        if (resortValueGainText != null)
        {
            resortValueGainText.text =
                $"+{resortValueGain:N0} Resort Value";
        }

        RefreshBuildingImage(type, nextLevel);

        // Reuse the same validation as manual building upgrades.
        string blockReason =
            gameController.GetBuildingUpgradeBlockReason(type);

        if (upgradeNowButton != null)
            upgradeNowButton.interactable = blockReason == null;

        return true;
    }

    private void RefreshBuildingImage(
        BuildingType type,
        int previewLevel)
    {
        if (buildingImage == null)
            return;

        if (buildingVisuals != null)
        {
            foreach (BuildingVisual visual in buildingVisuals)
            {
                if (visual == null ||
                    visual.type != type ||
                    visual.levelVisuals == null)
                    continue;

                Sprite sprite =
                    visual.levelVisuals.GetSpriteForLevel(
                        previewLevel);

                buildingImage.sprite = sprite;
                buildingImage.enabled = sprite != null;
                buildingImage.preserveAspect = true;

                return;
            }
        }

        buildingImage.sprite = null;
        buildingImage.enabled = false;

        Debug.LogWarning(
            $"[SCRUM-200] Missing preview sprite for {type}.",
            this);
    }

    public void UpgradeNow()
    {
        if (currentSuggestion == null ||
            gameController == null)
            return;

        BuildingType type = currentSuggestion.Building;

        if (gameController.TryUpgradeBuilding(
                type, out string error))
        {
            Debug.Log(
                $"[SCRUM-200] Accepted upgrade suggestion: {type}.",
                this);

            Close();
        }
        else
        {
            Debug.LogWarning(
                $"[SCRUM-200] Upgrade failed: {error}",
                this);

            // Refresh button availability if game state changed.
            if (!Refresh())
                Close();
        }
    }

    public void Close()
    {
        currentSuggestion = null;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (gameController != null &&
            gameController.IsInitialized)
        {
            gameController.SetUpgradeSuggestionOpen(false);
        }
    }
}