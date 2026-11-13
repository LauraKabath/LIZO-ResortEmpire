using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Displays the evaluation of a completed wave.
[DisallowMultipleComponent]
public sealed class WaveCompletePanelUI : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private GameController gameController;
    [SerializeField] private GameObject panelRoot;

    [Header("Evaluation UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image starsImage;
    [Tooltip("Index 0 = 1 star, index 4 = 5 stars.")]
    [SerializeField] private Sprite[] ratingSprites = new Sprite[5];
    [SerializeField] private TMP_Text needsServedText;
    [SerializeField] private TMP_Text satisfactionText;
    [SerializeField] private TMP_Text successText;
    [SerializeField] private TMP_Text improvementText;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (gameController != null)
            gameController.PhaseChanged += HandlePhaseChanged;
    }

    private void Start()
    {
        // Handles script execution order and scenes loaded into an existing game phase.
        if (gameController != null && gameController.IsInitialized)
            HandlePhaseChanged(gameController.Phase);
    }

    private void OnDisable()
    {
        if (gameController != null)
            gameController.PhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(ResortPhase phase)
    {
        if (panelRoot == null)
            return;

        if (phase != ResortPhase.WAVE_COMPLETE)
        {
            panelRoot.SetActive(false);
            return;
        }

        if (gameController == null || gameController.LastWaveEvaluation == null ||
            gameController.ActiveWave == null)
        {
            Debug.LogWarning("Cannot show wave results: evaluation or completed wave data is missing.", this);
            panelRoot.SetActive(false);
            return;
        }

        WaveEvaluationResult result = gameController.LastWaveEvaluation;
        WaveData wave = gameController.ActiveWave;

        if (titleText != null)
            titleText.text = $"WAVE {result.WaveNumber} COMPLETE!";

        if (starsImage != null)
        {
            int index = result.Stars - 1;
            if (ratingSprites != null && index >= 0 && index < ratingSprites.Length &&
                ratingSprites[index] != null)
            {
                starsImage.sprite = ratingSprites[index];
                starsImage.enabled = true;
            }
            else
            {
                starsImage.enabled = false;
                Debug.LogWarning($"Missing star sprite for rating {result.Stars}.", this);
            }
        }

        int servedNeeds = 0;
        int totalNeeds = 0;
        if (wave.needs != null)
        {
            foreach (WaveNeedData need in wave.needs)
            {
                if (need == null) continue;
                servedNeeds += need.filled;
                totalNeeds += need.demandTotal;
            }
        }

        // WaveData tracks serviced need units, not unique guests.
        if (needsServedText != null)
            needsServedText.text = $"Needs Served: {servedNeeds}/{totalNeeds}";
        if (satisfactionText != null)
            satisfactionText.text = $"Avg. Satisfaction: {result.SatisfactionPercent:F0}%";
        if (successText != null)
            successText.text = result.Success;
        if (improvementText != null)
            improvementText.text = result.AreaToImprove;

        panelRoot.SetActive(true);
    }
}
