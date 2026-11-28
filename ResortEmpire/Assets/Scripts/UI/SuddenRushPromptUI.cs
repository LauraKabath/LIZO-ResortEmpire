using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sudden Rush decision, shown to the right of the rushed need's row in the Wave Needs Panel.
// The wave keeps running. The prompt stays until the player answers or the wave ends; its state
// lives in the wave data, so an unanswered prompt comes back after loading a save.
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class SuddenRushPromptUI : MonoBehaviour
{
    [Header("References (found automatically when empty)")]
    [SerializeField] private GameController gameController;
    [SerializeField] private WaveNeedsPanel needsPanel;

    [Header("Prompt")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button moveWorkerButton;
    [SerializeField] private Button useEnergyButton;
    [SerializeField] private TMP_Text useEnergyLabel;
    [SerializeField] private Button ignoreButton;
    [Tooltip("Short reason shown when an option is unavailable. Hidden when empty.")]
    [SerializeField] private TMP_Text infoText;

    [Header("Texts")]
    [SerializeField] private string titleFormat = "Sudden rush at {0}!";
    [SerializeField] private string useEnergyFormat = "Use Energy ({0})";
    [SerializeField] private string pausedInfo = "Wave paused.";

    [Header("Placement")]
    [Tooltip("Space between the right edge of the need's row and the prompt.")]
    [SerializeField] private float gapFromRow = 16f;

    private readonly Vector3[] rowCorners = new Vector3[4];
    private RectTransform rect;
    private CanvasGroup canvasGroup;
    private bool visible = true;
    private string clickError;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (needsPanel == null) needsPanel = GetComponentInParent<WaveNeedsPanel>(true);

        if (moveWorkerButton != null) moveWorkerButton.onClick.AddListener(OnMoveWorkerClicked);
        if (useEnergyButton != null) useEnergyButton.onClick.AddListener(OnUseEnergyClicked);
        if (ignoreButton != null) ignoreButton.onClick.AddListener(OnIgnoreClicked);

        SetVisible(false);
    }

    private void OnEnable()
    {
        if (gameController == null) gameController = FindFirstObjectByType<GameController>();
        if (gameController != null) gameController.SuddenRushStarted += HandleRushStarted;
    }

    private void OnDisable()
    {
        if (gameController != null) gameController.SuddenRushStarted -= HandleRushStarted;
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (moveWorkerButton != null) moveWorkerButton.onClick.RemoveListener(OnMoveWorkerClicked);
        if (useEnergyButton != null) useEnergyButton.onClick.RemoveListener(OnUseEnergyClicked);
        if (ignoreButton != null) ignoreButton.onClick.RemoveListener(OnIgnoreClicked);
    }

    private void HandleRushStarted(SuddenRushState rush) => clickError = null;

    // LateUpdate, so the prompt follows the row after the panel has updated this frame.
    private void LateUpdate()
    {
        SuddenRushState rush = gameController != null ? gameController.SuddenRush : null;
        RectTransform row = null;
        if (gameController != null && gameController.IsSuddenRushPromptOpen && needsPanel != null)
            row = needsPanel.GetVisibleRowRect(rush.need);

        if (row == null)
        {
            clickError = null;
            SetVisible(false);
            return;
        }

        PlaceBesideRow(row);
        Refresh(rush);
        SetVisible(true);
    }

    private void PlaceBesideRow(RectTransform row)
    {
        if (rect.parent is not RectTransform parent) return;

        // Corners: 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right.
        row.GetWorldCorners(rowCorners);
        Vector3 rightMiddle = (rowCorners[2] + rowCorners[3]) * 0.5f;
        Vector3 local = parent.InverseTransformPoint(rightMiddle);

        // The prompt's pivot is its left-middle point.
        var position = new Vector3(local.x + gapFromRow, local.y, 0f);
        if (rect.localPosition != position) rect.localPosition = position;
    }

    private void Refresh(SuddenRushState rush)
    {
        BuildingDefinition definition = gameController.GetBuildingDefinition(rush.building);
        SetText(titleText, string.Format(titleFormat, definition != null ? definition.DisplayName : rush.building.ToString()));
        SetText(useEnergyLabel, string.Format(useEnergyFormat, gameController.EnergyBoostCost));

        bool canAnswer = gameController.CanAnswerSuddenRush;
        string energyBlock = canAnswer ? gameController.GetSuddenRushEnergyBlockReason() : null;

        SetInteractable(moveWorkerButton, canAnswer && gameController.CanManageWorkers);
        SetInteractable(useEnergyButton, canAnswer && energyBlock == null);
        SetInteractable(ignoreButton, canAnswer);

        string info = !canAnswer ? pausedInfo : clickError ?? energyBlock;
        SetText(infoText, info ?? string.Empty);
        if (infoText != null && infoText.gameObject.activeSelf != !string.IsNullOrEmpty(info))
            infoText.gameObject.SetActive(!string.IsNullOrEmpty(info));
    }

    private void OnMoveWorkerClicked()
    {
        clickError = null;
        SuddenRushState rush = gameController != null ? gameController.SuddenRush : null;
        if (rush == null || !gameController.CanAnswerSuddenRush) return;

        // The prompt is answered once a Worker arrives; closing the panel keeps it open.
        if (needsPanel == null || !needsPanel.TryOpenWorkerAllocation(rush.building))
            clickError = "The Worker panel is not available.";
    }

    private void OnUseEnergyClicked()
    {
        clickError = null;
        if (gameController != null && !gameController.TryUseEnergyForSuddenRush(out string error))
            clickError = error;
    }

    private void OnIgnoreClicked()
    {
        clickError = null;
        if (gameController != null && !gameController.TryIgnoreSuddenRush(out string error))
            clickError = error;
    }

    private void SetVisible(bool show)
    {
        if (canvasGroup == null || visible == show) return;
        visible = show;
        canvasGroup.alpha = show ? 1f : 0f;
        canvasGroup.interactable = show;
        canvasGroup.blocksRaycasts = show;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null && text.text != value) text.text = value;
    }

    private static void SetInteractable(Button button, bool interactable)
    {
        if (button != null && button.interactable != interactable) button.interactable = interactable;
    }
}
