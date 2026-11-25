using System;

// Owns game phase rules independently of Unity scene objects.
public sealed class ResortSession
{
    public const int PurchaseLevel = 1;

    public ResortData Data { get; }
    public ResortPhase Phase => Data.phase;
    public bool CanStartWave => Phase == ResortPhase.AWAITING_WAVE;
    public bool CanManageWorkers =>
        Phase == ResortPhase.AWAITING_WAVE ||
        Phase == ResortPhase.WAVE_ACTIVE ||
        Phase == ResortPhase.UPGRADE;

    public bool CanUpgradeBuildings =>
        Phase == ResortPhase.AWAITING_WAVE ||
        Phase == ResortPhase.UPGRADE;

    public event Action<ResortPhase> PhaseChanged;

    public ResortSession(ResortData data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    internal string GetPurchaseBlockReason(BuildingDefinition definition, WorkerRoster roster)
    {
        BuildingData building = Data.GetBuilding(definition.Type);
        if (building == null || !building.availableToPurchase || building.owned)
            return "This building is unavailable.";
        if (!CanUpgradeBuildings) return "Purchase during preparation or upgrades.";
        int price = definition.GetCostCoins(PurchaseLevel);
        if (price < 0) return "This building has an invalid price.";
        if (Data.coins < price) return "Not enough Coins.";
        int addedSlots = definition.GetWorkerSlots(PurchaseLevel);
        if (roster.Workers.Count >= roster.GetCapWithAdditionalSlots(addedSlots))
            return "The bundled Worker would exceed the cap.";
        return null;
    }

    internal bool TryPurchaseBuilding(BuildingDefinition definition, WorkerRoster roster, out string error)
    {
        error = GetPurchaseBlockReason(definition, roster);
        if (error != null) return false;

        BuildingData building = Data.GetBuilding(definition.Type);
        int price = definition.GetCostCoins(PurchaseLevel);
        int workerId = 1;
        while (Data.GetWorker(workerId) != null) workerId++;

        Data.workers.Add(new WorkerData(workerId, building.type));
        Data.coins -= price;
        building.owned = true;
        building.availableToPurchase = false;
        building.level = PurchaseLevel;
        building.assignedWorkerId = workerId;
        roster.NotifyChanged();
        return true;
    }

    // Returns null when the selected owned building can be upgraded to the next level.
    internal string GetUpgradeBlockReason(BuildingDefinition definition)
    {
        if (definition == null) return "Building definition is missing.";

        BuildingData building = Data.GetBuilding(definition.Type);
        if (building == null || !building.owned)
            return "This building is not owned.";
        if (!CanUpgradeBuildings)
            return "Upgrade during preparation or upgrades.";
        if (building.level < PurchaseLevel || building.level > definition.MaxLevel)
            return "This building has an invalid level.";
        if (building.level >= definition.MaxLevel)
            return "This building is already at maximum level.";

        int nextLevel = building.level + 1;
        int cost = definition.GetCostCoins(nextLevel);
        if (cost < 0) return "This upgrade has an invalid price.";
        if (Data.coins < cost) return "Not enough Coins.";
        return null;
    }

    // Applies a single upgrade. Failure does not modify coins or building state.
    internal bool TryUpgradeBuilding(BuildingDefinition definition, out string error)
    {
        error = GetUpgradeBlockReason(definition);
        if (error != null) return false;

        BuildingData building = Data.GetBuilding(definition.Type);
        int nextLevel = building.level + 1;
        int cost = definition.GetCostCoins(nextLevel);
        Data.coins -= cost;
        building.level = nextLevel;
        return true;
    }

    public bool TryStartWave() => TryTransition(ResortPhase.AWAITING_WAVE, ResortPhase.WAVE_INCOMING);

    public bool TryBeginWave() => TryTransition(ResortPhase.WAVE_INCOMING, ResortPhase.WAVE_ACTIVE);

    public bool TryPauseWave() => TryTransition(ResortPhase.WAVE_ACTIVE, ResortPhase.WAVE_PAUSED);

    public bool TryResumeWave() => TryTransition(ResortPhase.WAVE_PAUSED, ResortPhase.WAVE_ACTIVE);

    public bool TryResolveWave() => TryTransition(ResortPhase.WAVE_ACTIVE, ResortPhase.WAVE_RESOLVING);

    public bool TryCompleteWave() => TryTransition(ResortPhase.WAVE_RESOLVING, ResortPhase.WAVE_COMPLETE);

    public bool TryEnterUpgrade() => TryTransition(ResortPhase.WAVE_COMPLETE, ResortPhase.UPGRADE);

    public bool TryPrepareNextWave()
    {
        if (Phase != ResortPhase.UPGRADE) return false;
        Data.nextWaveNumber++;
        return TryTransition(ResortPhase.UPGRADE, ResortPhase.AWAITING_WAVE);
    }

    internal void PrepareWaveForTesting(int waveNumber)
    {
        Data.nextWaveNumber = waveNumber;
        Data.phase = ResortPhase.AWAITING_WAVE;
        PhaseChanged?.Invoke(Phase);
    }

    private bool TryTransition(ResortPhase expected, ResortPhase next)
    {
        if (Phase != expected)
            return false;

        Data.phase = next;
        PhaseChanged?.Invoke(next);
        return true;
    }
}
