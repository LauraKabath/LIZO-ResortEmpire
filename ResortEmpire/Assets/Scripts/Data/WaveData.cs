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

    public int QueueCount => queuedUnits.Count;
    public int OccupiedSlots => inServiceUnits.Count;
    public int Backlog => demandArrived - filled; // Includes seated units; the queue does not.
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
    public List<WaveNeedData> needs = new List<WaveNeedData>();

    public double ArrivalSecondsRemaining => Math.Max(0, arrivalDurationSeconds - elapsedSeconds);
    public bool IsDraining => arrivalsClosed && !isComplete;

    public WaveNeedData GetNeed(NeedType need)
    {
        return needs.Find(state => state.need == need);
    }
}
