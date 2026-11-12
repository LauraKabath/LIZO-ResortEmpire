using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WorkerAllocationPanelUI : MonoBehaviour
{
    [Header("Existing scene components")]
    [SerializeField] private GameController gameController;
    [SerializeField] private BuildingDetailsUI buildingDetailsUI;

    [Header("Worker allocation panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text buildingLabel;
    [SerializeField] private TMP_Text assignmentLabel;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private TMP_Dropdown workerDropdown;
    [SerializeField] private Button assignButton;
    [SerializeField] private Button unassignButton;
    [SerializeField] private Button closeButton;

    private readonly List<int> displayedWorkerIds = new List<int>();
    private WorkerRoster roster;
    private BuildingType selectedBuilding;
    private bool hasSelection;
    public void Open(BuildingType type) => OnBuildingSelected(type);

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (workerDropdown != null) workerDropdown.onValueChanged.AddListener(OnWorkerChanged);
        if (assignButton != null) assignButton.onClick.AddListener(AssignSelected);
        if (unassignButton != null) unassignButton.onClick.AddListener(UnassignSelected);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        if (buildingDetailsUI != null)
            buildingDetailsUI.BuildingSelected += OnBuildingSelected;
        ConnectRoster();
    }

    private void Update()
    {
        // GameController creates the roster in Start(), which may run after OnEnable().
        if (roster == null) ConnectRoster();
    }

    private void OnDisable()
    {
        if (buildingDetailsUI != null)
            buildingDetailsUI.BuildingSelected -= OnBuildingSelected;
        if (roster != null) roster.Changed -= Refresh;
        roster = null;
    }

    private void OnDestroy()
    {
        if (workerDropdown != null) workerDropdown.onValueChanged.RemoveListener(OnWorkerChanged);
        if (assignButton != null) assignButton.onClick.RemoveListener(AssignSelected);
        if (unassignButton != null) unassignButton.onClick.RemoveListener(UnassignSelected);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    private void ConnectRoster()
    {
        if (gameController == null || !gameController.IsInitialized || gameController.Workers == null)
            return;
        if (roster == gameController.Workers) return;
        if (roster != null) roster.Changed -= Refresh;
        roster = gameController.Workers;
        roster.Changed += Refresh;
        if (hasSelection) Refresh();
    }

    private void OnBuildingSelected(BuildingType type)
    {
        if (gameController == null || !gameController.IsInitialized || panelRoot == null)
            return;
        BuildingData building = gameController.Data.GetBuilding(type);
        if (building == null || !building.owned)
        {
            Close();
            return;
        }
        selectedBuilding = type;
        hasSelection = true;
        panelRoot.SetActive(true);
        if (feedbackLabel != null) feedbackLabel.text = string.Empty;
        ConnectRoster();
        Refresh();
    }

    private int SelectedWorkerId =>
        workerDropdown != null && workerDropdown.value >= 0 &&
        workerDropdown.value < displayedWorkerIds.Count
            ? displayedWorkerIds[workerDropdown.value] : -1;

    private void Refresh()
    {
        if (!hasSelection || roster == null || panelRoot == null || !panelRoot.activeSelf)
            return;

        if (buildingLabel != null)
            buildingLabel.text = gameController.GetBuildingDefinition(selectedBuilding).DisplayName;

        BuildingData building = gameController.Data.GetBuilding(selectedBuilding);
        if (building == null || !building.owned)
        {
            Close();
            return;
        }

        if (assignmentLabel != null)
            assignmentLabel.text = building.assignedWorkerId == -1
                ? "Assigned: none"
                : $"Assigned: Worker #{building.assignedWorkerId}";

        int previousWorkerId = SelectedWorkerId;
        displayedWorkerIds.Clear();
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (WorkerData worker in roster.Workers)
        {
            displayedWorkerIds.Add(worker.id);
            string assignment = worker.isAssigned
                ? $" ({worker.assignedBuilding})" : " (available)";
            options.Add(new TMP_Dropdown.OptionData($"Worker #{worker.id}{assignment}"));
        }
        if (workerDropdown != null)
        {
            workerDropdown.ClearOptions();
            workerDropdown.AddOptions(options);
            int index = displayedWorkerIds.IndexOf(previousWorkerId);
            workerDropdown.SetValueWithoutNotify(index >= 0 ? index : 0);
            workerDropdown.RefreshShownValue();
            workerDropdown.interactable = displayedWorkerIds.Count > 0;
        }
        UpdateButtons();
    }

    private void OnWorkerChanged(int index) => UpdateButtons();

    private void UpdateButtons()
    {
        int workerId = SelectedWorkerId;
        WorkerData worker = workerId < 0 ? null : gameController.Data.GetWorker(workerId);
        BuildingData building = hasSelection ? gameController.Data.GetBuilding(selectedBuilding) : null;
        bool valid = roster != null && worker != null && building != null && building.owned;

        if (assignButton != null)
            assignButton.interactable = valid &&
                (building.assignedWorkerId == -1 || building.assignedWorkerId == workerId) &&
                (!worker.isAssigned || worker.assignedBuilding != selectedBuilding);
        if (unassignButton != null)
            unassignButton.interactable = valid && worker.isAssigned &&
                worker.assignedBuilding == selectedBuilding;
    }

    public void AssignSelected()
    {
        if (roster == null || !hasSelection || SelectedWorkerId < 0) return;
        bool success = roster.TryAssign(SelectedWorkerId, selectedBuilding, out string error);
        if (feedbackLabel != null)
            feedbackLabel.text = success ? "Worker assigned." : error;
        if (!success) Refresh();
    }

    public void UnassignSelected()
    {
        if (roster == null || !hasSelection || SelectedWorkerId < 0) return;
        WorkerData worker = gameController.Data.GetWorker(SelectedWorkerId);
        if (worker == null || !worker.isAssigned || worker.assignedBuilding != selectedBuilding)
            return;
        bool success = roster.TryUnassign(worker.id, out string error);
        if (feedbackLabel != null)
            feedbackLabel.text = success ? "Worker unassigned." : error;
        if (!success) Refresh();
    }

    public void Close()
    {
        hasSelection = false;
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
