using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Shows the current wave number only while the wave is active or paused.
[DisallowMultipleComponent]
public sealed class ActiveWaveIndicatorUI : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private GameController gameController;
    
    [Header("Prefab references")]
    [SerializeField] private GameObject indicatorRoot;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image backgroundImage;

    [Header("Status colors")]
    [SerializeField] private Color activeColor = new Color32(220, 0, 35, 255);
    [SerializeField] private Color pausedColor = new Color32(234, 122, 14, 255);

    private void Awake()
    {
        // Keep this component active; only the visual child should be hidden.
        if (indicatorRoot != null)
            indicatorRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameController != null)
            gameController.PhaseChanged += HandlePhaseChanged;

        Refresh();
    }

    private void Start()
    {
        // GameController initializes in Start, so refresh once more afterwards.
        Refresh();
    }

    private void OnDisable()
    {
        if (gameController != null)
            gameController.PhaseChanged -= HandlePhaseChanged;
    }

    public void Refresh()
    {
        if (gameController == null || !gameController.IsInitialized)
        {
            SetVisible(false);
            return;
        }

        HandlePhaseChanged(gameController.Phase);
    }

    private void HandlePhaseChanged(ResortPhase phase)
    {
        bool isActive = phase == ResortPhase.WAVE_ACTIVE;
        bool isPaused = phase == ResortPhase.WAVE_PAUSED;
        if (!isActive && !isPaused)
        {
            SetVisible(false);
            return;
        }

        if (gameController == null || gameController.Data == null)
        {
            SetVisible(false);
            return;
        }

        int waveNumber = gameController.Data.nextWaveNumber;
        if (statusText != null)
            statusText.text = isActive
                ? $"{waveNumber} · ACTIVE WAVE"
                : $"{waveNumber} · PAUSED WAVE";

        if (backgroundImage != null)
            backgroundImage.color = isActive ? activeColor : pausedColor;

        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (indicatorRoot != null && indicatorRoot.activeSelf != visible)
            indicatorRoot.SetActive(visible);
    }
}
