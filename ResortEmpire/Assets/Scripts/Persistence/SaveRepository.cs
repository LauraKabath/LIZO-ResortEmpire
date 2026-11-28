using System;

public enum SaveLoadStatus
{
    Loaded,           // The main save was valid.
    LoadedFromBackup, // The main save was damaged; the previous save was used instead.
    NoSave,           // No save file exists.
    Unreadable        // Save files exist but none of them is valid.
}

public sealed class SaveLoadResult
{
    public SaveLoadStatus Status { get; }
    public GameSaveData Data { get; }
    public string Problem { get; }
    public bool HasData => Data != null;

    public SaveLoadResult(SaveLoadStatus status, GameSaveData data, string problem)
    {
        Status = status;
        Data = data;
        Problem = problem;
    }
}

// Loading and saving logic, independent of Unity: the JSON conversion is passed in.
public sealed class SaveRepository
{
    private readonly SaveFileStore store;
    private readonly Func<string, GameSaveData> fromJson;
    private readonly Func<GameSaveData, string> toJson;

    public SaveFileStore Store => store;

    public SaveRepository(SaveFileStore store, Func<string, GameSaveData> fromJson, Func<GameSaveData, string> toJson)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.fromJson = fromJson ?? throw new ArgumentNullException(nameof(fromJson));
        this.toJson = toJson ?? throw new ArgumentNullException(nameof(toJson));
    }

    // Tries the main save, then the backup. With quarantineDamaged, a damaged file is moved aside
    // so the next save cannot copy it over the good backup.
    public SaveLoadResult Load(SaveRules rules, bool quarantineDamaged)
    {
        string problem = null;
        bool first = true;
        foreach ((string path, string json) in store.ReadCandidates())
        {
            GameSaveData data = null;
            string error;
            try
            {
                data = string.IsNullOrWhiteSpace(json) ? null : fromJson(json);
                error = SaveValidator.Validate(data, rules);
            }
            catch (Exception exception)
            {
                error = "The file is not valid JSON: " + exception.Message;
            }

            bool isMain = path == store.MainPath;
            if (error == null)
                return new SaveLoadResult(isMain ? SaveLoadStatus.Loaded : SaveLoadStatus.LoadedFromBackup, data, problem);

            problem = (problem == null ? string.Empty : problem + " ") +
                      $"{(isMain ? "Save" : "Backup")}: {error}";
            if (quarantineDamaged && isMain) store.Quarantine(path);
            first = false;
        }

        return first && !store.AnyFileExists
            ? new SaveLoadResult(SaveLoadStatus.NoSave, null, null)
            : new SaveLoadResult(SaveLoadStatus.Unreadable, null, problem ?? "The save file could not be read.");
    }

    public void Save(GameSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        data.saveVersion = GameSaveData.CurrentVersion;
        string error = SaveValidator.Validate(data);
        if (error != null) throw new InvalidOperationException("Refusing to save an invalid game state: " + error);
        store.Write(toJson(data));
    }

    public void Delete() => store.DeleteAll();
}
