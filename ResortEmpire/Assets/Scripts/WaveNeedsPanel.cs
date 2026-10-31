using TMPro;
using UnityEngine;

public sealed class WaveNeedsPanel : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public NeedType need;
        public string label;
        public GameObject root;
        public TMP_Text valueText;
    }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Row[] rows;

    private GameController game;

    private void Awake()
    {
        game = FindFirstObjectByType<GameController>();
    }

    private void OnEnable()
    {
        if (game == null) game = FindFirstObjectByType<GameController>();
        if (game != null) game.PhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        if (game != null) game.PhaseChanged -= HandlePhaseChanged;
    }

    private void Start()
    {
        HandlePhaseChanged(game != null ? game.Phase : ResortPhase.AWAITING_WAVE);
    }

    private void HandlePhaseChanged(ResortPhase phase)
    {
        bool visible = phase == ResortPhase.WAVE_ACTIVE || phase == ResortPhase.WAVE_PAUSED;
        panelRoot.SetActive(visible);
        if (visible) Refresh();
    }

    private void Update()
    {
        if (panelRoot.activeSelf) Refresh();
    }

    private void Refresh()
    {
        if (game == null) return;
        WaveData wave = game.ActiveWave;
        if (wave == null) return;

        foreach (Row row in rows)
            row.root.SetActive(false);

        foreach (WaveNeedData need in wave.needs)
        {
            foreach (Row row in rows)
            {
                if (row.need != need.need) continue;
                row.root.SetActive(true);
                row.valueText.text = $"{row.label} {need.filled}/{need.demandTotal}";
            }
        }
    }
}