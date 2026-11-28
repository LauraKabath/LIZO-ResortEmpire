using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField]
    private string gameSceneName = "IsometricSetupScene";

    [Tooltip("Can only be pressed when there is a save to continue.")]
    [SerializeField] private Button continueButton;

    [Tooltip("Optional: a short message, e.g. when the save could not be read.")]
    [SerializeField] private TMP_Text statusText;

    [Tooltip("Asks before a new game replaces the save. Loaded from Resources/UI/ConfirmDialog when empty.")]
    [SerializeField] private ConfirmDialogUI confirmDialogPrefab;

    private bool hasValidSave;
    private bool dialogOpen;

    private void Start()
    {
        SaveLoadResult save = SaveSystem.Peek();
        hasValidSave = save.HasData;
        if (continueButton != null) continueButton.interactable = hasValidSave;

        string message = null;
        if (save.Status == SaveLoadStatus.Unreadable)
        {
            message = "Your saved game could not be read. Start a new game to keep playing.";
            Debug.LogWarning($"[Save] {save.Problem}", this);
        }

        if (statusText != null)
        {
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(message != null);
        }
    }

    public void NewGame()
    {
        if (dialogOpen) return;
        if (!hasValidSave)
        {
            StartNewGame();
            return;
        }

        ConfirmDialogUI prefab = confirmDialogPrefab != null
            ? confirmDialogPrefab
            : Resources.Load<ConfirmDialogUI>("UI/ConfirmDialog");
        if (prefab == null)
        {
            Debug.LogError(
                "ConfirmDialog prefab is missing. New Game was cancelled to protect the existing save.",
                this);
            return;
        }

        dialogOpen = true;
        ConfirmDialogUI.Show(prefab,
            "Start a new game?",
            "Your saved progress will be replaced. This cannot be undone.",
            "New Game", "Cancel",
            () =>
            {
                dialogOpen = false;
                StartNewGame();
            },
            () => dialogOpen = false);
    }

    public void ContinueGame()
    {
        if (!hasValidSave)
        {
            Debug.Log("No saved game is available yet.", this);
            return;
        }

        GameLaunch.Request(GameLaunch.Mode.Continue);
        SceneManager.LoadScene(gameSceneName);
    }

    private void StartNewGame()
    {
        SaveSystem.Delete();
        GameLaunch.Request(GameLaunch.Mode.NewGame);
        SceneManager.LoadScene(gameSceneName);
    }
}
