using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Worker allocation panel: pick a source (FROM) and a target (TO), press the action button.
// "Free workers" is a regular entry in both dropdowns, so one control covers
// building -> building (MOVE), free -> building (ASSIGN) and building -> free (UNASSIGN).
// The button keeps its own text (ASSIGN) for all three cases.
public sealed class WorkerAllocationPanelUI : MonoBehaviour
{
    [Header("Existing scene components")]
    [SerializeField] private GameController gameController;
    [SerializeField] private BuildingDetailsUI buildingDetailsUI;

    [Header("Worker allocation panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField, FormerlySerializedAs("workerDropdown")] private TMP_Dropdown sourceDropdown;
    [SerializeField] private TMP_Dropdown targetDropdown;
    [SerializeField, FormerlySerializedAs("buildingLabel")] private TMP_Text sourceOccupancyLabel;
    [SerializeField, FormerlySerializedAs("assignmentLabel")] private TMP_Text targetOccupancyLabel;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField, FormerlySerializedAs("assignButton")] private Button moveButton;
    [SerializeField] private Button closeButton;

    [Header("Texts")]
    [SerializeField] private string fromPrefix = "FROM";
    [SerializeField] private string toPrefix = "TO";
    [SerializeField] private string poolName = "Free workers";

    // Either the pool of free workers or one building.
    private readonly struct Endpoint : IEquatable<Endpoint>
    {
        public readonly bool IsPool;
        public readonly BuildingType Building;

        private Endpoint(bool isPool, BuildingType building)
        {
            IsPool = isPool;
            Building = building;
        }

        public static Endpoint Pool => new Endpoint(true, default);
        public static Endpoint OfBuilding(BuildingType type) => new Endpoint(false, type);

        public bool Equals(Endpoint other) =>
            IsPool == other.IsPool && (IsPool || Building == other.Building);
        public override bool Equals(object obj) => obj is Endpoint other && Equals(other);
        public override int GetHashCode() => IsPool ? -1 : (int)Building;
    }

    private readonly List<Endpoint> sourceOptions = new List<Endpoint>();
    private readonly List<Endpoint> targetOptions = new List<Endpoint>();
    private WorkerRoster roster;
    private bool hasSelection;
    private Endpoint? preferredSource;
    private Endpoint? preferredTarget;

    public void Open(BuildingType type) => OnBuildingSelected(type);

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (sourceDropdown != null) sourceDropdown.onValueChanged.AddListener(OnSourceChanged);
        if (targetDropdown != null) targetDropdown.onValueChanged.AddListener(OnTargetChanged);
        if (moveButton != null) moveButton.onClick.AddListener(MoveSelected);
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
        if (sourceDropdown != null) sourceDropdown.onValueChanged.RemoveListener(OnSourceChanged);
        if (targetDropdown != null) targetDropdown.onValueChanged.RemoveListener(OnTargetChanged);
        if (moveButton != null) moveButton.onClick.RemoveListener(MoveSelected);
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

        // Preselect the building the panel was opened from:
        // as the source if it has a worker, otherwise as the target.
        preferredSource = null;
        preferredTarget = null;
        if (building.assignedWorkerId != -1) preferredSource = Endpoint.OfBuilding(type);
        else preferredTarget = Endpoint.OfBuilding(type);

        hasSelection = true;
        panelRoot.SetActive(true);
        if (feedbackLabel != null) feedbackLabel.text = string.Empty;
        ConnectRoster();
        Refresh();
    }

    // ---------- Current selection ----------

    private Endpoint? CurrentSource =>
        sourceDropdown != null && sourceDropdown.value >= 0 && sourceDropdown.value < sourceOptions.Count
            ? sourceOptions[sourceDropdown.value] : (Endpoint?)null;

    private Endpoint? CurrentTarget =>
        targetDropdown != null && targetDropdown.value >= 0 && targetDropdown.value < targetOptions.Count
            ? targetOptions[targetDropdown.value] : (Endpoint?)null;

    // ---------- Texts ----------

    private string NameOf(Endpoint e) =>
        e.IsPool ? poolName : gameController.GetBuildingDefinition(e.Building).DisplayName;

    // Pool: number of free workers. Building: occupied/total slots.
    private string StateOf(Endpoint e) =>
        e.IsPool
            ? $"{roster.GetAvailableWorkers().Count} free"
            : $"{roster.GetOccupiedSlots(e.Building)}/{roster.GetTotalSlots(e.Building)}";

    // ---------- Refresh ----------

    private void Refresh()
    {
        if (!hasSelection || roster == null || panelRoot == null || !panelRoot.activeSelf)
            return;

        Endpoint? wantedSource = preferredSource ?? CurrentSource;
        Endpoint? wantedTarget = preferredTarget ?? CurrentTarget;
        preferredSource = null;
        preferredTarget = null;

        BuildSources();
        FillDropdown(sourceDropdown, sourceOptions, wantedSource);

        BuildTargets(CurrentSource);
        FillDropdown(targetDropdown, targetOptions, wantedTarget);

        RefreshLabels();
    }

    private void BuildSources()
    {
        sourceOptions.Clear();

        if (roster.GetAvailableWorkers().Count > 0)
            sourceOptions.Add(Endpoint.Pool);

        foreach (BuildingType type in Enum.GetValues(typeof(BuildingType)))
        {
            BuildingData building = gameController.Data.GetBuilding(type);
            if (building != null && building.owned && building.assignedWorkerId != -1)
                sourceOptions.Add(Endpoint.OfBuilding(type));
        }
    }

    private void BuildTargets(Endpoint? source)
    {
        targetOptions.Clear();

        // A worker standing in a building can be sent back to the free pool.
        if (source.HasValue && !source.Value.IsPool)
            targetOptions.Add(Endpoint.Pool);

        foreach (BuildingType type in Enum.GetValues(typeof(BuildingType)))
        {
            if (source.HasValue && !source.Value.IsPool && source.Value.Building == type) continue;
            if (roster.GetFreeSlots(type) > 0)
                targetOptions.Add(Endpoint.OfBuilding(type));
        }
    }

    private void FillDropdown(TMP_Dropdown dropdown, List<Endpoint> items, Endpoint? preferred)
    {
        if (dropdown == null) return;

        var options = new List<TMP_Dropdown.OptionData>();
        foreach (Endpoint item in items)
            options.Add(new TMP_Dropdown.OptionData($"{NameOf(item)}  ({StateOf(item)})"));

        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        int index = preferred.HasValue ? items.IndexOf(preferred.Value) : 0;
        dropdown.SetValueWithoutNotify(Mathf.Max(0, index));
        dropdown.RefreshShownValue();
        dropdown.interactable = items.Count > 0;
    }

    private void RefreshLabels()
    {
        Endpoint? source = CurrentSource;
        Endpoint? target = CurrentTarget;

        // The labels are only short captions above their dropdowns;
        // the counts are shown inside the dropdown entries.
        if (sourceOccupancyLabel != null)
            sourceOccupancyLabel.text = source.HasValue ? fromPrefix : $"{fromPrefix} (nothing to move)";
        if (targetOccupancyLabel != null)
            targetOccupancyLabel.text = target.HasValue ? toPrefix : $"{toPrefix} (no free target)";

        if (moveButton != null)
        {
            moveButton.interactable = roster != null && source.HasValue && target.HasValue
                                      && roster.CanChangeWorkers;
        }
    }

    // ---------- Events ----------

    private void OnSourceChanged(int index)
    {
        if (roster == null) return;
        Endpoint? previousTarget = CurrentTarget;
        BuildTargets(CurrentSource);
        FillDropdown(targetDropdown, targetOptions, previousTarget);
        RefreshLabels();
    }

    private void OnTargetChanged(int index) => RefreshLabels();

    // ---------- Move ----------

    public void MoveSelected()
    {
        if (roster == null || !CurrentSource.HasValue || !CurrentTarget.HasValue) return;

        Endpoint source = CurrentSource.Value;
        Endpoint target = CurrentTarget.Value;

        // Which worker is being moved.
        int workerId;
        if (source.IsPool)
        {
            List<WorkerData> free = roster.GetAvailableWorkers();
            if (free.Count == 0)
            {
                SetFeedback("No free worker available.");
                Refresh();
                return;
            }
            workerId = free[0].id;
        }
        else
        {
            workerId = gameController.Data.GetBuilding(source.Building).assignedWorkerId;
        }

        // Names are taken before the move, because the roster change refreshes the UI.
        string sourceName = NameOf(source);
        string targetName = NameOf(target);

        bool success;
        string error;
        if (source.IsPool)
            success = roster.TryAssign(workerId, target.Building, out error);
        else if (target.IsPool)
            success = roster.TryUnassign(workerId, out error);
        else
            success = roster.TryMove(workerId, target.Building, out error);

        // On success the Changed event has already refreshed the dropdowns and labels
        // (the counts there show the new state); the feedback is written afterwards
        // so it is not cleared.
        if (success)
        {
            SetFeedback($"Worker #{workerId}: {sourceName} → {targetName}");
        }
        else
        {
            SetFeedback(error);
            Refresh();
        }
    }

    private void SetFeedback(string text)
    {
        if (feedbackLabel != null) feedbackLabel.text = text;
    }

    public void Close()
    {
        hasSelection = false;
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}