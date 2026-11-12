using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// SCRUM-67: purchase rules on in-memory resort data. Run from the component context menu.
public sealed class BuildingPurchaseSmokeTest : MonoBehaviour
{
    private int passed, failed;
    private Dictionary<BuildingType, BuildingDefinition> definitions;

    [ContextMenu("Run Scrum-67 Test")]
    public void RunTests()
    {
        passed = 0;
        failed = 0;
        try
        {
            definitions = Resources.LoadAll<BuildingDefinition>("Buildings")
                .ToDictionary(definition => definition.Type);

            CheckUnlocking(BuildingType.Pool, NeedType.Relax, requiredWave: 3, price: 1200);
            CheckUnlocking(BuildingType.Bungalows, NeedType.OceanView, requiredWave: 6, price: 2500);
            CheckPurchasePhases(BuildingType.Pool);
            CheckPurchasePhases(BuildingType.Bungalows);
            CheckRepeatPurchase(BuildingType.Pool);
            CheckRepeatPurchase(BuildingType.Bungalows);
            CheckInsufficientCoins();
            CheckWorkerCap();
        }
        catch (Exception exception)
        {
            failed++;
            Debug.LogError("[SCRUM-67] Unexpected test exception: " + exception, this);
        }
        Debug.Log($"[SCRUM-67] FINAL: {passed} PASSED, {failed} FAILED", this);
    }

    private void CheckUnlocking(BuildingType type, NeedType need, int requiredWave, int price)
    {
        var run = new Scenario(definitions, type);
        Check($"{type}: configured unlock wave, need, and price",
            run.Definition.RequiredCompletedWave == requiredWave && run.Definition.ServicedNeed == need &&
            run.Price == price, run);
        CheckRejected($"{type}: a locked plot cannot be bought", run);

        // Set the unlocked state on the fixture, as wave completion would.
        run.Plot.availableToPurchase = true;
        Check($"{type}: an unlocked plot can be bought and remains unowned until purchase",
            run.Session.GetPurchaseBlockReason(run.Definition, run.Roster) == null &&
            !run.Plot.owned && run.Plot.assignedWorkerId == -1 &&
            run.Roster.GetTotalSlots(type) == 0 && run.Roster.Workers.Count == 2 &&
            run.Resort.coins == run.Price, run);

        var restored = new Scenario(definitions, type, RoundTrip(run.Resort));
        Check($"{type}: an unlocked plot stays unowned after loading",
            restored.Plot.availableToPurchase && !restored.Plot.owned && restored.Plot.assignedWorkerId == -1,
            restored);
    }

    private void CheckPurchasePhases(BuildingType type)
    {
        foreach (ResortPhase phase in Enum.GetValues(typeof(ResortPhase)))
        {
            // Use a fresh, unlocked plot with an exact balance so only the phase determines eligibility.
            var run = new Scenario(definitions, type);
            run.Plot.availableToPurchase = true;
            run.Resort.phase = phase;
            if (phase == ResortPhase.AWAITING_WAVE || phase == ResortPhase.UPGRADE)
            {
                bool eligible = run.Session.GetPurchaseBlockReason(run.Definition, run.Roster) == null;
                bool purchased = run.Session.TryPurchaseBuilding(run.Definition, run.Roster, out string error);
                Check($"{type}: purchase is allowed during {phase}",
                    eligible && purchased && error == null && run.Plot.owned &&
                    run.Resort.coins == 0 && run.Resort.phase == phase, run);
            }
            else
            {
                CheckRejected($"{type}: purchase is blocked during {phase}", run);
            }
        }
    }

    private void CheckRepeatPurchase(BuildingType type)
    {
        var run = new Scenario(definitions, type);
        run.Plot.availableToPurchase = true;
        run.Resort.coins = run.Price * 2;
        bool purchased = run.Session.TryPurchaseBuilding(run.Definition, run.Roster, out string error);
        WorkerData worker = run.Resort.GetWorker(run.Plot.assignedWorkerId);
        Check($"{type}: purchase charges once, grants Level 1 ownership, and assigns one Worker",
            purchased && error == null && run.Resort.coins == run.Price && run.Plot.owned &&
            !run.Plot.availableToPurchase && run.Plot.level == 1 && run.Roster.Workers.Count == 3 &&
            run.Roster.WorkerCap == 3 && worker != null && worker.isAssigned && worker.assignedBuilding == type, run);
        CheckRejected($"{type}: a purchased plot cannot be bought again, even with enough Coins", run);

        var restored = new Scenario(definitions, type, RoundTrip(run.Resort));
        WorkerData restoredWorker = restored.Resort.GetWorker(restored.Plot.assignedWorkerId);
        Check($"{type}: the purchase, balance, and Worker assignment survive loading",
            restored.Resort.coins == run.Price && restored.Resort.energy == 5 && restored.Resort.resortValue == 10 &&
            restored.Plot.owned && !restored.Plot.availableToPurchase && restored.Plot.level == 1 &&
            restored.Roster.Workers.Count == 3 && restored.Plot.assignedWorkerId == run.Plot.assignedWorkerId &&
            restoredWorker != null && restoredWorker.isAssigned && restoredWorker.assignedBuilding == type, restored);
        CheckRejected($"{type}: a purchased plot cannot be bought again after loading", restored);

        restored.Plot.availableToPurchase = true;
        CheckRejected($"{type}: ownership blocks buying even if availability is set again", restored);
    }

    private void CheckInsufficientCoins()
    {
        var run = new Scenario(definitions, BuildingType.Pool);
        run.Plot.availableToPurchase = true;
        run.Resort.coins = run.Price - 1;
        CheckRejected("Insufficient Coins block an unlocked Pool purchase", run);
    }

    private void CheckWorkerCap()
    {
        // Four owned slots at 20% cannot support the bundled Worker.
        var run = new Scenario(definitions, BuildingType.Pool, capPercent: 20);
        run.Plot.availableToPurchase = true;
        CheckRejected("The projected Worker cap blocks an otherwise eligible Pool purchase", run);
    }

    private void CheckRejected(string name, Scenario run)
    {
        string before = JsonUtility.ToJson(run.Resort);
        string reason = run.Session.GetPurchaseBlockReason(run.Definition, run.Roster);
        bool purchased = run.Session.TryPurchaseBuilding(run.Definition, run.Roster, out string error);
        Check(name, !string.IsNullOrEmpty(reason) && !purchased && error == reason &&
            JsonUtility.ToJson(run.Resort) == before, run);
    }

    private void Check(string name, bool ok, Scenario run)
    {
        if (ok)
        {
            passed++;
            Debug.Log("[PASS] " + name, this);
        }
        else
        {
            failed++;
            Debug.LogError($"[FAIL] {name} | Phase: {run.Resort.phase}, Coins: {run.Resort.coins}, " +
                $"owned: {run.Plot.owned}, available: {run.Plot.availableToPurchase}, " +
                $"Workers: {run.Roster.Workers.Count}/{run.Roster.WorkerCap}", this);
        }
    }

    private static ResortData RoundTrip(ResortData data) => JsonUtility.FromJson<ResortData>(JsonUtility.ToJson(data));

    private sealed class Scenario
    {
        public readonly BuildingDefinition Definition;
        public readonly ResortData Resort;
        public readonly ResortSession Session;
        public readonly WorkerRoster Roster;

        public int Price => Definition.GetCostCoins(1);
        public BuildingData Plot => Resort.GetBuilding(Definition.Type);

        public Scenario(Dictionary<BuildingType, BuildingDefinition> definitions, BuildingType type,
            ResortData data = null, int capPercent = WorkerRoster.DefaultCapPercent)
        {
            Definition = definitions[type];
            Resort = data ?? ResortData.CreatePrototypeStart(Price, 5, 10, capPercent);
            Session = new ResortSession(Resort);
            Roster = new WorkerRoster(Resort, building => definitions[building], capPercent);
        }
    }
}
