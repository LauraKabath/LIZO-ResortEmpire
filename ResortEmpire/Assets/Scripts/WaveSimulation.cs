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
    public double ProgressPerSecond =>
        (Building.assignedWorkerId == -1 ? UtilizationWithoutWorker : 1) / ServiceTimeSeconds;

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
    private int nextUnitId = 1;

    public WaveData Data { get; }
    public event Action<WaveNeedData, NeedUnit> NeedGenerated;
    public event Action ArrivalsClosed;

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

            for (int index = 0; index < services.Count; index++)
            {
                WaveNeedData need = Data.needs[index];
                double rate = services[index].ProgressPerSecond;
                need.fillRate = need.capacity * rate;
                if (!Data.arrivalsClosed && need.demandArrived < need.demandTotal)
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
        // First arrival follows one interval; the final one lands exactly at the cutoff.
        return Data.arrivalDurationSeconds * ((need.demandArrived + 1d) / need.demandTotal);
    }

    private void GenerateArrivals(WaveNeedData need)
    {
        if (Data.arrivalsClosed) return;
        while (need.demandArrived < need.demandTotal && NextArrivalTime(need) <= Data.elapsedSeconds)
        {
            var unit = new NeedUnit
            {
                id = nextUnitId++,
                need = need.need,
                building = need.building,
                arrivedAtSeconds = NextArrivalTime(need)
            };
            need.demandArrived++;
            need.queuedUnits.Add(unit);
            SeatWaitingUnits(need);
            NeedGenerated?.Invoke(need, unit);
        }
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
        foreach (WaveNeedData need in Data.needs)
        {
            if (need.demandArrived != need.demandTotal || need.QueueCount != 0 || need.OccupiedSlots != 0)
                return false;
        }
        return true;
    }
}
