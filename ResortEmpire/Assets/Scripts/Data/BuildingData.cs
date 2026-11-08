using System;

// Mutable state, independent of Unity scene objects; JsonUtility-compatible fields.
[Serializable]
public sealed class BuildingData
{
    public BuildingType type;
    public bool owned;
    public bool availableToPurchase;
    public int level;
    public int assignedWorkerId; // -1 means no worker (prototype: max 1)

    public BuildingData(BuildingType type, bool owned, bool availableToPurchase, int level = 1)
    {
        this.type = type;
        this.owned = owned;
        this.availableToPurchase = availableToPurchase;
        this.level = level;
        assignedWorkerId = -1;
    }
}
