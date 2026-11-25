
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WaveNeedsPanelCollapseUI : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private GameController gameController;

    [Header("Panel UI")]
    [SerializeField] private CanvasGroup contentGroup;
    [SerializeField] private Button collapseButton;
    [SerializeField] private GameObject expandTab;
    [SerializeField] private Button expandButton;
    [SerializeField] private GameObject warningIcon;
    [SerializeField] private CanvasGroup alertGroup;

    private bool isCollapsed;

    private void Awake()
    {
        if (collapseButton != null)
            collapseButton.onClick.AddListener(Collapse);

        if (expandButton != null)
            expandButton.onClick.AddListener(Expand);

        if (expandTab != null)
            expandTab.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameController == null)
            gameController = FindFirstObjectByType<GameController>();

        if (gameController != null)
            gameController.PhaseChanged += HandlePhaseChanged;
    }

    private void Start()
    {
        HandlePhaseChanged(
            gameController != null
                ? gameController.Phase
                : ResortPhase.AWAITING_WAVE);
    }

    private void OnDisable()
    {
        if (gameController != null)
            gameController.PhaseChanged -= HandlePhaseChanged;
    }

    private void OnDestroy()
    {
        if (collapseButton != null)
            collapseButton.onClick.RemoveListener(Collapse);

        if (expandButton != null)
            expandButton.onClick.RemoveListener(Expand);
    }

    private void HandlePhaseChanged(ResortPhase phase)
    {
        if (!IsWaveVisible(phase))
            isCollapsed = false;

        RefreshVisibility();
    }

    private bool IsWaveVisible(ResortPhase phase)
    {
        return phase == ResortPhase.WAVE_ACTIVE ||
               phase == ResortPhase.WAVE_PAUSED;
    }

    public void Collapse()
    {
        if (gameController == null ||
            !IsWaveVisible(gameController.Phase))
            return;

        isCollapsed = true;
        RefreshVisibility();
    }

    public void Expand()
    {
        isCollapsed = false;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        bool visible = gameController != null &&
                       IsWaveVisible(gameController.Phase);

        if (contentGroup != null)
        {
            bool showContent = visible && !isCollapsed;

            contentGroup.alpha = showContent ? 1f : 0f;
            contentGroup.interactable = showContent;
            contentGroup.blocksRaycasts = showContent;
        }

        if (expandTab != null)
            expandTab.SetActive(visible && isCollapsed);
        
        if (alertGroup != null)
        {
            alertGroup.alpha = isCollapsed ? 0f : 1f;
            alertGroup.interactable = !isCollapsed;
            alertGroup.blocksRaycasts = !isCollapsed;
        }
    }

    private void LateUpdate()
    {
        if (warningIcon == null ||
            gameController == null ||
            !isCollapsed ||
            !IsWaveVisible(gameController.Phase))
        {
            if (warningIcon != null)
                warningIcon.SetActive(false);

            return;
        }

        warningIcon.SetActive(HasBottleneck());
    }

    private bool HasBottleneck()
    {
        ServiceLoadMonitor monitor = gameController.ServiceLoad;

        if (monitor == null)
            return false;

        foreach (NeedType need in Enum.GetValues(typeof(NeedType)))
        {
            ServiceLoad load = monitor.Get(need);

            if (load != null &&
                load.State == ServiceLoadState.BOTTLENECK)
                return true;
        }

        return false;
    }
}
