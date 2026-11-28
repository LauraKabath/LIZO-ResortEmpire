using System;
using System.Collections.Generic;

[Serializable]
public sealed class NeedUnit
{
    public int id;
    public NeedType need;
    public BuildingType building;
    public double arrivedAtSeconds;
    public double seatedAtSeconds = -1;
    public double completedAtSeconds = -1;
    public double serviceProgress;

    // Preserve actual queue waiting for the future satisfaction calculation.
    public double WaitingSeconds => seatedAtSeconds < 0 ? 0 : seatedAtSeconds - arrivedAtSeconds;
}

[Serializable]
public sealed class WaveNeedData
{
    public NeedType need;
    public BuildingType building;
    public int demandTotal;
    public double arrivalRate;
    public int demandArrived;
    public int filled;
    public int capacity;
    public double serviceTimeSeconds;
    public double fillRate;
    public List<NeedUnit> queuedUnits = new List<NeedUnit>();
    public List<NeedUnit> inServiceUnits = new List<NeedUnit>();
    public List<NeedUnit> completedUnits = new List<NeedUnit>();

    // Sudden Rush units are part of demandTotal/demandArrived, but arrive on their own schedule,
    // so the regular arrivals keep their original timing.
    public int rushUnits;
    public int rushArrived;
    public double rushArrivalRate; // Extra arrivals per second while the rush is arriving.

    public int QueueCount => queuedUnits.Count;
    public int OccupiedSlots => inServiceUnits.Count;
    public int Backlog => demandArrived - filled; // Includes seated units; the queue does not.
    public int RegularDemandTotal => demandTotal - rushUnits;
    public int RegularDemandArrived => demandArrived - rushArrived;
}

// Sudden Rush progress for one wave. Everything needed to restore the event, including an
// unanswered prompt, lives here so it is saved together with the wave.
[Serializable]
public sealed class SuddenRushState
{
    // Copied from Waves.json at wave start.
    public bool planned;
    public double startSeconds;
    public double durationSeconds;
    public int units;

    // Runtime state.
    public bool started;
    public bool skipped; // No unstaffed building was available when the rush was due.
    public BuildingType building;
    public NeedType need;
    public int arrived;
    public SuddenRushChoice choice;

    public bool IsArriving => started && arrived < units;
    public bool IsPromptPending => started && choice == SuddenRushChoice.NONE;
    public bool IsStillToStart => planned && !started && !skipped;

    // The units arrive evenly over the rush; the last one lands exactly at its end.
    public double NextArrivalSeconds => startSeconds + durationSeconds * (arrived + 1d) / units;
}

[Serializable]
public sealed class WaveData
{
    public int waveNumber;
    public int guestCount;
    public double arrivalDurationSeconds;
    public double elapsedSeconds;
    public bool arrivalsClosed;
    public bool isComplete;
    public int nextUnitId = 1;
    public List<WaveNeedData> needs = new List<WaveNeedData>();
    public SuddenRushState suddenRush = new SuddenRushState();

    public double ArrivalSecondsRemaining => Math.Max(0, arrivalDurationSeconds - elapsedSeconds);
    public bool IsDraining => arrivalsClosed && !isComplete;

    public WaveNeedData GetNeed(NeedType need)
    {
        return needs.Find(state => state.need == need);
    }
}
