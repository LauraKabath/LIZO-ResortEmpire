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

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
