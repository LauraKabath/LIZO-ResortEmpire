using System;

// Pure rules for the prototype's one-slot-per-building, no-bench worker system.
public static class WorkerAllocation
{
    public static bool TryMove(ResortData resort, int workerId, BuildingType target,
        out string error)
    {
        error = null;
        if (resort == null) { error = "Missing resort data."; return false; }
        WorkerData worker = resort.GetWorker(workerId);
        BuildingData destination = resort.GetBuilding(target);
        if (worker == null || !worker.isAssigned)
        {
            error = "Worker does not exist or is not assigned.";
            return false;
        }
        if (destination == null || !destination.owned)
        {
            error = "Target building is not owned.";
            return false;
        }
        BuildingData source = resort.GetBuilding(worker.assignedBuilding);
        if (source == null || source.assignedWorkerId != worker.id)
        {
            error = "Worker assignment is inconsistent.";
            return false;
        }
        if (source.type == destination.type) return true; // No change.
        if (destination.assignedWorkerId != -1)
        {
            error = "Target worker slot is already occupied.";
            return false;
        }

        source.assignedWorkerId = -1;
        destination.assignedWorkerId = worker.id;
        worker.assignedBuilding = target;
        return true;
    }

    public static float GetUtilization(BuildingDefinition definition, BuildingData state)
    {
        if (definition == null || state == null || !state.owned)
            throw new ArgumentException("Missing definition or owned building.");
        return state.assignedWorkerId == -1 ? definition.UtilizationWithoutWorker : 1f;
    }

    public static float GetFillRate(BuildingDefinition definition, BuildingData state)
    {
        if (definition.Type != state.type)
            throw new ArgumentException("Building definition and state types differ.");
        return definition.GetCapacity(state.level) * GetUtilization(definition, state)
               / definition.ServiceTimeSeconds;
    }
}
