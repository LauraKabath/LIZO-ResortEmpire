using UnityEngine;
using UnityEngine.UI;

// Full-screen "Paused - tap to continue" layer, shown whenever the wave is paused: after the app
// comes back from the background, or after continuing a save that stopped mid-wave.
// Created by GameController from Resources/UI/PausedOverlay.
[DisallowMultipleComponent]
public sealed class PausedOverlayUI : MonoBehaviour
{
    [Tooltip("Found automatically when empty.")]
    [SerializeField] private GameController gameController;

    [Tooltip("The visible part of the overlay; switched on only while the wave is paused.")]
    [SerializeField] private GameObject overlayRoot;

    [Tooltip("Covers the whole screen, so a tap anywhere resumes the wave.")]
    [SerializeField] private Button continueButton;

    private void Awake()
    {
        if (continueButton != null) continueButton.onClick.AddListener(Continue);
        if (overlayRoot != null) overlayRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (continueButton != null) continueButton.onClick.RemoveListener(Continue);
    }

    public void Bind(GameController controller) => gameController = controller;

    private void Update()
    {
        if (gameController == null) gameController = FindFirstObjectByType<GameController>();
        bool show = gameController != null && gameController.IsInitialized &&
                    gameController.Phase == ResortPhase.WAVE_PAUSED;
        if (overlayRoot != null && overlayRoot.activeSelf != show) overlayRoot.SetActive(show);
    }

    private void Continue()
    {
        if (gameController != null && gameController.Phase == ResortPhase.WAVE_PAUSED)
            gameController.ResumeWave();
    }
}
