using TMPro;
using UnityEngine;

public class WaveForecastPanelUI : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public NeedType need;
        public GameObject root;
        public TMP_Text valueText;
        public bool requiresUnlock;
    }

    [Header("Panel Visibility")]
    [SerializeField] private GameObject panelRoot;

    [Header("Collapse / Expand")]
    [SerializeField] private GameObject expandedPanel;
    [SerializeField] private GameObject collapsedTab;

    [Header("Forecast Needs")]
    [SerializeField] private Row[] rows;

    [Header("Wave Data (Waves.json)")]
    [Tooltip("Fill the forecast with the next wave's demand from Waves.json. Off keeps the authored placeholder texts.")]
    [SerializeField] private TMP_Text waveNumberText;

    private GameController game;
    private bool isExpanded = true;
    private string[] rowLabels;
    private WorkerRoster subscribedRoster;

    private void Awake()
    {
        game = FindFirstObjectByType<GameController>();
        CaptureRowLabels();
        SetExpanded(true);
    }

    private void OnEnable()
    {
        if (game == null)
            game = FindFirstObjectByType<GameController>();

        if (game != null)
            game.PhaseChanged += HandlePhaseChanged;
    }

    private void Start()
    {
        HandlePhaseChanged(
            game != null
                ? game.Phase
                : ResortPhase.AWAITING_WAVE
        );
    }

    private void OnDisable()
    {
        if (game != null)
            game.PhaseChanged -= HandlePhaseChanged;

        UnsubscribeRoster();
    }

    private void HandlePhaseChanged(ResortPhase phase)
    {
        bool visible = phase == ResortPhase.AWAITING_WAVE;

        panelRoot.SetActive(visible);

        if (visible)
        {
            SetExpanded(true);
            RefreshNeeds();
        }
    }

    public void SetExpanded(bool expanded)
    {
        isExpanded = expanded;

        expandedPanel.SetActive(isExpanded);
        collapsedTab.SetActive(!isExpanded);
    }

    public void ToggleExpanded()
    {
        SetExpanded(!isExpanded);
    }

    // Checks whether the player owns a building
    // that services the specified need.
    private bool IsNeedUnlocked(NeedType need)
    {
        if (game == null || game.Data == null)
            return false;

        foreach (BuildingData building in game.Data.buildings)
        {
            if (!building.owned)
                continue;

            if (game.GetBuildingDefinition(building.type).ServicedNeed == need)
                return true;
        }

        return false;
    }

    // Updates the visibility of forecast needs
    // based on currently owned buildings.
    public void RefreshNeeds()
    {
        if (game == null || game.Data == null)
            return;

        RefreshWaveData();
        return;
    }

    // Shows the wave that Start Wave will launch next (Data.nextWaveNumber),
    // so the last configured wave is forecast like any other.
    private void RefreshWaveData()
    {
        // GameController raises PhaseChanged once Waves.json is loaded; refresh then.
        if (!game.IsInitialized)
            return;

        EnsureRosterSubscribed();

        WaveDefinition wave = game.GetWaveDefinition(game.Data.nextWaveNumber);
        if (wave == null)
        {
            // No upcoming wave is configured: hide the panel instead of showing empty values.
            panelRoot.SetActive(false);
            return;
        }

        if (waveNumberText != null)
            waveNumberText.text = $"Wave {wave.waveNumber}";

        for (int index = 0; index < rows.Length; index++)
        {
            Row row = rows[index];
            if (row.root == null)
                continue;

            int demand = wave.GetDemand(row.need);

            // Needs of buildings that are not owned yet never spawn, so they are not forecast.
            // Optional needs are also hidden in waves that do not request them.
            bool visible = !row.requiresUnlock ||
                           (IsNeedUnlocked(row.need) && demand > 0);

            row.root.SetActive(visible);

            if (visible && row.valueText != null)
                row.valueText.text = $"{rowLabels[index]} x{demand}";
        }
    }

    // Keeps the authored label of each row ("Rooms" from "Rooms x8"),
    // so only the number is replaced with wave data.
    private void CaptureRowLabels()
    {
        if (rows == null)
            rows = new Row[0];

        rowLabels = new string[rows.Length];
        for (int index = 0; index < rows.Length; index++)
        {
            Row row = rows[index];
            string text = row.valueText != null ? row.valueText.text.Trim() : string.Empty;
            int separator = text.LastIndexOf(" x", System.StringComparison.Ordinal);
            rowLabels[index] = separator > 0 ? text.Substring(0, separator) : row.need.ToString();
        }
    }

    // A building purchase while awaiting the wave adds its need to the forecast.
    private void EnsureRosterSubscribed()
    {
        WorkerRoster roster = game != null ? game.Workers : null;
        if (roster == subscribedRoster)
            return;

        UnsubscribeRoster();
        subscribedRoster = roster;

        if (subscribedRoster != null)
            subscribedRoster.Changed += HandleWorkersChanged;
    }

    private void UnsubscribeRoster()
    {
        if (subscribedRoster != null)
            subscribedRoster.Changed -= HandleWorkersChanged;

        subscribedRoster = null;
    }

    private void HandleWorkersChanged()
    {
        if (panelRoot.activeSelf)
            RefreshNeeds();
    }
}
