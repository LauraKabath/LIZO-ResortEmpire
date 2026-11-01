using UnityEngine;

/// Starting resources for a new game. No save/load or UI behavior
[CreateAssetMenu(fileName = "ResortInitialSettings", menuName = "Resort Empire/Initial Settings")]
public sealed class ResortInitialSettings : ScriptableObject
{
    [Header("Prototype resources (tuning values)")]
    [SerializeField, Min(0)] private int startingCoins = 100;
    [SerializeField, Min(0)] private int startingEnergy = 5;
    [SerializeField, Min(0)] private int startingResortValue = 0;

    [Header("MVP-ready resources")]
    [SerializeField, Min(0)] private int startingReputation = 0;
    [SerializeField, Min(0)] private int startingGems = 0;
    [SerializeField, Min(0)] private int startingMaterials = 0;

    public int StartingCoins => startingCoins;
    public int StartingEnergy => startingEnergy;
    public int StartingResortValue => startingResortValue;
    public int StartingReputation => startingReputation;
    public int StartingGems => startingGems;
    public int StartingMaterials => startingMaterials;

    // Used by automated tests; does not touch an asset on disk.
    public void SetForTesting(int coins, int energy, int value, int reputation,
        int gems, int materials)
    {
        startingCoins = coins;
        startingEnergy = energy;
        startingResortValue = value;
        startingReputation = reputation;
        startingGems = gems;
        startingMaterials = materials;
    }
}
