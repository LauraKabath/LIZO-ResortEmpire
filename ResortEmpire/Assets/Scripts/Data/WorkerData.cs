using System;

[Serializable]
public sealed class WorkerData
{
    public int id;
    public bool isAssigned;
    public BuildingType assignedBuilding;

    public WorkerData(int id, BuildingType building)
    {
        this.id = id;
        isAssigned = true;
        assignedBuilding = building;
    }
}
