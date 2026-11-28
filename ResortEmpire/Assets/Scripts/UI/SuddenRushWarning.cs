using UnityEngine;
using UnityEngine.Events;

// Heads-up shortly before a wave's Sudden Rush starts, so the player can notice it coming.
// Timing uses wave time, so a paused wave also pauses the countdown. Fires once per wave.
// Put the awareness animation in OnRushIncoming, or hook it to the event in the Inspector.
[DisallowMultipleComponent]
public sealed class SuddenRushWarning : MonoBehaviour
{
    [Tooltip("Found automatically when empty.")]
    [SerializeField] private GameController gameController;

    [Tooltip("How many seconds of wave time before the rush the warning fires.")]
    [SerializeField, Min(0f)] private float leadSeconds = 3f;

    [Tooltip("Optional: animation, sound or anything else to play with the warning.")]
    [SerializeField] private UnityEvent onRushIncoming = new UnityEvent();

    // The wave that already got its warning. A new WaveData is created for every wave start.
    private WaveData warnedWave;

    private void OnEnable()
    {
        if (gameController == null) gameController = FindFirstObjectByType<GameController>();
    }

    private void Update()
    {
        if (gameController == null || gameController.Phase != ResortPhase.WAVE_ACTIVE) return;

        WaveData wave = gameController.ActiveWave;
        SuddenRushState rush = wave?.suddenRush;
        if (rush == null || !rush.IsStillToStart || ReferenceEquals(wave, warnedWave)) return;

        double secondsLeft = rush.startSeconds - wave.elapsedSeconds;
        if (secondsLeft > leadSeconds) return;

        warnedWave = wave;
        bool hasTarget = gameController.TryGetLikelySuddenRushBuilding(out BuildingType likelyBuilding);
        OnRushIncoming(wave, rush, (float)System.Math.Max(0d, secondsLeft), hasTarget, likelyBuilding);
    }

    // Placeholder for the "rush is coming" feedback. likelyBuilding is the currently unstaffed
    // building; it can still change if the player moves a Worker before the rush starts.
    private void OnRushIncoming(WaveData wave, SuddenRushState rush, float secondsLeft,
                                bool hasTarget, BuildingType likelyBuilding)
    {
        string where = hasTarget ? $", most likely at {likelyBuilding}" : string.Empty;
        Debug.Log($"[Sudden Rush] Incoming in {secondsLeft:F1}s on wave {wave.waveNumber}{where} " +
                  $"(+{rush.units} guests over {rush.durationSeconds:F0}s).", this);

        onRushIncoming?.Invoke();
    }
}
