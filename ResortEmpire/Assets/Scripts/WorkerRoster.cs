using System;
using System.Collections.Generic;

// Worker data and operations for the UI, independent of Unity scene objects.
// Everything lives in ResortData, so assignments survive between waves and the
// running WaveSimulation picks up changes immediately.
public sealed class WorkerRoster
{
    public const int DefaultCapPercent = 80;

    private readonly ResortData data;
    private readonly Func<BuildingType, BuildingDefinition> getDefinition;
    private readonly int capPercent;
    private readonly Func<bool> canChangeWorkers;

    public IReadOnlyList<WorkerData> Workers => data.workers;
    public int WorkerCap => GetCapWithAdditionalSlots(0);

    internal int GetCapWithAdditionalSlots(int additionalSlots)
    {
        int slots = additionalSlots;
        foreach (BuildingData building in data.buildings)
        {
            if (building.owned)
                slots += getDefinition(building.type).GetWorkerSlots(building.level);
        }
        return CalculateCap(slots, capPercent);
    }

    // The game phase decides when Workers may change (ResortSession.CanManageWorkers).
    public bool CanChangeWorkers => canChangeWorkers == null || canChangeWorkers();

    public event Action Changed;

    internal void NotifyChanged() => Changed?.Invoke();

    public WorkerRoster(ResortData data, Func<BuildingType, BuildingDefinition> getDefinition,
        int capPercent = DefaultCapPercent, Func<bool> canChangeWorkers = null)
    {
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.getDefinition = getDefinition ?? throw new ArgumentNullException(nameof(getDefinition));
        this.capPercent = capPercent;
        this.canChangeWorkers = canChangeWorkers;
    }

    internal static int CalculateCap(int slots, int percent) =>
        slots * Math.Clamp(percent, 0, 99) / 100;

    public List<WorkerData> GetAvailableWorkers() => data.workers.FindAll(worker => !worker.isAssigned);

    // Locked buildings offer no slots until they are owned.
    public int GetTotalSlots(BuildingType type)
    {
        BuildingData building = data.GetBuilding(type);
        return building == null || !building.owned ? 0 : getDefinition(type).GetWorkerSlots(building.level);
    }

    public int GetOccupiedSlots(BuildingType type)
    {
        BuildingData building = data.GetBuilding(type);
        return building == null || building.assignedWorkerId == -1 ? 0 : 1;
    }

    public int GetFreeSlots(BuildingType type) => GetTotalSlots(type) - GetOccupiedSlots(type);

    public bool TryAssign(int workerId, BuildingType target, out string error)
    {
        if (!CheckPhase(out error) || !WorkerAllocation.TryAssign(data, workerId, target, out error)) return false;
        NotifyChanged();
        return true;
    }

    public bool TryUnassign(int workerId, out string error)
    {
        if (!CheckPhase(out error) || !WorkerAllocation.TryUnassign(data, workerId, out error)) return false;
        NotifyChanged();
        return true;
    }

    private bool CheckPhase(out string error)
    {
        error = CanChangeWorkers ? null : $"Workers cannot be changed during {data.phase}.";
        return error == null;
    }
}
