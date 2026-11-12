using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WaveNeedsPanel : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public NeedType need;
        public string label;
        public GameObject root;
        public TMP_Text valueText;
        public Button button; // tlačidlo Manage otvorí prideľovanie Workerov
    }

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Row[] rows;

    [Header("Service load colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color bottleneckColor = Color.red;
    [SerializeField] private Color recoveringColor = new Color(1f, 0.7f, 0.2f);

    [Header("Worker allocation")]
    [SerializeField] private WorkerAllocationPanelUI workerAllocationPanel;

    [Header("Alert")]
    [SerializeField] private GameObject alertRoot;
    [SerializeField] private TMP_Text alertText;
    [SerializeField] private float alertSeconds = 4f;

    private GameController game;
    private ServiceLoadMonitor subscribedMonitor;
    private WorkerRoster subscribedRoster;
    private Coroutine alertRoutine;

    private void Awake()
    {
        game = FindFirstObjectByType<GameController>();

        foreach (Row row in rows)
        {
            if (row.button == null) continue;
            NeedType need = row.need;
            row.button.onClick.AddListener(() => OnRowClicked(need));
        }

        if (alertRoot != null) alertRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (game == null) game = FindFirstObjectByType<GameController>();
        if (game != null) game.PhaseChanged += HandlePhaseChanged;
    }

    private void OnDisable()
    {
        if (game != null) game.PhaseChanged -= HandlePhaseChanged;
        Unsubscribe();
        UnsubscribeRoster();
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
        else HideAlert();
    }

    private void Update()
    {
        if (panelRoot.activeSelf) Refresh();
    }

    // Monitor vzniká s každou vlnou, preto sa naň prihlasujeme znova, keď sa zmení.
    private void EnsureSubscribed(ServiceLoadMonitor monitor)
    {
        if (monitor == subscribedMonitor) return;
        Unsubscribe();
        subscribedMonitor = monitor;
        if (subscribedMonitor != null) subscribedMonitor.StateChanged += HandleLoadChanged;
    }

    private void Unsubscribe()
    {
        if (subscribedMonitor != null) subscribedMonitor.StateChanged -= HandleLoadChanged;
        subscribedMonitor = null;
    }

    // Po zmene workerov hneď prepočítame stav, aj keď je vlna pozastavená.
    private void EnsureRosterSubscribed()
    {
        WorkerRoster roster = game != null ? game.Workers : null;
        if (roster == subscribedRoster) return;
        UnsubscribeRoster();
        subscribedRoster = roster;
        if (subscribedRoster != null) subscribedRoster.Changed += HandleWorkersChanged;
    }

    private void UnsubscribeRoster()
    {
        if (subscribedRoster != null) subscribedRoster.Changed -= HandleWorkersChanged;
        subscribedRoster = null;
    }

    private void HandleWorkersChanged()
    {
        game?.ServiceLoad?.Update();
        if (panelRoot.activeSelf) Refresh();
    }

    private void HandleLoadChanged(ServiceLoad load)
    {
        if (load.State != ServiceLoadState.BOTTLENECK) return;
        ShowAlert(GetLabel(load.Need), load.Cause);
    }

    private void ShowAlert(string label, BottleneckCause cause)
    {
        if (alertRoot == null || alertText == null) return;

        alertText.text = cause switch
        {
            BottleneckCause.MISSING_WORKER =>
                $"{label}: no Worker assigned. Assign a Worker to the building's slot.",
            BottleneckCause.INSUFFICIENT_CAPACITY =>
                $"{label}: building capacity is too low even with a Worker. Upgrade the building.",
            _ => $"{label}: guests are waiting too long."
        };
        alertRoot.SetActive(true);

        if (alertRoutine != null) StopCoroutine(alertRoutine);
        alertRoutine = StartCoroutine(HideAlertAfterDelay());
    }

    private IEnumerator HideAlertAfterDelay()
    {
        yield return new WaitForSeconds(alertSeconds);
        HideAlert();
    }

    private void HideAlert()
    {
        if (alertRoutine != null)
        {
            StopCoroutine(alertRoutine);
            alertRoutine = null;
        }
        if (alertRoot != null) alertRoot.SetActive(false);
    }

    private void OnRowClicked(NeedType need)
    {
        ServiceLoadMonitor monitor = game != null ? game.ServiceLoad : null;
        ServiceLoad load = monitor?.Get(need);
        if (load == null || load.State != ServiceLoadState.BOTTLENECK) return;

        if (workerAllocationPanel != null)
            workerAllocationPanel.Open(load.Building);
    }

    private string GetLabel(NeedType need)
    {
        foreach (Row row in rows)
            if (row.need == need) return row.label;
        return need.ToString();
    }

    private void Refresh()
    {
        if (game == null) return;
        WaveData wave = game.ActiveWave;
        if (wave == null) return;

        ServiceLoadMonitor monitor = game.ServiceLoad;
        EnsureSubscribed(monitor);
        EnsureRosterSubscribed();

        foreach (Row row in rows)
            row.root.SetActive(false);

        foreach (WaveNeedData need in wave.needs)
        {
            foreach (Row row in rows)
            {
                if (row.need != need.need) continue;
                row.root.SetActive(true);

                string text = $"{row.label} {need.filled}/{need.demandTotal}";
                Color color = normalColor;
                bool showManage = false;

                ServiceLoad load = monitor?.Get(need.need);
                if (load != null)
                {
                    switch (load.State)
                    {
                        case ServiceLoadState.BOTTLENECK:
                            color = bottleneckColor;
                            showManage = true;        // len tlačidlo Manage
                            break;
                        case ServiceLoadState.RECOVERING:
                            color = recoveringColor;
                            text += " (recovering)";  // tlačidlo zmizne
                            break;
                    }
                }

                row.valueText.text = text;
                row.valueText.color = color;
                if (row.button != null)
                    row.button.gameObject.SetActive(showManage);
            }
        }
    }
}