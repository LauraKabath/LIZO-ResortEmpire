using System;
using System.Collections.Generic;

// Everything written to the save file. Only plain data: it is restored into the running game by
// GameController. Building and wave definitions are not stored; they come from the project assets.
[Serializable]
public sealed class GameSaveData
{
    // Bump when the format changes in a way older code cannot read, and migrate in SaveValidator.
    public const int CurrentVersion = 1;

    public int saveVersion = CurrentVersion;
    public string savedAtUtc;

    // Phase, wave number, currencies, buildings and Workers.
    public ResortData resort;

    // The current wave: running, paused or finished but not yet claimed. Includes every guest unit,
    // timers and the Sudden Rush state.
    public bool hasWave;
    public WaveData wave;
    public List<BoostSaveData> boosts = new List<BoostSaveData>();
    public List<ServiceLoadSaveData> serviceLoads = new List<ServiceLoadSaveData>();

    // After a finished wave.
    public bool rewardClaimed;

    // An upgrade suggestion the player has not answered yet.
    public bool hasPendingUpgradeSuggestion;
    public int pendingSuggestionWave;
    public BuildingType pendingSuggestionBuilding;
    public double pendingSuggestionSeconds;
}

// Energy boost of one building in the current wave.
[Serializable]
public sealed class BoostSaveData
{
    public BuildingType building;
    public bool used;
    public bool active;
    public double endsAtSeconds;
    public double speedMultiplier = 1d;
}

// Bottleneck tracking of one need in the current wave (feeds the upgrade suggestion).
[Serializable]
public sealed class ServiceLoadSaveData
{
    public NeedType need;
    public ServiceLoadState state;
    public BottleneckCause cause;
    public int peakQueue;
    public double secondsAsBottleneck;
    public double secondsMissingWorker;
    public double secondsInsufficientCapacity;
}
