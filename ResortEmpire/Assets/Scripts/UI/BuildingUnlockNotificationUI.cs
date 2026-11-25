
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Displays a notification when a new service building becomes available.
[DisallowMultipleComponent]
public sealed class BuildingUnlockNotificationUI : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private GameController gameController;
    [SerializeField] private BuildingPurchasePopup buildingPurchasePopup;

    [Header("Notification UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Image buildingImage;

    [Header("Level 1 building sprites")]
    [SerializeField] private Sprite poolLevel1Sprite;
    [SerializeField] private Sprite bungalowsLevel1Sprite;

    [Header("Buttons")]
    [SerializeField] private Button viewBuildingButton;
    [SerializeField] private Button closeButton;

    // Prevents duplicate notifications in the same game session.
    private readonly HashSet<int> shownAfterWaves = new HashSet<int>();

    private BuildingType? unlockedBuilding;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (viewBuildingButton != null)
            viewBuildingButton.onClick.AddListener(ViewBuilding);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        if (gameController != null)
            gameController.PhaseChanged += HandlePhaseChanged;
    }

    private void Start()
    {
        // Handles initialization order.
        if (gameController != null && gameController.IsInitialized)
            HandlePhaseChanged(gameController.Phase);
    }

    private void OnDisable()
    {
        if (gameController != null)
            gameController.PhaseChanged -= HandlePhaseChanged;
    }

    private void OnDestroy()
    {
        if (viewBuildingButton != null)
            viewBuildingButton.onClick.RemoveListener(ViewBuilding);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(Close);
    }

    private void HandlePhaseChanged(ResortPhase phase)
    {
        if (panelRoot == null ||
            gameController == null ||
            !gameController.IsInitialized)
            return;

        // Display notifications after claiming rewards,
        // when the next wave is ready.
        if (phase != ResortPhase.AWAITING_WAVE)
        {
            panelRoot.SetActive(false);
            unlockedBuilding = null;
            return;
        }

        int completedWave = gameController.Data.nextWaveNumber - 1;

        if (shownAfterWaves.Contains(completedWave))
            return;

        BuildingType building;

        if (completedWave == 3)
            building = BuildingType.Pool;
        else if (completedWave == 6)
            building = BuildingType.Bungalows;
        else
            return;

        // Check whether the building is available and not owned.
        bool available = false;

        foreach (BuildingData data in gameController.Data.buildings)
        {
            if (data.type != building)
                continue;

            available = data.availableToPurchase && !data.owned;
            break;
        }

        if (!available)
            return;

        BuildingDefinition definition =
            gameController.GetBuildingDefinition(building);

        if (definition == null)
        {
            Debug.LogWarning(
                $"Missing building definition for {building}.",
                this);
            return;
        }

        unlockedBuilding = building;
        shownAfterWaves.Add(completedWave);

        UpdateNotification(definition, building);

        panelRoot.SetActive(true);

        Debug.Log(
            $"[SCRUM-231] {definition.DisplayName} unlocked after Wave {completedWave}.",
            this);
    }

    private void UpdateNotification(
        BuildingDefinition definition,
        BuildingType building)
    {
        if (titleText != null)
            titleText.text = $"{definition.DisplayName} Unlocked!";

        if (descriptionText != null)
            descriptionText.text = "Now available to purchase.";

        if (priceText != null)
            priceText.text = $"{definition.GetCostCoins(1):N0} Coins";

        if (buildingImage != null)
        {
            Sprite sprite = building == BuildingType.Pool
                ? poolLevel1Sprite
                : bungalowsLevel1Sprite;

            buildingImage.sprite = sprite;
            buildingImage.enabled = sprite != null;

            if (sprite == null)
                Debug.LogWarning(
                    $"Missing Level 1 sprite for {building}.",
                    this);
        }
    }

    public void ViewBuilding()
    {
        if (gameController == null ||
            buildingPurchasePopup == null ||
            !unlockedBuilding.HasValue)
        {
            Debug.LogWarning(
                "Cannot open the building purchase popup. Check references.",
                this);
            return;
        }

        BuildingDefinition definition =
            gameController.GetBuildingDefinition(unlockedBuilding.Value);

        if (definition == null)
        {
            Debug.LogWarning(
                "Building definition was not found.",
                this);
            return;
        }

        // Hide the notification before opening the purchase popup.
        Close();

        buildingPurchasePopup.Show(definition, gameController);
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
