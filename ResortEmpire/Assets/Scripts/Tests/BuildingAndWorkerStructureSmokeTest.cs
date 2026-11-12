using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class BuildingAndWorkerStructureSmokeTest : MonoBehaviour
{
    private void Start() => RunTests();

    [ContextMenu("Run Building and Worker Smoke Tests")]
    public void RunTests()
    {
        int passed = 0;
        int failed = 0;
        Run("Building levels and Coin prices", CheckBuildings);
        Run("Editable Worker cap on new games", CheckStartingRoster);
        Run("Worker cap across owned buildings and levels", CheckOwnedSlots);
        Run("Purchase gates, prices and bundled Workers", CheckBuildingPurchases);
        Run("Purchase respects the projected Worker cap", CheckPurchaseWorkerCap);
        Run("Wave catalog and reward configuration", CheckWaveRewards);

        Debug.Log($"[Building and Worker Structure] {passed} PASSED, {failed} FAILED", this);

        void Run(string name, Action test)
        {
            try
            {
                test();
                passed++;
                Debug.Log("[PASS] " + name, this);
            }
            catch (Exception exception)
            {
                failed++;
                Debug.LogError($"[FAIL] {name}: {exception.Message}", this);
            }
        }
    }

    private static Dictionary<BuildingType, BuildingDefinition> LoadBuildings() =>
        Resources.LoadAll<BuildingDefinition>("Buildings").ToDictionary(building => building.Type);

    private static void CheckBuildings()
    {
        var buildings = LoadBuildings();
        var purchasePrices = new Dictionary<BuildingType, int>
        {
            { BuildingType.Reception, 0 },
            { BuildingType.Restaurant, 0 },
            { BuildingType.BeachBar, 0 },
            { BuildingType.Pool, 1200 },
            { BuildingType.Bungalows, 2500 }
        };
        Expect(buildings.Count == purchasePrices.Count, "Expected five building definitions.");
        int[] capacities = { 3, 5, 7 };
        foreach (var entry in purchasePrices)
        {
            BuildingDefinition building = buildings[entry.Key];
            int[] costs = { entry.Value, 500, 1200 };
            Expect(building.MaxLevel == 3, $"{entry.Key} must have three levels.");
            for (int level = 1; level <= 3; level++)
                Expect(building.GetCostCoins(level) == costs[level - 1] &&
                    building.GetCapacity(level) == capacities[level - 1] && building.GetWorkerSlots(level) == 1,
                    $"{entry.Key} Lv{level} price, capacity, or Worker slots differ from the prototype defaults.");
        }
    }

    private static void CheckStartingRoster()
    {
        var settings = Resources.Load<ResortInitialSettings>("Configuration/InitialSettings");
        Expect(settings != null, "The InitialSettings asset is missing.");
        var data = ResortInitializer.CreateNewGame(settings);
        var buildings = LoadBuildings();
        var roster = new WorkerRoster(data, type => buildings[type], settings.WorkerCapPercent);
        int expected = 3 * settings.WorkerCapPercent / 100;
        Expect(roster.WorkerCap == expected && roster.Workers.Count == expected,
            "The starting roster must match the configured cap.");

        ResortData tuned = ResortData.CreatePrototypeStart(100, 5, 0, 60);
        ResortInitializer.ValidateNewGame(tuned, 60);
        Expect(tuned.workers.Count == 1, "A 60% cap on three starting slots must round down to one.");
    }

    private static void CheckOwnedSlots()
    {
        var buildings = LoadBuildings();
        var data = ResortData.CreatePrototypeStart(100, 5, 0);
        var roster = new WorkerRoster(data, type => buildings[type], 80);
        BuildingData pool = data.GetBuilding(BuildingType.Pool);
        Expect(roster.WorkerCap == 2, "Three owned slots should allow two Workers.");

        // Set up an owned-building fixture to test the cap independently of purchasing.
        pool.owned = true;
        for (int level = 1; level <= 3; level++)
        {
            pool.level = level;
            Expect(roster.WorkerCap == 3, $"Four owned slots at Pool Lv{level} should allow three Workers.");
        }
        data.GetBuilding(BuildingType.Bungalows).owned = true;
        Expect(roster.WorkerCap == 4, "Five owned slots should allow four Workers.");
    }

    private static void CheckWaveRewards()
    {
        var waveFile = Resources.Load<TextAsset>("Waves");
        Expect(waveFile != null, "The Waves JSON asset is missing.");
        var catalog = JsonUtility.FromJson<WaveCatalog>(waveFile.text);
        Expect(catalog != null && catalog.rewards != null, "The shared reward settings must load with the wave catalog.");
        catalog.Validate();
        for (int wave = 1; wave <= 8; wave++)
            Expect(catalog.GetWave(wave) != null, $"Missing Wave {wave}.");

        // Mutate the in-memory fixture to verify validation without editing the JSON asset.
        catalog.rewards.energyByStars = new int[4];
        try { catalog.Validate(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("A reward table with fewer than five Energy entries must be rejected.");
    }

    private static void CheckBuildingPurchases()
    {
        var buildings = LoadBuildings();
        BuildingDefinition poolDefinition = buildings[BuildingType.Pool];
        BuildingDefinition bungalowsDefinition = buildings[BuildingType.Bungalows];
        int poolPrice = poolDefinition.GetCostCoins(1);
        int bungalowsPrice = bungalowsDefinition.GetCostCoins(1);
        var data = ResortData.CreatePrototypeStart(poolPrice + bungalowsPrice, 5, 0);
        var session = new ResortSession(data);
        var roster = new WorkerRoster(data, type => buildings[type]);
        BuildingData pool = data.GetBuilding(BuildingType.Pool);
        BuildingData bungalows = data.GetBuilding(BuildingType.Bungalows);
        int rosterChanges = 0;
        roster.Changed += () => rosterChanges++;

        Expect(!session.TryPurchaseBuilding(poolDefinition, roster, out _), "A locked Pool must reject purchase.");
        pool.availableToPurchase = true;
        foreach (ResortPhase phase in Enum.GetValues(typeof(ResortPhase)))
        {
            if (phase == ResortPhase.AWAITING_WAVE || phase == ResortPhase.UPGRADE) continue;
            data.phase = phase;
            Expect(!session.TryPurchaseBuilding(poolDefinition, roster, out _), $"Purchase must be blocked during {phase}.");
        }
        Expect(data.coins == poolPrice + bungalowsPrice && !pool.owned && pool.availableToPurchase &&
            pool.assignedWorkerId == -1 && roster.Workers.Count == 2 && rosterChanges == 0,
            "Rejected purchases must preserve the wallet, plot, and roster.");

        data.phase = ResortPhase.AWAITING_WAVE;
        Expect(session.TryPurchaseBuilding(poolDefinition, roster, out string poolError), poolError);
        Expect(data.coins == bungalowsPrice && roster.Workers.Count == 3 && rosterChanges == 1,
            "Purchasing the Pool should charge its price and add one Worker.");
        CheckPurchasedBuilding(data, pool);
        Expect(!session.TryPurchaseBuilding(poolDefinition, roster, out _) &&
            data.coins == bungalowsPrice && roster.Workers.Count == 3 && rosterChanges == 1,
            "The same building cannot be bought twice.");

        data.phase = ResortPhase.UPGRADE;
        bungalows.availableToPurchase = true;
        data.coins = bungalowsPrice - 1;
        Expect(!session.TryPurchaseBuilding(bungalowsDefinition, roster, out _) && data.coins == bungalowsPrice - 1 &&
            !bungalows.owned && bungalows.availableToPurchase && roster.Workers.Count == 3 && rosterChanges == 1,
            "An insufficient balance must leave the purchase state intact.");
        data.coins = bungalowsPrice;
        Expect(session.TryPurchaseBuilding(bungalowsDefinition, roster, out string bungalowsError), bungalowsError);
        Expect(data.coins == 0 && roster.Workers.Count == 4 && rosterChanges == 2,
            "An exact balance should buy the Bungalows and add one Worker.");
        CheckPurchasedBuilding(data, bungalows);
    }

    private static void CheckPurchaseWorkerCap()
    {
        var buildings = LoadBuildings();
        var data = ResortData.CreatePrototypeStart(10000, 5, 0, 50);
        var session = new ResortSession(data);
        var roster = new WorkerRoster(data, type => buildings[type], 50);
        data.GetBuilding(BuildingType.Pool).availableToPurchase = true;
        BuildingData bungalows = data.GetBuilding(BuildingType.Bungalows);
        bungalows.availableToPurchase = true;

        Expect(session.TryPurchaseBuilding(buildings[BuildingType.Pool], roster, out string poolError), poolError);
        int remainingCoins = data.coins;
        Expect(!session.TryPurchaseBuilding(buildings[BuildingType.Bungalows], roster, out _) &&
            data.coins == remainingCoins && !bungalows.owned && bungalows.availableToPurchase &&
            roster.Workers.Count == 2 && roster.WorkerCap == 2,
            "Five slots at 50% allow two Workers, so a third bundled Worker must block the purchase.");
    }

    private static void CheckPurchasedBuilding(ResortData data, BuildingData building)
    {
        WorkerData worker = data.GetWorker(building.assignedWorkerId);
        Expect(building.owned && !building.availableToPurchase && building.level == 1 &&
            worker != null && worker.isAssigned && worker.assignedBuilding == building.type,
            "A purchased building must be owned at level 1 with its new Worker assigned.");
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
