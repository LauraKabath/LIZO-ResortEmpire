using System;
using System.Collections.Generic;

// Game rules the validator needs from the project's definitions. Leave it null for a
// structure-only check (the main menu has no access to the definitions).
public sealed class SaveRules
{
    public Func<int, bool> WaveExists;
    public Func<BuildingType, int> MaxLevel;
    public Func<BuildingType, int, int> CapacityAtLevel;
    public Func<BuildingType, NeedType> ServicedNeed;
}

// Decides whether a loaded save can be trusted. Returns null when it can, otherwise the reason.
// Anything that fails here is treated as a damaged file and never reaches the game.
public static class SaveValidator
{
    public static string Validate(GameSaveData save, SaveRules rules = null)
    {
        try
        {
            return ValidateUnsafe(save, rules);
        }
        catch (Exception exception)
        {
            return "Unexpected save content: " + exception.Message;
        }
    }

    private static string ValidateUnsafe(GameSaveData save, SaveRules rules)
    {
        if (save == null) return "The file is empty or is not a save file.";
        if (save.saveVersion < 1) return "The file has no valid save version.";
        if (save.saveVersion > GameSaveData.CurrentVersion)
            return $"The save was made by a newer game version ({save.saveVersion}).";

        ResortData resort = save.resort;
        if (resort == null || resort.buildings == null || resort.workers == null)
            return "Resort data is missing.";
        if (!Enum.IsDefined(typeof(ResortPhase), resort.phase)) return "Unknown game phase.";
        if (resort.nextWaveNumber < 1) return "Invalid wave number.";
        if (rules?.WaveExists != null && !rules.WaveExists(resort.nextWaveNumber))
            return $"Wave {resort.nextWaveNumber} is not configured.";
        if (resort.coins < 0 || resort.energy < 0 || resort.resortValue < 0 ||
            resort.reputation < 0 || resort.gems < 0 || resort.materials < 0)
            return "Negative resources.";

        string error = ValidateBuildingsAndWorkers(resort, rules);
        if (error != null) return error;

        bool waveExpected = resort.phase == ResortPhase.WAVE_INCOMING || resort.phase == ResortPhase.WAVE_ACTIVE ||
                            resort.phase == ResortPhase.WAVE_PAUSED || resort.phase == ResortPhase.WAVE_RESOLVING ||
                            resort.phase == ResortPhase.WAVE_COMPLETE;
        if (waveExpected != save.hasWave)
            return waveExpected ? $"Phase {resort.phase} needs wave data." : $"Phase {resort.phase} cannot have wave data.";

        if (save.hasWave)
        {
            error = ValidateWave(save, resort, rules);
            if (error != null) return error;
        }

        if (save.hasPendingUpgradeSuggestion)
        {
            if (!Enum.IsDefined(typeof(BuildingType), save.pendingSuggestionBuilding) ||
                save.pendingSuggestionWave < 1 || !IsFinitePositive(save.pendingSuggestionSeconds))
                return "Invalid pending upgrade suggestion.";
        }

        return null;
    }

    private static string ValidateBuildingsAndWorkers(ResortData resort, SaveRules rules)
    {
        var seen = new HashSet<BuildingType>();
        foreach (BuildingData building in resort.buildings)
        {
            if (building == null || !Enum.IsDefined(typeof(BuildingType), building.type) || !seen.Add(building.type))
                return "Unknown or duplicate building.";
            if (building.level < 1) return $"{building.type} has an invalid level.";
            if (rules?.MaxLevel != null && building.level > rules.MaxLevel(building.type))
                return $"{building.type} is above its maximum level.";
            if (building.owned && building.availableToPurchase)
                return $"{building.type} is owned and for sale at the same time.";
            if (!building.owned && building.assignedWorkerId != -1)
                return $"{building.type} is not owned but has a Worker.";
        }
        if (seen.Count != Enum.GetValues(typeof(BuildingType)).Length) return "A building is missing.";

        var workerIds = new HashSet<int>();
        foreach (WorkerData worker in resort.workers)
        {
            if (worker == null || worker.id <= 0 || !workerIds.Add(worker.id))
                return "Invalid or duplicate Worker.";
            if (!worker.isAssigned) continue;
            BuildingData building = resort.GetBuilding(worker.assignedBuilding);
            if (building == null || !building.owned || building.assignedWorkerId != worker.id)
                return $"Worker {worker.id} does not match its building.";
        }

        foreach (BuildingData building in resort.buildings)
        {
            if (building.assignedWorkerId == -1) continue;
            WorkerData worker = resort.GetWorker(building.assignedWorkerId);
            if (worker == null || !worker.isAssigned || worker.assignedBuilding != building.type)
                return $"{building.type} points to a missing Worker.";
        }
        return null;
    }

    private static string ValidateWave(GameSaveData save, ResortData resort, SaveRules rules)
    {
        WaveData wave = save.wave;
        if (wave == null || wave.needs == null) return "Wave data is missing.";
        if (wave.waveNumber != resort.nextWaveNumber) return "Wave data belongs to another wave.";
        if (!IsFiniteNonNegative(wave.elapsedSeconds) || !IsFinitePositive(wave.arrivalDurationSeconds))
            return "Invalid wave timers.";
        if (wave.nextUnitId < 1) return "Invalid guest numbering.";
        if (resort.phase == ResortPhase.WAVE_RESOLVING && !wave.isComplete)
            return "A resolving wave must be complete.";
        if (resort.phase == ResortPhase.WAVE_COMPLETE && !wave.isComplete)
            return "A completed wave is not finished.";

        var needsSeen = new HashSet<NeedType>();
        var unitIds = new HashSet<int>();
        foreach (WaveNeedData need in wave.needs)
        {
            if (need == null || need.queuedUnits == null || need.inServiceUnits == null || need.completedUnits == null)
                return "Incomplete need data.";
            if (!Enum.IsDefined(typeof(NeedType), need.need) || !needsSeen.Add(need.need))
                return "Unknown or duplicate need.";
            BuildingData building = resort.GetBuilding(need.building);
            if (building == null || !building.owned) return $"{need.need} is served by a building that is not owned.";
            if (rules?.ServicedNeed != null && rules.ServicedNeed(need.building) != need.need)
                return $"{need.building} does not serve {need.need}.";
            if (rules?.CapacityAtLevel != null && need.capacity != rules.CapacityAtLevel(need.building, building.level))
                return $"{need.building} capacity does not match its level.";
            if (need.capacity < 1 || !IsFinitePositive(need.serviceTimeSeconds))
                return $"{need.need} has invalid service settings.";
            if (need.demandTotal < 0 || need.demandArrived < 0 || need.demandArrived > need.demandTotal ||
                need.rushUnits < 0 || need.rushUnits > need.demandTotal ||
                need.rushArrived < 0 || need.rushArrived > need.rushUnits ||
                need.RegularDemandArrived < 0 || need.RegularDemandArrived > need.RegularDemandTotal)
                return $"{need.need} has invalid guest counts.";
            if (need.demandArrived != need.queuedUnits.Count + need.inServiceUnits.Count + need.completedUnits.Count ||
                need.filled != need.completedUnits.Count)
                return $"{need.need} guest lists do not add up.";
            if (need.inServiceUnits.Count > need.capacity) return $"{need.need} has more guests in service than seats.";
            if (wave.isComplete && need.filled != need.demandTotal) return "A finished wave still has guests.";

            foreach (List<NeedUnit> list in new[] { need.queuedUnits, need.inServiceUnits, need.completedUnits })
            {
                foreach (NeedUnit unit in list)
                {
                    if (unit == null || unit.need != need.need || unit.building != need.building ||
                        unit.id < 1 || unit.id >= wave.nextUnitId || !unitIds.Add(unit.id))
                        return $"Invalid guest in {need.need}.";
                    if (!IsFiniteNonNegative(unit.arrivedAtSeconds) || unit.arrivedAtSeconds > wave.elapsedSeconds + 1e-6 ||
                        double.IsNaN(unit.serviceProgress) || unit.serviceProgress < 0 || unit.serviceProgress > 1 + 1e-6)
                        return $"Invalid guest timing in {need.need}.";
                }
            }
        }

        SuddenRushState rush = wave.suddenRush;
        if (rush != null && rush.planned)
        {
            if (rush.units < 0 || rush.arrived < 0 || rush.arrived > rush.units ||
                !IsFiniteNonNegative(rush.startSeconds) || !IsFinitePositive(rush.durationSeconds) ||
                !Enum.IsDefined(typeof(SuddenRushChoice), rush.choice))
                return "Invalid Sudden Rush data.";
            if (rush.started)
            {
                WaveNeedData target = wave.GetNeed(rush.need);
                if (target == null || target.building != rush.building || target.rushArrived != rush.arrived)
                    return "Sudden Rush does not match its need.";
            }
        }

        if (save.boosts != null)
        {
            var boosted = new HashSet<BuildingType>();
            foreach (BoostSaveData boost in save.boosts)
            {
                if (boost == null || !boosted.Add(boost.building) ||
                    wave.needs.Find(need => need.building == boost.building) == null)
                    return "Energy boost for a building outside the wave.";
                if (boost.active && (!boost.used || !IsFiniteNonNegative(boost.endsAtSeconds) ||
                                     !(boost.speedMultiplier > 1d) || double.IsInfinity(boost.speedMultiplier)))
                    return "Invalid Energy boost.";
            }
        }

        if (save.serviceLoads != null && save.serviceLoads.Count > 0)
        {
            if (save.serviceLoads.Count != wave.needs.Count) return "Bottleneck data does not match the wave.";
            for (int index = 0; index < wave.needs.Count; index++)
            {
                ServiceLoadSaveData load = save.serviceLoads[index];
                if (load == null || load.need != wave.needs[index].need || load.peakQueue < 0 ||
                    !IsFiniteNonNegative(load.secondsAsBottleneck) ||
                    !IsFiniteNonNegative(load.secondsMissingWorker) ||
                    !IsFiniteNonNegative(load.secondsInsufficientCapacity))
                    return "Invalid bottleneck data.";
            }
        }
        return null;
    }

    private static bool IsFiniteNonNegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
    private static bool IsFinitePositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
}
