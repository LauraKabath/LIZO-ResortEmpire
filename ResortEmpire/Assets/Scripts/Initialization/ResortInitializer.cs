using System;
using System.Collections.Generic;

/// Creates independent, valid new-game data; not responsible for loading saves.
public static class ResortInitializer
{
    public static ResortData CreateNewGame(ResortInitialSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings), "Assign ResortInitialSettings first.");

        if (settings.StartingCoins < 0 || settings.StartingEnergy < 0 ||
            settings.StartingResortValue < 0 || settings.StartingReputation < 0 ||
            settings.StartingGems < 0 || settings.StartingMaterials < 0)
            throw new ArgumentException("Starting resources cannot be negative.", nameof(settings));

        ResortData data = ResortData.CreatePrototypeStart(
            settings.StartingCoins, settings.StartingEnergy, settings.StartingResortValue, settings.WorkerCapPercent);
        data.reputation = settings.StartingReputation;
        data.gems = settings.StartingGems;
        data.materials = settings.StartingMaterials;
        data.phase = ResortPhase.AWAITING_WAVE;
        data.nextWaveNumber = 1;

        ValidateNewGame(data, settings.WorkerCapPercent);
        return data;
    }

    public static void ValidateNewGame(ResortData data, int workerCapPercent = WorkerRoster.DefaultCapPercent)
    {
        if (data == null || data.buildings == null || data.workers == null)
            throw new InvalidOperationException("New-game state or its lists are missing.");
        if (data.phase != ResortPhase.AWAITING_WAVE || data.nextWaveNumber != 1)
            throw new InvalidOperationException("A new game must await Wave 1.");
        if (data.coins < 0 || data.energy < 0 || data.resortValue < 0 ||
            data.reputation < 0 || data.gems < 0 || data.materials < 0)
            throw new InvalidOperationException("New-game resources cannot be negative.");

        var expectedOwnership = new Dictionary<BuildingType, bool>
        {
            { BuildingType.Reception, true },
            { BuildingType.Restaurant, true },
            { BuildingType.BeachBar, true },
            { BuildingType.Pool, false },
            { BuildingType.Bungalows, false }
        };

        if (data.buildings.Count != expectedOwnership.Count)
            throw new InvalidOperationException("New game must have five building records.");

        var buildingsSeen = new HashSet<BuildingType>();
        var assignedIds = new HashSet<int>();
        int startingSlots = 0;
        foreach (BuildingData building in data.buildings)
        {
            if (building == null || !expectedOwnership.TryGetValue(building.type, out bool owned) ||
                !buildingsSeen.Add(building.type))
                throw new InvalidOperationException("Unknown or duplicate building record.");
            if (building.owned != owned || building.availableToPurchase || building.level != 1)
                throw new InvalidOperationException($"Invalid initial state for {building.type}.");
            if (owned) startingSlots++;
            if (!building.owned && building.assignedWorkerId != -1)
                throw new InvalidOperationException("Locked building cannot have a worker.");
            if (building.assignedWorkerId != -1 && !assignedIds.Add(building.assignedWorkerId))
                throw new InvalidOperationException("A worker occupies more than one building.");
        }

        int workerCap = WorkerRoster.CalculateCap(startingSlots, workerCapPercent);
        if (data.workers.Count != workerCap || assignedIds.Count != workerCap)
            throw new InvalidOperationException($"Prototype must start with {workerCap} assigned Workers.");

        var workerIds = new HashSet<int>();
        foreach (WorkerData worker in data.workers)
        {
            if (worker == null || worker.id <= 0 || !workerIds.Add(worker.id) ||
                !worker.isAssigned || !assignedIds.Contains(worker.id))
                throw new InvalidOperationException("Invalid or duplicate worker record.");
            BuildingData building = data.GetBuilding(worker.assignedBuilding);
            if (building == null || !building.owned || building.assignedWorkerId != worker.id)
                throw new InvalidOperationException("Worker-to-building assignment mismatch.");
        }
    }
}
