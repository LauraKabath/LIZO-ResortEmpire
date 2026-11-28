using System;
using System.Collections.Generic;

// Snapshot building tuning while retaining the live worker assignment.
// Keeping Unity assets outside the simulation lets the same rules run in tests.
public sealed class WaveBuildingService
{
    public BuildingData Building { get; }
    public NeedType Need { get; }
    public int Capacity { get; }
    public double ServiceTimeSeconds { get; }
    public double UtilizationWithoutWorker { get; }

    // Per-wave Energy boost state (not part of the permanent building definition).
    public bool BoostUsed { get; private set; }
    public bool BoostActive { get; private set; }
    public double BoostEndsAtSeconds { get; private set; }
    private double boostSpeedMultiplier = 1d;

    public double ProgressPerSecond =>
        (Building.assignedWorkerId == -1 ? UtilizationWithoutWorker : 1d)
        / ServiceTimeSeconds * (BoostActive ? boostSpeedMultiplier : 1d);

    internal void ActivateBoost(double endsAtSeconds, double speedMultiplier)
    {
        BoostUsed = true;
        BoostActive = true;
        BoostEndsAtSeconds = endsAtSeconds;
        boostSpeedMultiplier = speedMultiplier;
    }

    internal void ExpireBoost()
    {
        BoostActive = false;
    }

    internal BoostSaveData CreateBoostSnapshot() => new BoostSaveData
    {
        building = Building.type,
        used = BoostUsed,
        active = BoostActive,
        endsAtSeconds = BoostEndsAtSeconds,
        speedMultiplier = boostSpeedMultiplier
    };

    internal void RestoreBoost(BoostSaveData saved)
    {
        BoostUsed = saved.used;
        BoostActive = saved.active;
        BoostEndsAtSeconds = saved.endsAtSeconds;
        boostSpeedMultiplier = saved.active ? saved.speedMultiplier : 1d;
    }

    public WaveBuildingService(BuildingData building, NeedType need, int capacity,
        double serviceTimeSeconds, double utilizationWithoutWorker)
    {
        Building = building ?? throw new ArgumentNullException(nameof(building));
        if (!Enum.IsDefined(typeof(NeedType), need) || capacity < 1 ||
            double.IsNaN(serviceTimeSeconds) || double.IsInfinity(serviceTimeSeconds) || serviceTimeSeconds <= 0 ||
            double.IsNaN(utilizationWithoutWorker) || double.IsInfinity(utilizationWithoutWorker) ||
            utilizationWithoutWorker <= 0 || utilizationWithoutWorker > 1)
            throw new ArgumentException("Building service requires a valid need, positive capacity/time and utilization in (0, 1].");
        Need = need;
        Capacity = capacity;
        ServiceTimeSeconds = serviceTimeSeconds;
        UtilizationWithoutWorker = utilizationWithoutWorker;
    }
}

// Processes arrivals and service completions in time order, independent of frame length.
public sealed class WaveSimulation
{
    private const double Epsilon = 1e-9;
    private readonly List<WaveBuildingService> services = new List<WaveBuildingService>();

    public WaveData Data { get; }
    public event Action<WaveNeedData, NeedUnit> NeedGenerated;
    public event Action ArrivalsClosed;
    public event Action<SuddenRushState> SuddenRushStarted;
    public event Action<SuddenRushState> SuddenRushSkipped;

    // Records the player's answer once. Phase and action checks belong to the controller.
    public bool TryAnswerSuddenRush(SuddenRushChoice choice)
    {
        SuddenRushState rush = Data.suddenRush;
        if (choice == SuddenRushChoice.NONE || rush == null || !rush.IsPromptPending || Data.isComplete)
            return false;
        rush.choice = choice;
        return true;
    }

    public bool HasUsedEnergyBoost(BuildingType type)
    {
        WaveBuildingService service = FindService(type);
        return service != null && service.BoostUsed;
    }

    public bool IsEnergyBoostActive(BuildingType type)
    {
        WaveBuildingService service = FindService(type);
        return service != null && service.BoostActive;
    }

    public double GetEnergyBoostRemainingSeconds(BuildingType type)
    {
        WaveBuildingService service = FindService(type);
        return service != null && service.BoostActive
            ? Math.Max(0d, service.BoostEndsAtSeconds - Data.elapsedSeconds)
            : 0d;
    }

    // The controller validates phase and Energy before requesting activation.
    public bool TryActivateEnergyBoost(BuildingType type, double durationSeconds,
        double speedMultiplier, out string error)
    {
        if (Data.isComplete)
        {
            error = "The wave is already complete.";
            return false;
        }

        if (durationSeconds <= 0d || double.IsNaN(durationSeconds) ||
            double.IsInfinity(durationSeconds) || speedMultiplier <= 1d ||
            double.IsNaN(speedMultiplier) || double.IsInfinity(speedMultiplier))
        {
            error = "Invalid Energy boost configuration.";
            return false;
        }

        WaveBuildingService service = FindService(type);
        if (service == null)
        {
            error = "This building is not serving the current wave.";
            return false;
        }

        if (service.BoostUsed)
        {
            error = "Energy boost has already been used on this building this wave.";
            return false;
        }

        service.ActivateBoost(Data.elapsedSeconds + durationSeconds, speedMultiplier);
        error = null;
        return true;
    }

    private WaveBuildingService FindService(BuildingType type)
    {
        foreach (WaveBuildingService service in services)
            if (service.Building.type == type)
                return service;
        return null;
    }

    // Continues a saved wave exactly where it stopped: same guests, timers, rush and boosts.
    // The services must be built from the same owned buildings, in the same order, as when the wave started.
    public static WaveSimulation Restore(WaveData savedWave, IEnumerable<WaveBuildingService> buildingServices,
        IEnumerable<BoostSaveData> boosts)
    {
        if (savedWave == null) throw new ArgumentNullException(nameof(savedWave));
        if (buildingServices == null) throw new ArgumentNullException(nameof(buildingServices));

        var simulation = new WaveSimulation(savedWave);
        foreach (WaveBuildingService service in buildingServices)
        {
            if (service == null) throw new ArgumentException("A building service is missing.");
            if (service.Building.owned) simulation.services.Add(service);
        }

        if (simulation.services.Count != savedWave.needs.Count)
            throw new InvalidOperationException("The saved wave does not match the owned buildings.");
        for (int index = 0; index < savedWave.needs.Count; index++)
        {
            WaveNeedData need = savedWave.needs[index];
            WaveBuildingService service = simulation.services[index];
            if (need.building != service.Building.type || need.need != service.Need || need.capacity != service.Capacity)
                throw new InvalidOperationException($"The saved wave does not match {service.Building.type}.");
        }

        if (boosts != null)
        {
            foreach (BoostSaveData boost in boosts)
            {
                if (boost == null) continue;
                WaveBuildingService service = simulation.FindService(boost.building);
                if (service == null)
                    throw new InvalidOperationException($"Saved Energy boost for {boost.building}, which is not in the wave.");
                service.RestoreBoost(boost);
            }
        }

        // Rates depend on the restored Workers and boosts.
        for (int index = 0; index < savedWave.needs.Count; index++)
            savedWave.needs[index].fillRate = savedWave.needs[index].capacity * simulation.services[index].ProgressPerSecond;
        return simulation;
    }

    private WaveSimulation(WaveData savedWave)
    {
        Data = savedWave;
        if (Data.suddenRush == null) Data.suddenRush = new SuddenRushState();
    }

    public List<BoostSaveData> CreateBoostSnapshots()
    {
        var boosts = new List<BoostSaveData>();
        foreach (WaveBuildingService service in services)
            if (service.BoostUsed) boosts.Add(service.CreateBoostSnapshot());
        return boosts;
    }

    public WaveSimulation(WaveDefinition definition, double arrivalDurationSeconds,
        IEnumerable<WaveBuildingService> buildingServices)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        definition.Validate();
        if (double.IsNaN(arrivalDurationSeconds) || double.IsInfinity(arrivalDurationSeconds) || arrivalDurationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(arrivalDurationSeconds));
        if (buildingServices == null) throw new ArgumentNullException(nameof(buildingServices));

        Data = new WaveData
        {
            waveNumber = definition.waveNumber,
            guestCount = definition.guestCount,
            arrivalDurationSeconds = arrivalDurationSeconds
        };

        var activeNeeds = new HashSet<NeedType>();
        foreach (WaveBuildingService service in buildingServices)
        {
            if (service == null) throw new ArgumentException("A building service is missing.");
            // Locked buildings generate no demand. Eligibility is captured at wave start.
            if (!service.Building.owned) continue;
            if (!activeNeeds.Add(service.Need))
                throw new ArgumentException($"More than one building serves {service.Need}.");

            int total = definition.GetDemand(service.Need);
            services.Add(service);
            Data.needs.Add(new WaveNeedData
            {
                need = service.Need,
                building = service.Building.type,
                demandTotal = total,
                arrivalRate = total / arrivalDurationSeconds,
                capacity = service.Capacity,
                serviceTimeSeconds = service.ServiceTimeSeconds,
                fillRate = service.Capacity * service.ProgressPerSecond
            });
        }

        if (definition.HasSuddenRush)
        {
            Data.suddenRush = new SuddenRushState
            {
                planned = true,
                startSeconds = definition.suddenRush.startSeconds,
                durationSeconds = definition.suddenRush.durationSeconds,
                units = definition.suddenRush.units
            };
        }
    }

    public void Tick(double deltaTime)
    {
        if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime) || deltaTime < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        if (Data.isComplete || deltaTime == 0) return;

        double targetTime = Data.elapsedSeconds + deltaTime;
        if (double.IsInfinity(targetTime)) throw new ArgumentOutOfRangeException(nameof(deltaTime));

        while (Data.elapsedSeconds < targetTime && !Data.isComplete)
        {
            double nextTime = targetTime;
            if (!Data.arrivalsClosed)
                nextTime = Math.Min(nextTime, Data.arrivalDurationSeconds);

            // The rush starts and its units arrive at exact wave times, whatever the frame length.
            SuddenRushState rush = Data.suddenRush;
            if (rush != null && rush.IsStillToStart)
                nextTime = Math.Min(nextTime, Math.Max(Data.elapsedSeconds, rush.startSeconds));
            if (rush != null && rush.IsArriving)
                nextTime = Math.Min(nextTime, Math.Max(Data.elapsedSeconds, rush.NextArrivalSeconds));

            for (int index = 0; index < services.Count; index++)
            {
                WaveBuildingService service = services[index];
                // A large frame cannot extend a boost past its exact expiration time.
                if (service.BoostActive)
                    nextTime = Math.Min(nextTime, service.BoostEndsAtSeconds);

                WaveNeedData need = Data.needs[index];
                double rate = service.ProgressPerSecond;
                need.fillRate = need.capacity * rate;
                if (!Data.arrivalsClosed && need.RegularDemandArrived < need.RegularDemandTotal)
                    nextTime = Math.Min(nextTime, NextArrivalTime(need));
                foreach (NeedUnit unit in need.inServiceUnits)
                    nextTime = Math.Min(nextTime,
                        Data.elapsedSeconds + Math.Max(0, 1 - unit.serviceProgress) / rate);
            }

            double step = Math.Max(0, nextTime - Data.elapsedSeconds);
            for (int index = 0; index < services.Count; index++)
            {
                double rate = services[index].ProgressPerSecond;
                foreach (NeedUnit unit in Data.needs[index].inServiceUnits)
                    unit.serviceProgress = Math.Min(1, unit.serviceProgress + step * rate);
            }
            Data.elapsedSeconds = nextTime;

            // The preceding step was processed at boosted speed until this instant.
            for (int index = 0; index < services.Count; index++)
            {
                WaveBuildingService service = services[index];
                if (service.BoostActive && Data.elapsedSeconds >= service.BoostEndsAtSeconds)
                    service.ExpireBoost();

                Data.needs[index].fillRate =
                    Data.needs[index].capacity * service.ProgressPerSecond;
            }

            TryStartSuddenRush();

            foreach (WaveNeedData need in Data.needs)
            {
                CompleteService(need);
                SeatWaitingUnits(need);
                GenerateArrivals(need);
            }

            if (!Data.arrivalsClosed && Data.elapsedSeconds >= Data.arrivalDurationSeconds)
            {
                Data.arrivalsClosed = true;
                ArrivalsClosed?.Invoke();
            }

            if (Data.arrivalsClosed && AllNeedsFinished())
                Data.isComplete = true;
        }
    }

    private double NextArrivalTime(WaveNeedData need)
    {
        // Regular arrivals only. The first follows one interval; the final one lands exactly at the cutoff.
        return Data.arrivalDurationSeconds * ((need.RegularDemandArrived + 1d) / need.RegularDemandTotal);
    }

    private void GenerateArrivals(WaveNeedData need)
    {
        if (!Data.arrivalsClosed)
        {
            while (need.RegularDemandArrived < need.RegularDemandTotal &&
                   NextArrivalTime(need) <= Data.elapsedSeconds)
                AddArrival(need, NextArrivalTime(need));
        }

        // Rush units keep arriving even if the regular arrival window has closed in the meantime.
        SuddenRushState rush = Data.suddenRush;
        if (rush == null || !rush.started || rush.need != need.need) return;
        while (rush.IsArriving && rush.NextArrivalSeconds <= Data.elapsedSeconds)
        {
            double arrivedAt = rush.NextArrivalSeconds;
            rush.arrived++;
            need.rushArrived++;
            AddArrival(need, arrivedAt);
        }
        if (!rush.IsArriving) need.rushArrivalRate = 0;
    }

    private void AddArrival(WaveNeedData need, double arrivedAtSeconds)
    {
        var unit = new NeedUnit
        {
            id = Data.nextUnitId++,
            need = need.need,
            building = need.building,
            arrivedAtSeconds = arrivedAtSeconds
        };
        need.demandArrived++;
        need.queuedUnits.Add(unit);
        SeatWaitingUnits(need);
        NeedGenerated?.Invoke(need, unit);
    }

    // Starts the rush once its time is reached. It targets a building without a Worker, so moving
    // a Worker there is always a free answer. With several, the one with the most demand is chosen.
    // The building the rush would hit if it started now. Before the start this is only a
    // prediction: moving a Worker can still change it.
    public bool TryGetSuddenRushTarget(out BuildingType building)
    {
        int target = FindSuddenRushTarget();
        building = target >= 0 ? Data.needs[target].building : default;
        return target >= 0;
    }

    private int FindSuddenRushTarget()
    {
        int target = -1;
        for (int index = 0; index < services.Count; index++)
        {
            if (services[index].Building.assignedWorkerId != -1) continue;
            if (target < 0) { target = index; continue; }

            int demand = Data.needs[index].RegularDemandTotal;
            int bestDemand = Data.needs[target].RegularDemandTotal;
            if (demand > bestDemand ||
                (demand == bestDemand && (int)services[index].Building.type < (int)services[target].Building.type))
                target = index;
        }
        return target;
    }

    private void TryStartSuddenRush()
    {
        SuddenRushState rush = Data.suddenRush;
        if (rush == null || !rush.IsStillToStart || Data.elapsedSeconds < rush.startSeconds) return;

        int target = FindSuddenRushTarget();
        if (target < 0)
        {
            rush.skipped = true;
            SuddenRushSkipped?.Invoke(rush);
            return;
        }

        WaveNeedData need = Data.needs[target];
        rush.started = true;
        rush.building = need.building;
        rush.need = need.need;
        need.rushUnits += rush.units;
        need.demandTotal += rush.units;
        need.rushArrivalRate = rush.units / rush.durationSeconds;
        SuddenRushStarted?.Invoke(rush);
    }

    private void SeatWaitingUnits(WaveNeedData need)
    {
        while (need.queuedUnits.Count > 0 && need.inServiceUnits.Count < need.capacity)
        {
            NeedUnit unit = need.queuedUnits[0];
            need.queuedUnits.RemoveAt(0);
            unit.seatedAtSeconds = Data.elapsedSeconds;
            need.inServiceUnits.Add(unit);
        }
    }

    private void CompleteService(WaveNeedData need)
    {
        for (int index = 0; index < need.inServiceUnits.Count;)
        {
            NeedUnit unit = need.inServiceUnits[index];
            if (unit.serviceProgress < 1 - Epsilon)
            {
                index++;
                continue;
            }
            unit.serviceProgress = 1;
            unit.completedAtSeconds = Data.elapsedSeconds;
            need.inServiceUnits.RemoveAt(index);
            need.completedUnits.Add(unit);
            need.filled++;
        }
    }

    private bool AllNeedsFinished()
    {
        // A wave cannot finish before its rush has started and fully arrived.
        SuddenRushState rush = Data.suddenRush;
        if (rush != null && (rush.IsStillToStart || rush.IsArriving)) return false;

        foreach (WaveNeedData need in Data.needs)
        {
            if (need.demandArrived != need.demandTotal || need.QueueCount != 0 || need.OccupiedSlots != 0)
                return false;
        }
        return true;
    }
}
