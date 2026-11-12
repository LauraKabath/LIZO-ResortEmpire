using System;
using UnityEngine;

/// Run from component context menu outside Play Mode.
public sealed class WorkerRosterSmokeTest : MonoBehaviour
{
    [ContextMenu("SCRUM-182/Run Worker Assignment Tests")]
    public void RunTests()
    {
        int passed = 0, failed = 0;
        BuildingDefinition definition = ScriptableObject.CreateInstance<BuildingDefinition>();
        try
        {
            ResortData resort = ResortData.CreatePrototypeStart(100, 5, 0);
            BuildingData reception = resort.GetBuilding(BuildingType.Reception);
            BuildingData restaurant = resort.GetBuilding(BuildingType.Restaurant);
            BuildingData beachBar = resort.GetBuilding(BuildingType.BeachBar);
            WorkerData first = resort.GetWorker(1);
            WorkerData second = resort.GetWorker(2);
            var roster = new WorkerRoster(resort, type => definition);
            int changes = 0;
            roster.Changed += () => changes++;

            Check("Two workers, none waiting", roster.Workers.Count == 2 &&
                roster.GetAvailableWorkers().Count == 0, ref passed, ref failed);
            Check("Occupied building has no free slot", roster.GetTotalSlots(BuildingType.Reception) == 1 &&
                roster.GetOccupiedSlots(BuildingType.Reception) == 1 &&
                roster.GetFreeSlots(BuildingType.Reception) == 0, ref passed, ref failed);
            Check("Empty building has one free slot", roster.GetOccupiedSlots(BuildingType.BeachBar) == 0 &&
                roster.GetFreeSlots(BuildingType.BeachBar) == 1, ref passed, ref failed);
            Check("Locked building has no slots", roster.GetTotalSlots(BuildingType.Pool) == 0 &&
                roster.GetFreeSlots(BuildingType.Pool) == 0, ref passed, ref failed);

            Check("Unassign frees the slot", roster.TryUnassign(2, out string error) && error == null &&
                !second.isAssigned && restaurant.assignedWorkerId == -1 &&
                roster.GetAvailableWorkers().Count == 1 && roster.GetFreeSlots(BuildingType.Restaurant) == 1,
                ref passed, ref failed);
            Check("Cannot unassign a waiting worker", !roster.TryUnassign(2, out error) && error != null,
                ref passed, ref failed);

            Check("Occupied slot rejects a waiting worker", !roster.TryAssign(2, BuildingType.Reception, out error) &&
                error != null && !second.isAssigned && reception.assignedWorkerId == 1,
                ref passed, ref failed);
            Check("Locked building rejects a waiting worker", !roster.TryAssign(2, BuildingType.Pool, out error) &&
                error != null && !second.isAssigned && resort.GetBuilding(BuildingType.Pool).assignedWorkerId == -1,
                ref passed, ref failed);
            Check("Unknown worker is rejected", !roster.TryAssign(99, BuildingType.BeachBar, out error) &&
                error != null && !roster.TryUnassign(99, out error) && error != null &&
                beachBar.assignedWorkerId == -1, ref passed, ref failed);

            Check("Waiting worker can be assigned", roster.TryAssign(2, BuildingType.BeachBar, out error) &&
                error == null && second.isAssigned && second.assignedBuilding == BuildingType.BeachBar &&
                beachBar.assignedWorkerId == 2 && roster.GetAvailableWorkers().Count == 0,
                ref passed, ref failed);

            // Worker 1 goes straight to the Restaurant, so it may hold only that one building.
            Check("Assigned worker moves without a duplicate", roster.TryAssign(1, BuildingType.Restaurant, out error) &&
                first.assignedBuilding == BuildingType.Restaurant && restaurant.assignedWorkerId == 1 &&
                reception.assignedWorkerId == -1, ref passed, ref failed);
            Check("Assigned worker cannot enter an occupied slot", !roster.TryAssign(1, BuildingType.BeachBar, out error) &&
                first.assignedBuilding == BuildingType.Restaurant && beachBar.assignedWorkerId == 2,
                ref passed, ref failed);
            Check("Changed fires only after successful operations", changes == 3, ref passed, ref failed);

            var service = new WaveBuildingService(restaurant, NeedType.Food, 3, 12, 0.33);
            double staffed = service.ProgressPerSecond;
            roster.TryUnassign(1, out error);
            double unstaffed = service.ProgressPerSecond;
            roster.TryAssign(1, BuildingType.Restaurant, out error);
            Check("Assignment changes the live service speed", unstaffed < staffed &&
                service.ProgressPerSecond == staffed, ref passed, ref failed);

            var session = new ResortSession(resort);
            bool finishedWave = session.TryStartWave() && session.TryBeginWave() && session.TryResolveWave() &&
                session.TryCompleteWave() && session.TryEnterUpgrade() && session.TryPrepareNextWave();
            Check("Assignments stay between waves", finishedWave && resort.nextWaveNumber == 2 &&
                restaurant.assignedWorkerId == 1 && beachBar.assignedWorkerId == 2 &&
                first.isAssigned && second.isAssigned, ref passed, ref failed);

            ResortData restored = JsonUtility.FromJson<ResortData>(JsonUtility.ToJson(resort));
            Check("JSON round-trip", restored.GetBuilding(BuildingType.BeachBar).assignedWorkerId == 2 &&
                restored.GetWorker(2).assignedBuilding == BuildingType.BeachBar,
                ref passed, ref failed);

            // SCRUM-184: a move is free and instant, and follows the phase rules from ResortSession.
            ResortData moves = ResortData.CreatePrototypeStart(100, 5, 0);
            var phases = new ResortSession(moves);
            var phaseRoster = new WorkerRoster(moves, type => definition, WorkerRoster.DefaultCapPercent,
                () => phases.CanManageWorkers);
            BuildingData source = moves.GetBuilding(BuildingType.Restaurant);
            BuildingData target = moves.GetBuilding(BuildingType.BeachBar);
            var sourceService = new WaveBuildingService(source, NeedType.Food, 3, 12, 0.33);
            var targetService = new WaveBuildingService(target, NeedType.Drinks, 3, 12, 0.33);
            double sourceBefore = sourceService.ProgressPerSecond, targetBefore = targetService.ProgressPerSecond;
            phases.TryStartWave();
            Check("Moves are blocked while a wave is incoming", !phaseRoster.TryAssign(2, BuildingType.BeachBar, out error) &&
                error != null && source.assignedWorkerId == 2, ref passed, ref failed);
            phases.TryBeginWave();
            Check("A move during an active wave is free and instant", phaseRoster.TryAssign(2, BuildingType.BeachBar, out error) &&
                moves.coins == 100 && source.assignedWorkerId == -1 && target.assignedWorkerId == 2 &&
                moves.GetWorker(2).assignedBuilding == BuildingType.BeachBar, ref passed, ref failed);
            Check("A move updates the speed of both buildings at once", sourceService.ProgressPerSecond < sourceBefore &&
                targetService.ProgressPerSecond > targetBefore, ref passed, ref failed);
            phases.TryPauseWave();
            Check("Moves are blocked while the wave is paused", !phaseRoster.TryAssign(2, BuildingType.Restaurant, out error) &&
                !phaseRoster.TryUnassign(2, out error) && target.assignedWorkerId == 2, ref passed, ref failed);
            phases.TryResumeWave();
            phases.TryResolveWave();
            phases.TryCompleteWave();
            Check("Moves are blocked on the wave result", !phaseRoster.CanChangeWorkers &&
                !phaseRoster.TryUnassign(2, out error) && target.assignedWorkerId == 2, ref passed, ref failed);
            phases.TryEnterUpgrade();
            Check("Moves are allowed again before the next wave", phaseRoster.TryAssign(2, BuildingType.Restaurant, out error) &&
                source.assignedWorkerId == 2 && target.assignedWorkerId == -1, ref passed, ref failed);

            // SCRUM-186: without a Worker a building serves more slowly, but never stops.
            BuildingDefinition[] configs = Resources.LoadAll<BuildingDefinition>("Buildings");
            Check("Every building has a coefficient between 0 and 1 for working without a Worker", configs.Length == 5 &&
                Array.TrueForAll(configs, config => config.UtilizationWithoutWorker > 0 && config.UtilizationWithoutWorker < 1),
                ref passed, ref failed);
            double withWorker = SecondsToServeSix(true), withoutWorker = SecondsToServeSix(false);
            Check("An unstaffed building finishes, at about a third of the speed", Math.Abs(withWorker - 24.5) < 0.01 &&
                Math.Abs(withoutWorker - (0.5 + 24 / 0.33)) < 0.01, ref passed, ref failed);
        }
        catch (Exception exception)
        {
            failed++;
            Debug.LogError("[SCRUM-182] Unexpected test exception: " + exception);
        }
        finally
        {
            if (Application.isPlaying) Destroy(definition); else DestroyImmediate(definition);
        }
        Debug.Log($"[SCRUM-182] FINAL: {passed} PASSED, {failed} FAILED");
    }

    // Six needs arrive within the first second; three slots at 12 s each serve them in two rounds.
    private static double SecondsToServeSix(bool staffed)
    {
        ResortData resort = ResortData.CreatePrototypeStart(100, 5, 0);
        BuildingData reception = resort.GetBuilding(BuildingType.Reception);
        if (!staffed) WorkerAllocation.TryUnassign(resort, reception.assignedWorkerId, out _);
        var wave = new WaveDefinition { waveNumber = 1, guestCount = 6, rooms = 6 };
        var simulation = new WaveSimulation(wave, 1,
            new[] { new WaveBuildingService(reception, NeedType.Rooms, 3, 12, 0.33) });
        for (int step = 0; step < 1000 && !simulation.Data.isComplete; step++) simulation.Tick(0.5);
        return simulation.Data.isComplete ? simulation.Data.elapsedSeconds : double.PositiveInfinity;
    }

    private static void Check(string name, bool ok, ref int passed, ref int failed)
    {
        if (ok) { passed++; Debug.Log("[PASS] " + name); }
        else { failed++; Debug.LogError("[FAIL] " + name); }
    }
}
