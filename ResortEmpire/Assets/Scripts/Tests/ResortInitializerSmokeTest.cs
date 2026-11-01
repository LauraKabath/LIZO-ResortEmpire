using System;
using UnityEngine;

/// Run from component context menu outside Play Mode.
public sealed class ResortInitializerSmokeTest : MonoBehaviour
{
    [ContextMenu("SCRUM-159/Run Initialization Tests")]
    private void RunTests()
    {
        int passed = 0, failed = 0;
        ResortInitialSettings settings = ScriptableObject.CreateInstance<ResortInitialSettings>();
        try
        {
            settings.SetForTesting(125, 6, 10, 3, 2, 4);
            ResortData first = ResortInitializer.CreateNewGame(settings);
            Check("Initial phase and Wave 1", first.phase == ResortPhase.AWAITING_WAVE &&
                first.nextWaveNumber == 1, ref passed, ref failed);
            Check("Three owned Lv1 buildings", first.GetBuilding(BuildingType.Reception).owned &&
                first.GetBuilding(BuildingType.Restaurant).owned &&
                first.GetBuilding(BuildingType.BeachBar).owned &&
                first.GetBuilding(BuildingType.Reception).level == 1 &&
                first.GetBuilding(BuildingType.Restaurant).level == 1 &&
                first.GetBuilding(BuildingType.BeachBar).level == 1, ref passed, ref failed);
            Check("Pool and Bungalows locked", !first.GetBuilding(BuildingType.Pool).owned &&
                !first.GetBuilding(BuildingType.Bungalows).owned, ref passed, ref failed);
            Check("Two valid workers", first.workers.Count == 2 &&
                first.GetBuilding(BuildingType.Reception).assignedWorkerId == 1 &&
                first.GetBuilding(BuildingType.Restaurant).assignedWorkerId == 2,
                ref passed, ref failed);
            Check("All configured resource values", first.coins == 125 && first.energy == 6 &&
                first.resortValue == 10 && first.reputation == 3 && first.gems == 2 &&
                first.materials == 4, ref passed, ref failed);

            ResortData second = ResortInitializer.CreateNewGame(settings);
            first.coins = 0;
            first.GetBuilding(BuildingType.Reception).level = 2;
            Check("Independent initialization", !ReferenceEquals(first, second) &&
                second.coins == 125 && second.GetBuilding(BuildingType.Reception).level == 1,
                ref passed, ref failed);

            ResortData restored = JsonUtility.FromJson<ResortData>(JsonUtility.ToJson(second));
            ResortInitializer.ValidateNewGame(restored);
            Check("JSON round-trip", restored.coins == 125 && restored.workers.Count == 2 &&
                restored.buildings.Count == 5 && restored.materials == 4,
                ref passed, ref failed);

            ResortData duplicate = ResortInitializer.CreateNewGame(settings);
            duplicate.buildings.Add(new BuildingData(BuildingType.Reception, true, false));
            Check("Reject invalid building configuration", Throws(() =>
                ResortInitializer.ValidateNewGame(duplicate)), ref passed, ref failed);

            settings.SetForTesting(-1, 6, 10, 3, 2, 4);
            Check("Reject negative resources", Throws(() =>
                ResortInitializer.CreateNewGame(settings)), ref passed, ref failed);

            var session = new ResortSession(second);
            Check("Compatible with ResortSession", session.CanStartWave &&
                session.Phase == ResortPhase.AWAITING_WAVE,
                ref passed, ref failed);
        }
        catch (Exception exception)
        {
            failed++;
            Debug.LogError("[SCRUM-159] Unexpected test exception: " + exception);
        }
        finally
        {
            if (Application.isPlaying) Destroy(settings); else DestroyImmediate(settings);
        }
        Debug.Log($"[SCRUM-159] FINAL: {passed} PASSED, {failed} FAILED");
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; }
        catch (ArgumentException) { return true; }
        catch (InvalidOperationException) { return true; }
    }

    private static void Check(string name, bool ok, ref int passed, ref int failed)
    {
        if (ok) { passed++; Debug.Log("[PASS] " + name); }
        else { failed++; Debug.LogError("[FAIL] " + name); }
    }
}
