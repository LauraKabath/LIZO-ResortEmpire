
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Displays upgrade information and allows upgrading an owned building.
// The actual upgrade logic is handled by GameController.
[DisallowMultipleComponent]
public sealed class BuildingUpgradePanelUI : MonoBehaviour
{
    
    [Serializable]
    private class BuildingVisual
    {
        public BuildingType type;
        public BuildingLevelVisuals levelVisuals;
    }


    [Header("Scene references")]
    [SerializeField] private GameController gameController;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("Header")]
    [SerializeField] private TMP_Text buildingNameText;
    [SerializeField] private TMP_Text currentLevelText;

    [Header("Building information")]
    [SerializeField] private Image buildingImage;
    [SerializeField] private TMP_Text nextLevelText;
    [SerializeField] private TMP_Text capacityText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private TMP_Text resortValueGainText;

    [Header("Buttons")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button closeButton;

    [Header("Building visuals")]
    [SerializeField] private BuildingVisual[] buildingVisuals;

    [Header("Settings")]
    [SerializeField, Min(1)] private int maxLevel = 3;

    private BuildingType selectedBuilding;
    private bool hasSelection;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(Upgrade);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        if (gameController != null)
            gameController.PhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        if (gameController != null)
            gameController.PhaseChanged -= HandlePhaseChanged;
    }

    private void OnDestroy()
    {
        if (upgradeButton != null)
            upgradeButton.onClick.RemoveListener(Upgrade);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    private void HandlePhaseChanged(ResortPhase phase)
    {
        if (gameController == null ||
            !gameController.IsInitialized)
            return;

        if (!gameController.CanUpgradeBuildings)
        {
            Close();
            return;
        }

        if (hasSelection)
            Refresh();
    }

    // Called when the player selects UPGRADE in Building Popup.
    public void Open(BuildingType type)
    {
        if (gameController == null ||
            !gameController.IsInitialized ||
            !gameController.CanUpgradeBuildings)
            return;

        BuildingData building =
            gameController.Data.GetBuilding(type);

        if (building == null || !building.owned)
            return;

        selectedBuilding = type;
        hasSelection = true;

        Refresh();

        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void Close()
    {
        hasSelection = false;

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void Refresh()
    {
        if (!hasSelection ||
            gameController == null ||
            !gameController.IsInitialized)
            return;

        BuildingData building =
            gameController.Data.GetBuilding(selectedBuilding);

        if (building == null || !building.owned)
        {
            Close();
            return;
        }

        BuildingDefinition definition =
            gameController.GetBuildingDefinition(selectedBuilding);

        if (definition == null)
        {
            Close();
            return;
        }

        int currentLevel = building.level;
        int currentCapacity =
            definition.GetCapacity(currentLevel);

        bool isMaxLevel = currentLevel >= maxLevel;

        // Header
        if (buildingNameText != null)
            buildingNameText.text = definition.DisplayName;

        if (currentLevelText != null)
            currentLevelText.text = $"(Level {currentLevel})";

        // Display the current building sprite.
        RefreshBuildingImage(isMaxLevel ? currentLevel : currentLevel + 1);

        if (isMaxLevel)
        {
            if (nextLevelText != null)
                nextLevelText.text = "MAX LEVEL";

            if (capacityText != null)
                capacityText.text =
                    $"Capacity: {currentCapacity}";

            if (costText != null)
                costText.text = "Fully upgraded";

            if (upgradeButton != null)
                upgradeButton.interactable = false;
            
            if (resortValueGainText != null)
                resortValueGainText.gameObject.SetActive(false);

            return;
        }

        int nextLevel = currentLevel + 1;
        int nextCapacity = definition.GetCapacity(nextLevel);
        int upgradeCost = definition.GetCostCoins(nextLevel);
        int resortValueGain =
            gameController.GetResortValueGainForInvestment(upgradeCost);

        if (resortValueGainText != null)
        {
            resortValueGainText.text =
                $"+{resortValueGain:N0} Resort Value";

            resortValueGainText.gameObject.SetActive(true);
        }


        // Upgrade details
        if (nextLevelText != null)
            nextLevelText.text =
                $"Upgrade to Level {nextLevel}";

        if (capacityText != null)
            capacityText.text =
                $"Capacity: {currentCapacity} \u2192 {nextCapacity}";

        if (costText != null)
            costText.text =
                $"{upgradeCost:N0} Coins";

        // The actual validation is also performed
        // by GameController.TryUpgradeBuilding().
        bool canAfford =
            gameController.Data.coins >= upgradeCost;

        if (upgradeButton != null)
        {
            upgradeButton.interactable =
                gameController.CanUpgradeBuildings &&
                canAfford;
        }
    }

    
    private void RefreshBuildingImage(int previewLevel)
    {
        if (buildingImage == null)
            return;

        if (buildingVisuals == null)
            return;

        foreach (BuildingVisual visual in buildingVisuals)
        {
            if (visual == null ||
                visual.type != selectedBuilding ||
                visual.levelVisuals == null)
                continue;

            Sprite sprite =
                visual.levelVisuals.GetSpriteForLevel(previewLevel);

            buildingImage.sprite = sprite;
            buildingImage.enabled = sprite != null;
            buildingImage.preserveAspect = true;

            if (sprite == null)
            {
                Debug.LogWarning(
                    $"Missing preview sprite for {selectedBuilding} " +
                    $"Level {previewLevel}.",
                    this);
            }

            return;
        }

        buildingImage.sprite = null;
        buildingImage.enabled = false;

        Debug.LogWarning(
            $"Missing BuildingLevelVisuals reference for {selectedBuilding}.",
            this);
    }


    public void Upgrade()
    {
        if (!hasSelection ||
            gameController == null ||
            !gameController.IsInitialized)
            return;

        if (gameController.TryUpgradeBuilding(
                selectedBuilding, out string error))
        {
            Debug.Log(
                $"[SCRUM-196] {selectedBuilding} upgraded.",
                this);

            // Refresh level, capacity, cost and image.
            Refresh();
        }
        else
        {
            Debug.LogWarning(
                $"[SCRUM-196] Upgrade failed: {error}",
                this);

            Refresh();
        }
    }
}
