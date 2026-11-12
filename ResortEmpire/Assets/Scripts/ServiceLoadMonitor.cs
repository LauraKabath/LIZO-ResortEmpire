using System;
using System.Collections.Generic;

public enum ServiceLoadState
{
    NORMAL,
    BOTTLENECK,
    RECOVERING
}

public enum BottleneckCause
{
    NONE,
    MISSING_WORKER,
    INSUFFICIENT_CAPACITY
}

// Live load of one need's service during a wave, for the Wave Needs Panel and wave results.
public sealed class ServiceLoad
{
    public NeedType Need { get; }
    public BuildingType Building { get; }
    public ServiceLoadState State { get; internal set; }
    // Set only while the service is a bottleneck.
    public BottleneckCause Cause { get; internal set; }
    public int QueueCount { get; internal set; }
    public int PeakQueue { get; internal set; }
    public double SecondsAsBottleneck { get; internal set; }
    // Needs per second: arriving guests, what the building finishes now, and what it would with a Worker.
    public double ArrivalRate { get; internal set; }
    public double ServiceRate { get; internal set; }
    public double ServiceRateWithWorker { get; internal set; }

    internal ServiceLoad(NeedType need, BuildingType building)
    {
        Need = need;
        Building = building;
    }
}

// Watches the queues of a running wave and flags services that cannot keep up: guests arrive
// faster than the building can serve them, so its queue grows. Comparing the rates, rather than
// waiting to see the queue grow, reacts at once when a Worker is assigned or moved away.
// It only reads WaveData and ResortData; call Update() after every WaveSimulation.Tick.
public sealed class ServiceLoadMonitor
{
    private const double Epsilon = 1e-9;

    private readonly WaveData wave;
    private readonly ResortData resort;
    private readonly List<ServiceLoad> loads = new List<ServiceLoad>();
    private double lastUpdateSeconds;

    public IReadOnlyList<ServiceLoad> Loads => loads;

    public event Action<ServiceLoad> StateChanged;

    public ServiceLoadMonitor(WaveData wave, ResortData resort)
    {
        this.wave = wave ?? throw new ArgumentNullException(nameof(wave));
        this.resort = resort ?? throw new ArgumentNullException(nameof(resort));
        foreach (WaveNeedData need in wave.needs)
            loads.Add(new ServiceLoad(need.need, need.building));
        lastUpdateSeconds = wave.elapsedSeconds;
        Update();
    }

    public ServiceLoad Get(NeedType need) => loads.Find(load => load.Need == need);

    public void Update()
    {
        double now = wave.elapsedSeconds;
        double step = Math.Max(0, now - lastUpdateSeconds);
        lastUpdateSeconds = now;

        for (int index = 0; index < loads.Count; index++)
        {
            WaveNeedData need = wave.needs[index];
            ServiceLoad load = loads[index];
            if (load.State == ServiceLoadState.BOTTLENECK) load.SecondsAsBottleneck += step;

            int queue = need.QueueCount;
            load.QueueCount = queue;
            load.PeakQueue = Math.Max(load.PeakQueue, queue);
            load.ArrivalRate = wave.arrivalsClosed ? 0 : need.arrivalRate;
            load.ServiceRate = need.fillRate;
            load.ServiceRateWithWorker = need.capacity / need.serviceTimeSeconds;

            bool tooSlow = load.ArrivalRate > load.ServiceRate + Epsilon;
            // Flagged once guests actually wait. It then stays a bottleneck while the service is too
            // slow, even if its queue briefly empties, so a slightly overloaded service cannot flicker.
            bool bottleneck = tooSlow && (queue > 0 || load.State == ServiceLoadState.BOTTLENECK);
            ServiceLoadState state = bottleneck ? ServiceLoadState.BOTTLENECK
                : queue > 0 && load.State != ServiceLoadState.NORMAL ? ServiceLoadState.RECOVERING
                : ServiceLoadState.NORMAL;

            // Assigning a Worker is free and instant, so it is suggested before more capacity.
            BuildingData building = resort.GetBuilding(need.building);
            bool staffed = building != null && building.assignedWorkerId != -1;
            BottleneckCause cause = state != ServiceLoadState.BOTTLENECK ? BottleneckCause.NONE
                : staffed ? BottleneckCause.INSUFFICIENT_CAPACITY
                : BottleneckCause.MISSING_WORKER;

            if (state == load.State && cause == load.Cause) continue;
            load.State = state;
            load.Cause = cause;
            StateChanged?.Invoke(load);
        }
    }
}
