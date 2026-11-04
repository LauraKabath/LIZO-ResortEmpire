using System;
using System.Collections.Generic;

// Worker data and operations for the UI, independent of Unity scene objects.
// Everything lives in ResortData, so assignments survive between waves and the
// running WaveSimulation picks up changes immediately.
public sealed class WorkerRoster
{
    private readonly ResortData data;
    private readonly Func<BuildingType, BuildingDefinition> getDefinition;

    public IReadOnlyList<WorkerData> Workers => data.workers;

    public event Action Changed;

    public WorkerRoster(ResortData data, Func<BuildingType, BuildingDefinition> getDefinition)
    {
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.getDefinition = getDefinition ?? throw new ArgumentNullException(nameof(getDefinition));
    }

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
        if (!WorkerAllocation.TryAssign(data, workerId, target, out error)) return false;
        Changed?.Invoke();
        return true;
    }

    public bool TryUnassign(int workerId, out string error)
    {
        if (!WorkerAllocation.TryUnassign(data, workerId, out error)) return false;
        Changed?.Invoke();
        return true;
    }
}
