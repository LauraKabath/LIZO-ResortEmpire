using System;
using System.Collections.Generic;

// Runtime/save-game data only. ScriptableObject definitions are not embedded in JSON.
[Serializable]
public sealed class ResortData
{
    public int saveVersion = 1;
    public ResortPhase phase = ResortPhase.AWAITING_WAVE;
    public int nextWaveNumber = 1;
    public int coins;
    public int energy;
    public int resortValue;
    public int reputation; // MVP-ready
    public int gems;       // MVP-ready
    public int materials;  // MVP-ready
    public List<BuildingData> buildings = new List<BuildingData>();
    public List<WorkerData> workers = new List<WorkerData>();

    // Balancing values for coins/energy/value deliberately left to game initialization.
    public static ResortData CreatePrototypeStart(int coins, int energy, int resortValue)
    {
        var resort = new ResortData
        {
            coins = coins,
            energy = energy,
            resortValue = resortValue
        };
        resort.buildings.Add(new BuildingData(BuildingType.Reception, true, false));
        resort.buildings.Add(new BuildingData(BuildingType.Restaurant, true, false));
        resort.buildings.Add(new BuildingData(BuildingType.BeachBar, true, false));
        resort.buildings.Add(new BuildingData(BuildingType.Pool, false, false));
        resort.buildings.Add(new BuildingData(BuildingType.Bungalows, false, false));

        // Initial allocation is a provisional UI/gameplay choice and can be changed freely.
        resort.workers.Add(new WorkerData(1, BuildingType.Reception));
        resort.workers.Add(new WorkerData(2, BuildingType.Restaurant));
        resort.GetBuilding(BuildingType.Reception).assignedWorkerId = 1;
        resort.GetBuilding(BuildingType.Restaurant).assignedWorkerId = 2;
        return resort;
    }

    public BuildingData GetBuilding(BuildingType type)
    {
        return buildings.Find(building => building.type == type);
    }

    public WorkerData GetWorker(int id)
    {
        return workers.Find(worker => worker.id == id);
    }
}
