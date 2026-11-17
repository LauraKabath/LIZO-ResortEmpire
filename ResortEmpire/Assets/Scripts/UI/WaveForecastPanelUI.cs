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

    private GameController game;
    private bool isExpanded = true;

    private void Awake()
    {
        game = FindFirstObjectByType<GameController>();
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

        foreach (Row row in rows)
        {
            if (row.root == null)
                continue;

            bool visible = !row.requiresUnlock ||
                           IsNeedUnlocked(row.need);

            row.root.SetActive(visible);
        }
    }
}
