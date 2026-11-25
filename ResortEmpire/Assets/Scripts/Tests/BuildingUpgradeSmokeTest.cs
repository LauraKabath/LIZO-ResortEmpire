using System;
using System.Reflection;
using UnityEngine;

// Temporary smoke tests for SCRUM-195.
public sealed class BuildingUpgradeSmokeTest : MonoBehaviour
{
    [ContextMenu("SCRUM-195/Run Upgrade Smoke Tests")]
    public void Run()
    {
        BuildingDefinition definition = ScriptableObject.CreateInstance<BuildingDefinition>();
        try
        {
            SetField(definition, "type", BuildingType.Reception);
            SetField(definition, "levels", new[]
            {
                new BuildingDefinition.LevelConfig { capacity = 3, workerSlots = 1, costCoins = 0 },
                new BuildingDefinition.LevelConfig { capacity = 5, workerSlots = 1, costCoins = 500 },
                new BuildingDefinition.LevelConfig { capacity = 7, workerSlots = 1, costCoins = 1200 }
            });

            int passed = 0;
            int failed = 0;
            Check("Lv1 -> Lv2 costs 500 Coins", () =>
            {
                ResortData data = NewData(2000);
                ResortSession session = new ResortSession(data);
                bool upgraded = session.TryUpgradeBuilding(definition, out _);
                return upgraded && data.coins == 1500 && data.GetBuilding(BuildingType.Reception).level == 2;
            }, ref passed, ref failed);

            Check("Lv2 -> Lv3 costs 1200 Coins", () =>
            {
                ResortData data = NewData(2000);
                data.GetBuilding(BuildingType.Reception).level = 2;
                bool upgraded = new ResortSession(data).TryUpgradeBuilding(definition, out _);
                return upgraded && data.coins == 800 && data.GetBuilding(BuildingType.Reception).level == 3;
            }, ref passed, ref failed);

            Check("Maximum level blocks additional upgrades", () =>
            {
                ResortData data = NewData(2000);
                data.GetBuilding(BuildingType.Reception).level = 3;
                bool upgraded = new ResortSession(data).TryUpgradeBuilding(definition, out _);
                return !upgraded && data.coins == 2000 && data.GetBuilding(BuildingType.Reception).level == 3;
            }, ref passed, ref failed);

            Check("Not enough Coins does not modify state", () =>
            {
                ResortData data = NewData(499);
                bool upgraded = new ResortSession(data).TryUpgradeBuilding(definition, out _);
                return !upgraded && data.coins == 499 && data.GetBuilding(BuildingType.Reception).level == 1;
            }, ref passed, ref failed);

            Check("Cannot upgrade during active wave", () =>
            {
                ResortData data = NewData(2000);
                data.phase = ResortPhase.WAVE_ACTIVE;
                bool upgraded = new ResortSession(data).TryUpgradeBuilding(definition, out _);
                return !upgraded && data.coins == 2000 && data.GetBuilding(BuildingType.Reception).level == 1;
            }, ref passed, ref failed);

            Check("Unowned building cannot be upgraded", () =>
            {
                ResortData data = NewData(2000);
                data.GetBuilding(BuildingType.Reception).owned = false;
                bool upgraded = new ResortSession(data).TryUpgradeBuilding(definition, out _);
                return !upgraded && data.coins == 2000 && data.GetBuilding(BuildingType.Reception).level == 1;
            }, ref passed, ref failed);

            Debug.Log($"[SCRUM-195] FINAL: {passed} PASSED, {failed} FAILED", this);
        }
        finally
        {
            DestroyImmediate(definition);
        }
    }

    private static ResortData NewData(int coins)
    {
        ResortData data = new ResortData { coins = coins, phase = ResortPhase.AWAITING_WAVE };
        data.buildings.Add(new BuildingData(BuildingType.Reception, true, false, 1));
        return data;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new InvalidOperationException($"Missing test field: {name}");
        field.SetValue(target, value);
    }

    private void Check(string name, Func<bool> test, ref int passed, ref int failed)
    {
        try
        {
            if (test())
            {
                passed++;
                Debug.Log($"[SCRUM-195] [PASS] {name}", this);
            }
            else
            {
                failed++;
                Debug.LogError($"[SCRUM-195] [FAIL] {name}", this);
            }
        }
        catch (Exception ex)
        {
            failed++;
            Debug.LogError($"[SCRUM-195] [FAIL] {name}: {ex}", this);
        }
    }
}
