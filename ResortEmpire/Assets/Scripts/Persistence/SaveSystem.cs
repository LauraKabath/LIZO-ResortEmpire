using System;
using UnityEngine;

// How the game scene should start. Set by the main menu, read once by GameController.
public static class GameLaunch
{
    public enum Mode
    {
        Default,  // The game scene was opened directly (e.g. Play in the Editor): continue if a save exists.
        NewGame,  // "Start New Game" in the menu: always start fresh and replace the save.
        Continue  // "Continue" in the menu: load the save.
    }

    private static Mode requested = Mode.Default;

    public static void Request(Mode mode) => requested = mode;

    public static Mode Consume()
    {
        Mode mode = requested;
        requested = Mode.Default;
        return mode;
    }
}

// The local save file: Application.persistentDataPath/resort_save.json, plus a backup of the
// previous save. Works offline; nothing is sent anywhere.
public static class SaveSystem
{
    public const string FileName = "resort_save.json";

    private static SaveRepository repository;

    private static SaveRepository Repository =>
        repository ??= new SaveRepository(
            new SaveFileStore(Application.persistentDataPath, FileName),
            json => JsonUtility.FromJson<GameSaveData>(json),
            data => JsonUtility.ToJson(data, true));

    public static string SavePath => Repository.Store.MainPath;
    public static string SaveFolder => Application.persistentDataPath;

    // True when a save or backup file exists, valid or not.
    public static bool HasSaveFile => Repository.Store.AnyFileExists;

    // Structure-only check for the main menu, which has no access to the game definitions.
    // Does not change any file.
    public static SaveLoadResult Peek() => Repository.Load(null, false);

    // Full load for the game scene. A damaged main save is moved aside as resort_save.corrupt.json.
    public static SaveLoadResult Load(SaveRules rules) => Repository.Load(rules, true);

    public static bool TrySave(GameSaveData data, out string error)
    {
        try
        {
            Repository.Save(data);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static void Delete()
    {
        try
        {
            Repository.Delete();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not delete the save: " + exception.Message);
        }
    }
}
