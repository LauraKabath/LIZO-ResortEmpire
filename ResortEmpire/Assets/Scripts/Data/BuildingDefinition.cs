using System;
using UnityEngine;

// Designer-editable static configuration. Store assets in Assets/Resources/Buildings if needed later.
[CreateAssetMenu(fileName = "BuildingDefinition", menuName = "Resort Empire/Building Definition")]
public sealed class BuildingDefinition : ScriptableObject
{
    [Serializable]
    public sealed class LevelConfig
    {
        [Min(1)] public int capacity = 3;
        [Min(1)] public int workerSlots = 1;
    }

    [SerializeField] private BuildingType type;
    [SerializeField] private string displayName;
    [SerializeField] private NeedType servicedNeed;
    [SerializeField, Min(0.01f)] private float serviceTimeSeconds = 12f;
    [SerializeField, Range(0.01f, 1f)] private float utilizationWithoutWorker = 0.33f;
    [SerializeField] private LevelConfig[] levels =
    {
        new LevelConfig { capacity = 3, workerSlots = 1 },
        new LevelConfig { capacity = 5, workerSlots = 1 },
        new LevelConfig { capacity = 7, workerSlots = 1 }
    };

    public BuildingType Type => type;
    public string DisplayName => displayName;
    public NeedType ServicedNeed => servicedNeed;
    public float ServiceTimeSeconds => serviceTimeSeconds;
    public float UtilizationWithoutWorker => utilizationWithoutWorker;
    public int MaxLevel => levels == null ? 0 : levels.Length;

    public int GetCapacity(int level) => GetLevel(level).capacity;
    public int GetWorkerSlots(int level) => GetLevel(level).workerSlots;

    private LevelConfig GetLevel(int level)
    {
        if (levels == null || level < 1 || level > levels.Length || levels[level - 1] == null)
            throw new ArgumentOutOfRangeException(nameof(level),
                $"Invalid level {level} for building {name}.");
        return levels[level - 1];
    }

    private void OnValidate()
    {
        if (levels == null) return;
        foreach (LevelConfig config in levels)
        {
            if (config == null) continue;
            config.capacity = Mathf.Max(1, config.capacity);
            // Prototype restriction: exactly one worker slot at every level.
            config.workerSlots = 1;
        }
    }
}
