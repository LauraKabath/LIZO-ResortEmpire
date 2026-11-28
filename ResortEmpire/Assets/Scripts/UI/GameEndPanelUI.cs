using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// "Game finished" screen, shown after the rewards of the last wave are claimed.
// Put this on a controller object (like WaveCompleteUIController), not on panelRoot itself,
// so it keeps listening while the panel is hidden.
[DisallowMultipleComponent]
public sealed class GameEndPanelUI : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private GameController gameController;
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Hidden when the game finished screen opens, e.g. the Wave Complete panel.")]
    [SerializeField] private GameObject[] hideWhenShown = new GameObject[0];

    [Header("Texts (optional)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private string title = "GAME COMPLETE!";

    [Header("Buttons (optional)")]
    [SerializeField] private Button playAgainButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        if (panelRoot == gameObject)
            Debug.LogWarning("GameEndPanelUI: panelRoot should be a separate object, otherwise the screen stops listening while hidden.", this);
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameController != null)
        {
            gameController.FinalWaveRewardsClaimed += Show;
            gameController.PhaseChanged += HandlePhaseChanged;
        }
        if (playAgainButton != null) playAgainButton.onClick.AddListener(PlayAgain);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    private void OnDisable()
    {
        if (gameController != null)
        {
            gameController.FinalWaveRewardsClaimed -= Show;
            gameController.PhaseChanged -= HandlePhaseChanged;
        }
        if (playAgainButton != null) playAgainButton.onClick.RemoveListener(PlayAgain);
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(GoToMainMenu);
    }

    // A later phase change (for example the test wave selector) closes the screen again.
    private void HandlePhaseChanged(ResortPhase phase)
    {
        if (panelRoot != null && phase != ResortPhase.WAVE_COMPLETE)
            panelRoot.SetActive(false);
    }

    // The context menu entry lets you preview the screen in Play mode without finishing every wave.
    [ContextMenu("Show Game Finished Screen")]
    public void Show()
    {
        if (panelRoot == null)
        {
            Debug.LogWarning("GameEndPanelUI: assign panelRoot in the Inspector.", this);
            return;
        }

        if (titleText != null)
            titleText.text = title;
        if (messageText != null)
        {
            int waves = gameController != null && gameController.IsInitialized
                ? gameController.Data.nextWaveNumber
                : 0;
            if (waves > 1)
                messageText.text = $"You completed all {waves} waves.\nThanks for playing!";
            else if (waves == 1)
                messageText.text = "You completed the wave.\nThanks for playing!";
            else
                messageText.text = "Thanks for playing!";
        }

        foreach (GameObject other in hideWhenShown)
            if (other != null) other.SetActive(false);
        panelRoot.SetActive(true);
    }

    // The finished game is still the saved game, so the reloaded scene must start a new one
    // (which also replaces the save); otherwise it would continue straight back to this screen.
    public void PlayAgain()
    {
        GameLaunch.Request(GameLaunch.Mode.NewGame);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }
}
