using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Attach only to a test scene. Selecting a wave prepares it as though earlier waves were completed.
[DisallowMultipleComponent]
public sealed class IsometricTestWaveSelector : MonoBehaviour
{
    [SerializeField] private GameController gameController;
    [SerializeField] private Dropdown waveDropdown;
    [SerializeField, Min(0)] private int startingCoins = 3700;

    private int[] waveNumbers = Array.Empty<int>();

    private void OnEnable()
    {
        waveDropdown.onValueChanged.AddListener(SelectAt);
        gameController.PhaseChanged += Refresh;
        Refresh(gameController.Phase);
    }

    private void OnDisable()
    {
        gameController.PhaseChanged -= Refresh;
        waveDropdown.onValueChanged.RemoveListener(SelectAt);
    }

    private void SelectAt(int index)
    {
        if (index >= 0 && index < waveNumbers.Length)
            gameController.TrySelectWaveForTesting(waveNumbers[index]);
    }

    private void Refresh(ResortPhase phase)
    {
        waveDropdown.interactable = gameController.IsInitialized;
        if (!waveDropdown.interactable) return;
        if (waveNumbers.Length == 0)
        {
            gameController.Data.coins = startingCoins;
            waveNumbers = gameController.WaveDefinitions.Select(wave => wave.waveNumber).OrderBy(number => number).ToArray();
            waveDropdown.ClearOptions();
            waveDropdown.AddOptions(waveNumbers.Select(number => $"Wave {number}").ToList());
        }

        int waveNumber = gameController.Data.nextWaveNumber;
        int index = Array.IndexOf(waveNumbers, waveNumber);
        waveDropdown.SetValueWithoutNotify(index);
        waveDropdown.RefreshShownValue();
        if (phase == ResortPhase.WAVE_COMPLETE)
            waveDropdown.captionText.text = $"Wave {waveNumber} complete";
    }
}
