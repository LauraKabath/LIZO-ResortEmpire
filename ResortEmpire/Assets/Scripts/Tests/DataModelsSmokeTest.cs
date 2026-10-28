using UnityEngine;

public class DataModelsSmokeTest : MonoBehaviour
{
    [ContextMenu("Run SCRUM-17 Tests")]
    public void RunTests()
    {
        int passed = 0;
        int failed = 0;

        ResortData resort = ResortData.CreatePrototypeStart(100, 5, 0);

        Check("Initial game phase",
            resort.phase == ResortPhase.AWAITING_WAVE,
            ref passed, ref failed);

        Check("Three starting buildings",
            resort.GetBuilding(BuildingType.Reception).owned &&
            resort.GetBuilding(BuildingType.Restaurant).owned &&
            resort.GetBuilding(BuildingType.BeachBar).owned,
            ref passed, ref failed);

        Check("Locked buildings",
            !resort.GetBuilding(BuildingType.Pool).owned &&
            !resort.GetBuilding(BuildingType.Bungalows).owned,
            ref passed, ref failed);

        Check("Two starting workers",
            resort.workers.Count == 2,
            ref passed, ref failed);

        bool moved = WorkerAllocation.TryMove(
            resort, 2, BuildingType.BeachBar, out string error);

        Check("Worker moved successfully",
            moved && error == null,
            ref passed, ref failed);

        Check("Restaurant is now unstaffed",
            resort.GetBuilding(BuildingType.Restaurant)
                .assignedWorkerId == -1,
            ref passed, ref failed);

        Check("Beach Bar received worker",
            resort.GetBuilding(BuildingType.BeachBar)
                .assignedWorkerId == 2 &&
            resort.GetWorker(2).assignedBuilding ==
                BuildingType.BeachBar,
            ref passed, ref failed);

        bool occupiedMove = WorkerAllocation.TryMove(
            resort, 1, BuildingType.BeachBar, out _);

        Check("Occupied building rejects worker",
            !occupiedMove,
            ref passed, ref failed);

        bool lockedMove = WorkerAllocation.TryMove(
            resort, 1, BuildingType.Pool, out _);

        Check("Locked building rejects worker",
            !lockedMove,
            ref passed, ref failed);

        string json = JsonUtility.ToJson(resort);
        ResortData restored =
            JsonUtility.FromJson<ResortData>(json);

        Check("JSON round-trip",
            restored != null &&
            restored.coins == 100 &&
            restored.energy == 5 &&
            restored.buildings.Count == 5 &&
            restored.workers.Count == 2 &&
            restored.GetBuilding(BuildingType.BeachBar)
                .assignedWorkerId == 2 &&
            restored.GetWorker(2).assignedBuilding ==
                BuildingType.BeachBar,
            ref passed, ref failed);

        Debug.Log(
            $"SCRUM-17 Results: {passed} PASSED, {failed} FAILED");
    }

    private void Check(
        string name, bool condition,
        ref int passed, ref int failed)
    {
        if (condition)
        {
            passed++;
            Debug.Log($"[PASS] {name}");
        }
        else
        {
            failed++;
            Debug.LogError($"[FAIL] {name}");
        }
    }
}
