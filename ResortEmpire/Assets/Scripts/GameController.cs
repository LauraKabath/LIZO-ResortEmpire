using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Scene entry point. Add this only to scenes that should run a resort session.
// Runs before other scripts, so a loaded game is in place before any panel reads it in Start.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class GameController : MonoBehaviour
{
    [Header("Building definitions")] [SerializeField]
    private BuildingDefinition[] buildingDefinitions;

    [Header("New game initialization")] [SerializeField]
    private ResortInitialSettings initialSettings;

    [Header("Wave settings")] [SerializeField]
    private TextAsset waveDefinitionsFile;

    [SerializeField, Range(45f, 180f)] private float waveDurationSeconds = 90f;

    [Header("Prototype starting resources")] [SerializeField, Min(0)]
    private int startingCoins = 100;

    [SerializeField, Min(0)] private int startingEnergy = 5;
    [SerializeField, Min(0)] private int startingResortValue;

    [Header("Debugging")] [SerializeField] private bool logPhaseChanges = true;
    [SerializeField] private bool logGeneratedNeeds = true;
    [SerializeField] private bool logServiceLoad = true;

    [Header("Runtime data (created on Play)")] [SerializeField]
    private ResortData resortData;

    [SerializeField] private WaveData activeWaveData;

    [SerializeField] private Button startWaveButton;

    [Header("Wave evaluation")] [SerializeField]
    private WaveEvaluationSettings evaluationSettings = new WaveEvaluationSettings();

    [Header("Resort Value")] [SerializeField, Min(0)]
    private int resortValuePerStar = 10;

    [SerializeField, Range(0f, 100f)] private float resortValueInvestmentPercent = 10f;

    [Header("Upgrade suggestions")] [SerializeField, Min(0f)]
    private float minimumCapacityBottleneckSeconds = 1f;

    [Header("Saving")]
    [Tooltip("Only when this scene is played directly (not from the main menu): start a fresh game " +
             "instead of continuing the save. The fresh game then replaces the save.")]
    [SerializeField] private bool newGameWhenPlayedDirectly;

    [Tooltip("While a wave runs, also save every this many seconds (0 = off). Going to the " +
             "background, quitting and every game action always save.")]
    [SerializeField, Min(0f)] private float autosaveIntervalSeconds = 10f;

    [Tooltip("Shown while a wave is paused. Loaded from Resources/UI/PausedOverlay when empty.")]
    [SerializeField] private GameObject pausedOverlayPrefab;

    [SerializeField] private bool logSaves = true;

    [Header("Energy boost")]
    [SerializeField, Min(1)] private int energyBoostCost = 3;
    [SerializeField, Min(0.1f)] private float energyBoostDurationSeconds = 15f;
    [SerializeField, Min(1.01f)] private float energyBoostSpeedMultiplier = 2f;

    public WaveEvaluationResult LastWaveEvaluation { get; private set; }
    public WaveRewardResult LastWaveReward { get; private set; }
    public BuildingType? LastWaveBottleneck { get; private set; }
    public double LastWaveBottleneckSeconds { get; private set; }

    public bool HasUnclaimedRewards => Phase == ResortPhase.WAVE_COMPLETE &&
                                       LastWaveReward != null && !rewardClaimed;

    private bool rewardClaimed;

    private readonly Dictionary<BuildingType, BuildingDefinition> definitions =
        new Dictionary<BuildingType, BuildingDefinition>();

    private ResortSession session;
    private WaveCatalog waveCatalog;
    private WaveSimulation waveSimulation;
    private bool upgradeSuggestionOpen;

    // Save state.
    private bool upgradeSuggestionPending; // Shown after a wave and not answered yet.
    private bool saveRequested;
    private string saveReason;
    private float nextAutosaveTime;

    // True when this session continued a save instead of starting a new game.
    public bool StartedFromSave { get; private set; }

    public bool IsInitialized => session != null;
    public ResortData Data => resortData;
    public WaveData ActiveWave => activeWaveData;
    public WorkerRoster Workers { get; private set; }
    public ServiceLoadMonitor ServiceLoad { get; private set; }
    public UpgradeSuggestion LastUpgradeSuggestion { get; private set; }
    public ResortPhase Phase => session?.Phase ?? ResortPhase.AWAITING_WAVE;

    public bool CanStartWave => IsInitialized && session.CanStartWave &&
                                waveCatalog.GetWave(Data.nextWaveNumber) != null;

    public bool CanManageWorkers => IsInitialized && session.CanManageWorkers;

    public bool CanUpgradeBuildings => IsInitialized && session.CanUpgradeBuildings;

    // Public interface for SCRUM-206 (Energy boost button UI).
    public int EnergyBoostCost => energyBoostCost;
    public float EnergyBoostDurationSeconds => energyBoostDurationSeconds;

    public bool HasUsedEnergyBoost(BuildingType type) =>
        waveSimulation != null && waveSimulation.HasUsedEnergyBoost(type);

    public bool IsEnergyBoostActive(BuildingType type) =>
        waveSimulation != null && waveSimulation.IsEnergyBoostActive(type);

    public double GetEnergyBoostRemainingSeconds(BuildingType type) =>
        waveSimulation != null ? waveSimulation.GetEnergyBoostRemainingSeconds(type) : 0d;

    public string GetEnergyBoostBlockReason(BuildingType type)
    {
        if (!IsInitialized || Phase != ResortPhase.WAVE_ACTIVE || waveSimulation == null)
            return "Energy boost is only available during an active wave.";
        if (waveSimulation.Data.isComplete)
            return "The wave is already complete.";
        if (energyBoostCost < 1 || energyBoostDurationSeconds <= 0f ||
            energyBoostSpeedMultiplier <= 1f)
            return "Invalid Energy boost configuration.";
        BuildingData building = Data.GetBuilding(type);
        if (building == null || !building.owned)
            return "This building is not owned.";
        if (waveSimulation.HasUsedEnergyBoost(type))
            return "Energy boost already used on this building this wave.";
        if (Data.energy < energyBoostCost)
            return $"Missing: {energyBoostCost - Data.energy} Energy";
        return null;
    }

    public bool TryActivateEnergyBoost(BuildingType type, out string error)
    {
        if (!EnsureReady())
        {
            error = "The game is not initialized.";
            return false;
        }

        error = GetEnergyBoostBlockReason(type);
        if (error != null)
            return false;

        if (!waveSimulation.TryActivateEnergyBoost(type, energyBoostDurationSeconds,
                energyBoostSpeedMultiplier, out error))
            return false;

        Data.energy -= energyBoostCost;
        RequestSave($"{type} boosted");
        Debug.Log($"[SCRUM-205] Boosted {type} for {energyBoostDurationSeconds:F1}s " +
                  $"at x{energyBoostSpeedMultiplier:F2} speed. Cost: {energyBoostCost} Energy.", this);
        return true;
    }

    // ---------- Sudden Rush ----------
    // The rush state lives in the active WaveData, so a saved wave restores an unanswered prompt.

    public SuddenRushState SuddenRush => activeWaveData?.suddenRush;

    // Shown from the moment the rush starts until the player answers or the wave ends.
    public bool IsSuddenRushPromptOpen =>
        IsInitialized && waveSimulation != null && !activeWaveData.isComplete &&
        (Phase == ResortPhase.WAVE_ACTIVE || Phase == ResortPhase.WAVE_PAUSED) &&
        activeWaveData.suddenRush != null && activeWaveData.suddenRush.IsPromptPending;

    // While the wave is paused the prompt stays visible, but the player is not in the game.
    public bool CanAnswerSuddenRush => IsSuddenRushPromptOpen && Phase == ResortPhase.WAVE_ACTIVE;

    public event Action<SuddenRushState> SuddenRushStarted;
    public event Action<SuddenRushState> SuddenRushAnswered;

    // Which building the coming rush would hit right now (the unstaffed one). Only a prediction
    // until the rush starts, because the player can still move Workers.
    public bool TryGetLikelySuddenRushBuilding(out BuildingType building)
    {
        building = default;
        return waveSimulation != null && activeWaveData != null &&
               !activeWaveData.isComplete && waveSimulation.TryGetSuddenRushTarget(out building);
    }

    // Null when "Use Energy to serve faster" would succeed.
    public string GetSuddenRushEnergyBlockReason()
    {
        if (!CanAnswerSuddenRush) return "The Sudden Rush prompt is not active.";
        return GetEnergyBoostBlockReason(SuddenRush.building);
    }

    // "Use Energy to serve faster": the normal Speed Up on the rushed building.
    // Nothing is spent and the prompt stays open when the boost is not possible.
    public bool TryUseEnergyForSuddenRush(out string error)
    {
        error = GetSuddenRushEnergyBlockReason();
        if (error != null) return false;
        if (!TryActivateEnergyBoost(SuddenRush.building, out error)) return false;
        AnswerSuddenRush(SuddenRushChoice.USE_ENERGY);
        return true;
    }

    public bool TryIgnoreSuddenRush(out string error)
    {
        if (!CanAnswerSuddenRush)
        {
            error = "The Sudden Rush prompt is not active.";
            return false;
        }

        error = null;
        AnswerSuddenRush(SuddenRushChoice.IGNORE);
        return true;
    }

    // "Move Worker here" only opens the Worker panel; the prompt is answered once a Worker
    // actually stands on the rushed building, however it got there.
    private void CheckSuddenRushStaffed()
    {
        if (!CanAnswerSuddenRush) return;
        BuildingData building = Data.GetBuilding(SuddenRush.building);
        if (building != null && building.assignedWorkerId != -1)
            AnswerSuddenRush(SuddenRushChoice.MOVE_WORKER);
    }

    private void AnswerSuddenRush(SuddenRushChoice choice)
    {
        if (waveSimulation == null || !waveSimulation.TryAnswerSuddenRush(choice)) return;
        Debug.Log($"[Sudden Rush] Wave {activeWaveData.waveNumber}: player chose {choice} " +
                  $"at {activeWaveData.elapsedSeconds:F1}s for {SuddenRush.building}.", this);
        SuddenRushAnswered?.Invoke(SuddenRush);
        RequestSave("Sudden Rush answered");
    }

    private void HandleSuddenRushStarted(SuddenRushState rush)
    {
        Debug.Log($"[Sudden Rush] Wave {activeWaveData.waveNumber} @ {activeWaveData.elapsedSeconds:F1}s: " +
                  $"+{rush.units} {rush.need} over {rush.durationSeconds:F0}s at {rush.building}.", this);
        SuddenRushStarted?.Invoke(rush);
        // Saved right away, so quitting before answering brings the prompt back.
        RequestSave("Sudden Rush started");
    }

    private void HandleSuddenRushSkipped(SuddenRushState rush)
    {
        Debug.LogWarning($"[Sudden Rush] Wave {activeWaveData.waveNumber}: skipped, " +
                         "every building serving this wave has a Worker.", this);
    }

    // ---------- Saving and loading ----------
    // The save lives in Application.persistentDataPath (see SaveSystem). The game saves after every
    // action, when a wave starts, pauses or ends, periodically during a wave, and when the app goes
    // to the background or quits. Going to the background pauses a running wave first.

    private ResortData CreateNewGameData() =>
        initialSettings != null
            ? ResortInitializer.CreateNewGame(initialSettings)
            : ResortData.CreatePrototypeStart(startingCoins, startingEnergy, startingResortValue);

    private SaveRules CreateSaveRules() => new SaveRules
    {
        WaveExists = number => waveCatalog.GetWave(number) != null,
        MaxLevel = type => GetBuildingDefinition(type).MaxLevel,
        CapacityAtLevel = (type, level) => GetBuildingDefinition(type).GetCapacity(level),
        ServicedNeed = type => GetBuildingDefinition(type).ServicedNeed
    };

    // Null means "start a new game".
    private GameSaveData ChooseStartingSave()
    {
        GameLaunch.Mode mode = GameLaunch.Consume();
        if (mode == GameLaunch.Mode.NewGame)
        {
            SaveSystem.Delete();
            Debug.Log("[Save] New game started from the menu; it replaces the previous save.", this);
            return null;
        }

        if (mode == GameLaunch.Mode.Default && newGameWhenPlayedDirectly)
        {
            Debug.Log("[Save] 'New Game When Played Directly' is on: starting a new game.", this);
            return null;
        }

        SaveLoadResult result = SaveSystem.Load(CreateSaveRules());
        switch (result.Status)
        {
            case SaveLoadStatus.Loaded:
                Debug.Log($"[Save] Continuing the game saved at {result.Data.savedAtUtc} (UTC) " +
                          $"from {SaveSystem.SavePath}.", this);
                return result.Data;
            case SaveLoadStatus.LoadedFromBackup:
                Debug.LogWarning($"[Save] The save was damaged ({result.Problem}); continuing from the " +
                                 "previous save instead.", this);
                return result.Data;
            case SaveLoadStatus.NoSave:
                Debug.Log("[Save] No save found: starting a new game.", this);
                return null;
            default:
                Debug.LogError($"[Save] The save could not be read ({result.Problem}). Starting a new game; " +
                               "the damaged file was kept as resort_save.corrupt.json.", this);
                return null;
        }
    }

    private bool TryRebuildSavedWave(GameSaveData save, out WaveSimulation simulation, out ServiceLoadMonitor loads)
    {
        try
        {
            simulation = WaveSimulation.Restore(save.wave, CreateBuildingServices(save.resort), save.boosts);
            loads = new ServiceLoadMonitor(save.wave, save.resort, save.serviceLoads);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Save] The saved wave could not be restored ({exception.Message}). " +
                           "Starting a new game.", this);
            simulation = null;
            loads = null;
            return false;
        }
    }

    // Runs before any phase event, so every panel starts from the restored state.
    private void RestoreSavedState(GameSaveData save, WaveSimulation wave, ServiceLoadMonitor loads)
    {
        rewardClaimed = save.rewardClaimed;
        if (wave != null)
            AttachWave(wave, loads);

        switch (resortData.phase)
        {
            // A running wave always comes back paused: nothing happened while the player was away.
            case ResortPhase.WAVE_INCOMING:
            case ResortPhase.WAVE_ACTIVE:
                // A wave that had just finished is wrapped up on the first frame instead.
                resortData.phase = activeWaveData.isComplete ? ResortPhase.WAVE_ACTIVE : ResortPhase.WAVE_PAUSED;
                break;
            case ResortPhase.WAVE_RESOLVING:
                resortData.phase = ResortPhase.WAVE_ACTIVE;
                break;
            case ResortPhase.WAVE_COMPLETE:
                EvaluateLongestBottleneck();
                EvaluateUpgradeSuggestion();
                CalculateWaveResults(activeWaveData);
                break;
            case ResortPhase.UPGRADE:
                if (waveCatalog.GetWave(resortData.nextWaveNumber + 1) != null)
                    resortData.nextWaveNumber++;
                resortData.phase = ResortPhase.AWAITING_WAVE;
                break;
        }

        if (resortData.phase == ResortPhase.AWAITING_WAVE && save.hasPendingUpgradeSuggestion)
        {
            LastUpgradeSuggestion = new UpgradeSuggestion(save.pendingSuggestionWave,
                save.pendingSuggestionBuilding, save.pendingSuggestionSeconds);
            upgradeSuggestionPending = true;
        }

        Debug.Log($"[Save] Restored wave {resortData.nextWaveNumber}, phase {resortData.phase}, " +
                  $"{resortData.coins} Coins, {resortData.energy} Energy.", this);
    }

    // Windows that are normally opened by an action the player already took before saving.
    private void AnnounceRestoredState()
    {
        if (Phase == ResortPhase.WAVE_COMPLETE && rewardClaimed && IsFinalWave)
            FinalWaveRewardsClaimed?.Invoke();
        else if (Phase == ResortPhase.AWAITING_WAVE && upgradeSuggestionPending && LastUpgradeSuggestion != null)
            UpgradeSuggestionReady?.Invoke(LastUpgradeSuggestion);
    }

    private GameSaveData CreateSaveData()
    {
        // A wave exists only while it has a simulation. The serialized activeWaveData field is never
        // null in Unity (the Inspector fills it with an empty wave), so it cannot be used as the signal.
        bool hasWave = waveSimulation != null;
        var save = new GameSaveData
        {
            savedAtUtc = DateTime.UtcNow.ToString("o"),
            resort = resortData,
            hasWave = hasWave,
            wave = hasWave ? waveSimulation.Data : null,
            rewardClaimed = rewardClaimed
        };

        if (waveSimulation != null) save.boosts = waveSimulation.CreateBoostSnapshots();
        if (ServiceLoad != null) save.serviceLoads = ServiceLoad.CreateSnapshots();

        if (Phase == ResortPhase.AWAITING_WAVE && upgradeSuggestionPending && LastUpgradeSuggestion != null)
        {
            save.hasPendingUpgradeSuggestion = true;
            save.pendingSuggestionWave = LastUpgradeSuggestion.WaveNumber;
            save.pendingSuggestionBuilding = LastUpgradeSuggestion.Building;
            save.pendingSuggestionSeconds = LastUpgradeSuggestion.CapacityBottleneckSeconds;
        }
        return save;
    }

    // Collects several changes in one frame into a single write (done in LateUpdate).
    private void RequestSave(string reason)
    {
        saveRequested = true;
        saveReason = reason;
    }

    private void SaveNow(string reason)
    {
        saveRequested = false;
        if (!IsInitialized || resortData == null) return;

        // Short in-between phases are never written; the save follows on the next frame.
        if (Phase == ResortPhase.WAVE_INCOMING || Phase == ResortPhase.WAVE_RESOLVING || Phase == ResortPhase.UPGRADE)
        {
            RequestSave(reason);
            return;
        }

        if (!SaveSystem.TrySave(CreateSaveData(), out string error))
            Debug.LogError($"[Save] Could not save ({reason}): {error}", this);
        else if (logSaves)
            Debug.Log($"[Save] Saved ({reason}): wave {resortData.nextWaveNumber}, {Phase}.", this);
    }

    private void LateUpdate()
    {
        if (!IsInitialized) return;

        if (Phase == ResortPhase.WAVE_ACTIVE && autosaveIntervalSeconds > 0f &&
            Time.unscaledTime >= nextAutosaveTime)
        {
            if (nextAutosaveTime > 0f) RequestSave("autosave during the wave");
            nextAutosaveTime = Time.unscaledTime + autosaveIntervalSeconds;
        }

        if (saveRequested)
            SaveNow(saveReason);
    }

    // Mobile: the app is sent to the background (home button, call, app switcher).
    // Editor/desktop with Run In Background off: the window loses focus (e.g. Alt+Tab).
    private void OnApplicationPause(bool paused)
    {
        if (paused) HandleAppHidden("app went to the background");
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // On phones, losing focus (notification shade, system dialog) also means the player looked away.
        if (!hasFocus && Application.isMobilePlatform) HandleAppHidden("app lost focus");
    }

    private void OnApplicationQuit() => HandleAppHidden("app quit");

    private void HandleAppHidden(string reason)
    {
        if (!IsInitialized || !enabled) return;
        if (Phase == ResortPhase.WAVE_ACTIVE && !activeWaveData.isComplete)
            session.TryPauseWave();
        SaveNow(reason);
    }

    private void HandleWorkersChangedForSave() => RequestSave("Workers moved");

    private void SpawnPausedOverlay()
    {
        GameObject prefab = pausedOverlayPrefab != null
            ? pausedOverlayPrefab
            : Resources.Load<GameObject>("UI/PausedOverlay");
        if (prefab == null)
        {
            Debug.LogWarning("No paused overlay found (Resources/UI/PausedOverlay). A paused wave can only be " +
                             "resumed with Wave/Resume Wave on the GameController.", this);
            return;
        }

        GameObject overlay = Instantiate(prefab);
        PausedOverlayUI ui = overlay.GetComponentInChildren<PausedOverlayUI>(true);
        if (ui != null) ui.Bind(this);
    }

    [ContextMenu("Save/Save Now")]
    private void SaveNowFromMenu() => SaveNow("manual");

    [ContextMenu("Save/Simulate Going To Background")]
    private void SimulateAppHidden() => HandleAppHidden("simulated background");

    [ContextMenu("Save/Log Save File Location")]
    private void LogSaveLocation() => Debug.Log($"[Save] {SaveSystem.SavePath}", this);

    [ContextMenu("Save/Delete Save File")]
    private void DeleteSaveFromMenu()
    {
        SaveSystem.Delete();
        Debug.Log("[Save] Save file deleted. The next action or quit writes a new one.", this);
    }

    // True when no wave after the current one is configured in Waves.json.
    public bool IsFinalWave => IsInitialized && waveCatalog.GetWave(Data.nextWaveNumber + 1) == null;

    internal IReadOnlyList<WaveDefinition> WaveDefinitions => waveCatalog?.waves;

    public event Action<ResortPhase> PhaseChanged;
    public event Action<BuildingType> BuildingUpgraded;
    public event Action FinalWaveRewardsClaimed;
    public event Action<UpgradeSuggestion> UpgradeSuggestionReady;

    private void Start()
    {
        try
        {
            LoadBuildingDefinitions();
            if (waveDefinitionsFile == null)
                throw new InvalidOperationException("Assign the wave definitions JSON file in the Inspector.");
            waveCatalog = JsonUtility.FromJson<WaveCatalog>(waveDefinitionsFile.text);
            if (waveCatalog == null)
                throw new InvalidOperationException("The wave definitions file is empty or invalid.");
            waveCatalog.Validate();
            if (waveCatalog.rewards == null)
                throw new InvalidOperationException("Configure reward settings in Waves.json.");

            // Continue the save, or start a new game (which then replaces the save).
            GameSaveData save = ChooseStartingSave();
            WaveSimulation restoredWave = null;
            ServiceLoadMonitor restoredLoads = null;
            if (save != null && save.hasWave && !TryRebuildSavedWave(save, out restoredWave, out restoredLoads))
                save = null;

            resortData = save != null ? save.resort : CreateNewGameData();
            ValidateBuildingLevels();
            Workers = new WorkerRoster(resortData, GetBuildingDefinition,
                initialSettings != null ? initialSettings.WorkerCapPercent : WorkerRoster.DefaultCapPercent,
                () => CanManageWorkers);
            Workers.Changed += CheckSuddenRushStaffed;
            Workers.Changed += HandleWorkersChangedForSave;
            session = new ResortSession(resortData);
            session.PhaseChanged += HandlePhaseChanged;
            StartedFromSave = save != null;
            if (save != null)
                RestoreSavedState(save, restoredWave, restoredLoads);

            SpawnPausedOverlay();
            HandlePhaseChanged(session.Phase);

            if (save != null)
                AnnounceRestoredState();
            else
                SaveNow("new game");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            enabled = false;
        }
    }

    private void Update()
    {
        // Pausing stops both arrivals and service without changing Unity's global time scale.
        if (Phase != ResortPhase.WAVE_ACTIVE || waveSimulation == null) return;
        waveSimulation.Tick(Time.deltaTime);
        ServiceLoad.Update();
        CheckSuddenRushStaffed();
        if (!activeWaveData.isComplete) return;
        if (SuddenRush != null && SuddenRush.IsPromptPending)
            Debug.Log($"[Sudden Rush] Wave {activeWaveData.waveNumber} ended without an answer to the prompt.", this);
        EvaluateLongestBottleneck();
        EvaluateUpgradeSuggestion();
        // Keep the completed result even if a phase listener immediately prepares the next wave.
        WaveData completedWave = activeWaveData;
        session.TryResolveWave();
        UpdateBuildingPurchaseAvailability(completedWave.waveNumber);
        CalculateWaveResults(completedWave);
        rewardClaimed = false;
        Debug.Log(
            $"Wave {completedWave.waveNumber} evaluation: " +
            $"{LastWaveEvaluation.Stars}/5 stars, " +
            $"satisfaction {LastWaveEvaluation.SatisfactionPercent:F1}%.\n" +
            $"{LastWaveEvaluation.Success}\n" +
            $"{LastWaveEvaluation.AreaToImprove}",
            this);
        session.TryCompleteWave();
        Debug.Log(
            $"Wave {completedWave.waveNumber} complete after {completedWave.elapsedSeconds:F2}s: all generated needs served.",
            this);
    }


    // Satisfaction and rewards of a finished wave. Deterministic, so a loaded save that stopped
    // on the Wave Complete screen gets exactly the same result again.
    private void CalculateWaveResults(WaveData completedWave)
    {
        LastWaveEvaluation = WaveEvaluator.Evaluate(completedWave, evaluationSettings);
        WaveRewardResult baseReward = WaveRewardCalculator.Calculate(
            completedWave,
            LastWaveEvaluation,
            RewardSettings);

        int valueGain = checked(
            baseReward.ResortValue +
            ResortValueCalculator.CalculateWaveGain(
                LastWaveEvaluation.Stars,
                resortValuePerStar));

        LastWaveReward = new WaveRewardResult(
            baseReward.WaveNumber,
            baseReward.CompletedNeeds,
            baseReward.Stars,
            baseReward.IsPerfect,
            baseReward.Coins,
            baseReward.Energy,
            valueGain);
    }

    public int GetResortValueGainForInvestment(int costCoins)
    {
        return ResortValueCalculator.CalculateInvestmentGain(
            costCoins,
            resortValueInvestmentPercent);
    }


    private bool TryGetInvestmentValueGain(
        int cost,
        out int gain,
        out string error)
    {
        gain = 0;
        error = null;

        try
        {
            gain = GetResortValueGainForInvestment(cost);

            // Check for overflow before spending Coins.
            checked
            {
                int newValue = Data.resortValue + gain;
            }

            return true;
        }
        catch (OverflowException)
        {
            error = "Resort Value limit exceeded.";
            return false;
        }
        catch (ArgumentOutOfRangeException)
        {
            error = "Invalid Resort Value settings.";
            return false;
        }
    }

    public BuildingDefinition GetBuildingDefinition(BuildingType type) => definitions[type];
    public WaveDefinition GetWaveDefinition(int waveNumber) => waveCatalog?.GetWave(waveNumber);
    public WaveRewardSettings RewardSettings => waveCatalog?.rewards;

    public string GetBuildingPurchaseBlockReason(BuildingType type) =>
        IsInitialized && definitions.TryGetValue(type, out BuildingDefinition definition)
            ? session.GetPurchaseBlockReason(definition, Workers)
            : "This building is unavailable.";

    public bool TryPurchaseBuilding(BuildingType type, out string error)
    {
        if (!EnsureReady() || !definitions.TryGetValue(type, out BuildingDefinition definition))
        {
            error = "This building is unavailable.";
            return false;
        }

        int price = definition.GetCostCoins(ResortSession.PurchaseLevel);

        if (!TryGetInvestmentValueGain(price, out int gain, out error))
            return false;

        if (!session.TryPurchaseBuilding(definition, Workers, out error))
            return false;

        Data.resortValue += gain;
        RequestSave($"{type} bought");
        return true;
    }

    // Returns null when the next level can be purchased.
    public string GetBuildingUpgradeBlockReason(BuildingType type) =>
        IsInitialized && definitions.TryGetValue(type, out BuildingDefinition definition)
            ? session.GetUpgradeBlockReason(definition)
            : "This building is unavailable.";

    public bool TryUpgradeBuilding(BuildingType type, out string error)
    {
        if (!EnsureReady() || !definitions.TryGetValue(type, out BuildingDefinition definition))
        {
            error = "This building is unavailable.";
            return false;
        }

        BuildingData currentBuilding = Data.GetBuilding(type);

        if (currentBuilding == null)
        {
            error = "Building not found.";
            return false;
        }

        int nextLevel = currentBuilding.level + 1;

        if (nextLevel > definition.MaxLevel)
        {
            error = "This building is already at maximum level.";
            return false;
        }

        int upgradePrice = definition.GetCostCoins(nextLevel);

        if (!TryGetInvestmentValueGain(
                upgradePrice, out int valueGain, out error))
            return false;

        if (!session.TryUpgradeBuilding(definition, out error))
            return false;

        Data.resortValue += valueGain;
        BuildingData building = Data.GetBuilding(type);
        Debug.Log($"Upgraded {type} to level {building.level} for " +
                  $"{definition.GetCostCoins(building.level)} Coins.", this);
        BuildingUpgraded?.Invoke(type);
        RequestSave($"{type} upgraded");
        return true;
    }


    private void EvaluateLongestBottleneck()
    {
        LastWaveBottleneck = null;
        LastWaveBottleneckSeconds = 0d;

        if (ServiceLoad == null)
            return;

        const double epsilon = 1e-9;

        foreach (ServiceLoad load in ServiceLoad.Loads)
        {
            double seconds = load.SecondsAsBottleneck;

            if (seconds <= epsilon)
                continue;

            bool isLonger =
                seconds > LastWaveBottleneckSeconds + epsilon;

            bool isTie =
                Math.Abs(seconds - LastWaveBottleneckSeconds)
                <= epsilon;

            bool winsTie =
                isTie &&
                LastWaveBottleneck.HasValue &&
                (int)load.Building < (int)LastWaveBottleneck.Value;

            if (!LastWaveBottleneck.HasValue ||
                isLonger ||
                winsTie)
            {
                LastWaveBottleneck = load.Building;
                LastWaveBottleneckSeconds = seconds;
            }
        }

        // Diagnostic output for SCRUM-199.
        foreach (ServiceLoad load in ServiceLoad.Loads)
        {
            Debug.Log(
                $"[SCRUM-199] {load.Building}: " +
                $"Total: {load.SecondsAsBottleneck:F2}s, " +
                $"Missing Worker: {load.SecondsMissingWorker:F2}s, " +
                $"Insufficient Capacity: {load.SecondsInsufficientCapacity:F2}s, " +
                $"Peak Queue: {load.PeakQueue}.",
                this);
        }

        if (LastWaveBottleneck.HasValue)
        {
            Debug.Log(
                $"[SCRUM-199] Longest bottleneck: " +
                $"{LastWaveBottleneck.Value}, " +
                $"{LastWaveBottleneckSeconds:F2}s.",
                this);
        }
        else
        {
            Debug.Log(
                "[SCRUM-199] No bottleneck detected.",
                this);
        }
    }


    private void EvaluateUpgradeSuggestion()
    {
        LastUpgradeSuggestion = null;

        if (ServiceLoad == null ||
            activeWaveData == null ||
            IsFinalWave)
        {
            return;
        }

        const double epsilon = 1e-9;

        ServiceLoad bestCandidate = null;
        double longestCapacityTime = 0d;

        foreach (ServiceLoad load in ServiceLoad.Loads)
        {
            BuildingData building = Data.GetBuilding(load.Building);

            // Only owned buildings can be upgraded.
            if (building == null || !building.owned)
                continue;

            if (!definitions.TryGetValue(
                    load.Building,
                    out BuildingDefinition definition))
                continue;

            // Do not suggest upgrades beyond the maximum level.
            if (building.level >= definition.MaxLevel)
                continue;

            double capacityTime = load.SecondsInsufficientCapacity;
            double missingWorkerTime = load.SecondsMissingWorker;

            // Ignore insignificant capacity bottlenecks.
            if (capacityTime + epsilon <
                minimumCapacityBottleneckSeconds)
                continue;

            // Upgrade only when insufficient capacity
            // was the dominant cause of the bottleneck.
            if (capacityTime <= missingWorkerTime + epsilon)
                continue;

            bool isBetter =
                bestCandidate == null ||
                capacityTime > longestCapacityTime + epsilon;

            bool isTie =
                bestCandidate != null &&
                Math.Abs(capacityTime - longestCapacityTime)
                <= epsilon;

            bool winsTie =
                isTie &&
                (int)load.Building < (int)bestCandidate.Building;

            if (isBetter || winsTie)
            {
                bestCandidate = load;
                longestCapacityTime = capacityTime;
            }
        }

        if (bestCandidate == null)
        {
            Debug.Log(
                "[SCRUM-200] No suitable upgrade recommendation.",
                this);
            return;
        }

        LastUpgradeSuggestion = new UpgradeSuggestion(
            activeWaveData.waveNumber,
            bestCandidate.Building,
            longestCapacityTime);

        Debug.Log(
            $"[SCRUM-200] Recommended upgrade: " +
            $"{bestCandidate.Building}, " +
            $"capacity bottleneck " +
            $"{longestCapacityTime:F2}s.",
            this);
    }

    public void SetUpgradeSuggestionOpen(bool isOpen)
    {
        // Answered (Later or Upgrade Now): it no longer comes back after loading.
        if (upgradeSuggestionOpen && !isOpen && upgradeSuggestionPending)
        {
            upgradeSuggestionPending = false;
            RequestSave("upgrade suggestion answered");
        }

        upgradeSuggestionOpen = isOpen;
        RefreshStartWaveButton();
    }

    private void RefreshStartWaveButton()
    {
        if (startWaveButton == null)
            return;

        bool shouldBeVisible =
            Phase == ResortPhase.AWAITING_WAVE &&
            !upgradeSuggestionOpen;

        startWaveButton.gameObject.SetActive(shouldBeVisible);

        startWaveButton.interactable =
            shouldBeVisible && CanStartWave;
    }


    // Void entry points can also be connected directly to UI Button OnClick events.
    [ContextMenu("Wave/Start Wave")]
    public void StartWave()
    {
        if (upgradeSuggestionOpen)
            return;
        
        if (!EnsureReady()) return;
        if (!session.CanStartWave)
        {
            Debug.LogWarning($"Cannot start a wave during {Phase}.", this);
            return;
        }

        WaveDefinition wave = waveCatalog.GetWave(Data.nextWaveNumber);
        if (wave == null)
        {
            Debug.LogWarning($"No definition configured for wave {Data.nextWaveNumber}.", this);
            return;
        }

        // Build before changing phase, so invalid settings cannot leave a half-started wave.
        WaveSimulation nextSimulation;
        try
        {
            foreach (BuildingData building in Data.buildings)
            {
                BuildingDefinition definition = GetBuildingDefinition(building.type);
                if (!building.owned && wave.GetDemand(definition.ServicedNeed) > 0)
                    Debug.Log(
                        $"Wave {wave.waveNumber}: {building.type} must be purchased before it can serve {definition.ServicedNeed}.",
                        this);
            }

            nextSimulation = new WaveSimulation(wave, waveDurationSeconds, CreateBuildingServices(Data));
        }
        catch (Exception exception)
        {
            Debug.LogError($"Cannot start wave: {exception.Message}", this);
            return;
        }

        LastWaveBottleneck = null;
        LastWaveBottleneckSeconds = 0d;
        LastUpgradeSuggestion = null;
        upgradeSuggestionPending = false;
        AttachWave(nextSimulation, new ServiceLoadMonitor(nextSimulation.Data, Data));
        session.TryStartWave();
        // There is no incoming presentation yet, so enter the active phase immediately.
        session.TryBeginWave();
        Debug.Log(
            $"Wave {wave.waveNumber} started: {wave.guestCount} guests; needs arrive over {waveDurationSeconds:F2}s.",
            this);
    }

    // Services of the owned buildings, in building order (a saved wave relies on the same order).
    private List<WaveBuildingService> CreateBuildingServices(ResortData resort)
    {
        var services = new List<WaveBuildingService>();
        foreach (BuildingData building in resort.buildings)
        {
            if (!building.owned) continue;
            BuildingDefinition definition = GetBuildingDefinition(building.type);
            services.Add(new WaveBuildingService(building, definition.ServicedNeed,
                definition.GetCapacity(building.level), definition.ServiceTimeSeconds,
                definition.UtilizationWithoutWorker));
        }
        return services;
    }

    private void AttachWave(WaveSimulation simulation, ServiceLoadMonitor loads)
    {
        DetachWaveSimulation();
        waveSimulation = simulation;
        activeWaveData = simulation.Data;
        waveSimulation.NeedGenerated += HandleNeedGenerated;
        waveSimulation.ArrivalsClosed += HandleArrivalsClosed;
        waveSimulation.SuddenRushStarted += HandleSuddenRushStarted;
        waveSimulation.SuddenRushSkipped += HandleSuddenRushSkipped;
        ServiceLoad = loads;
        ServiceLoad.StateChanged += HandleServiceLoadChanged;
    }

    [ContextMenu("Wave/Pause Wave")]
    public void PauseWave()
    {
        if (EnsureReady() && !session.TryPauseWave())
            Debug.LogWarning($"Cannot pause a wave during {Phase}.", this);
    }

    [ContextMenu("Wave/Resume Wave")]
    public void ResumeWave()
    {
        if (EnsureReady() && !session.TryResumeWave())
            Debug.LogWarning($"Cannot resume a wave during {Phase}.", this);
    }

    // Applies a previously calculated reward exactly once. Does not advance the wave.
    public bool TryClaimRewards(out string error)
    {
        if (!EnsureReady() || !HasUnclaimedRewards)
        {
            error = "No unclaimed wave rewards are available.";
            return false;
        }

        try
        {
            // Validate all additions before modifying any resource.
            int newCoins = checked(Data.coins + LastWaveReward.Coins);
            int newEnergy = checked(Data.energy + LastWaveReward.Energy);
            int newValue = checked(Data.resortValue + LastWaveReward.ResortValue);
            Data.coins = newCoins;
            Data.energy = newEnergy;
            Data.resortValue = newValue;
        }
        catch (OverflowException)
        {
            error = "Reward exceeds the resource limit.";
            return false;
        }

        rewardClaimed = true;
        error = null;
        RequestSave("rewards claimed");
        Debug.Log($"Claimed wave {LastWaveReward.WaveNumber} rewards: " +
                  $"{LastWaveReward.Coins} Coins, {LastWaveReward.Energy} Energy, " +
                  $"{LastWaveReward.ResortValue} Resort Value.", this);
        return true;
    }

    // Direct UI Button.OnClick entry point: claim first, then continue when a next wave exists.
    [ContextMenu("Wave/Claim Rewards and Continue")]
    public void ClaimRewardsAndContinue()
    {
        if (!TryClaimRewards(out string error))
        {
            Debug.LogWarning(error, this);
            return;
        }

        if (IsFinalWave)
        {
            FinishFinalWave();
        }
        else
        {
            NextWave();
            if (Phase == ResortPhase.AWAITING_WAVE &&
                LastUpgradeSuggestion != null)
            {
                upgradeSuggestionPending = true;
                UpgradeSuggestionReady?.Invoke(LastUpgradeSuggestion);
                RequestSave("upgrade suggestion shown");
            }
        }
    }

    // Separate action for the last configured wave. Put the end-of-game flow here,
    // or subscribe to FinalWaveRewardsClaimed (GameEndPanelUI does).
    [ContextMenu("Wave/Finish Final Wave")]
    public void FinishFinalWave()
    {
        if (!EnsureReady()) return;
        if (Phase != ResortPhase.WAVE_COMPLETE || !rewardClaimed || !IsFinalWave)
        {
            Debug.LogWarning("Claim the rewards of the last configured wave before finishing.", this);
            return;
        }

        Debug.Log($"Final wave {Data.nextWaveNumber} finished. No further wave is configured.", this);
        FinalWaveRewardsClaimed?.Invoke();
    }

    [ContextMenu("Wave/Claim Rewards Only (Test)")]
    public void ClaimRewardsForTesting()
    {
        if (!TryClaimRewards(out string error))
            Debug.LogWarning(error, this);
    }

    [ContextMenu("Wave/Next Wave")]
    public void NextWave()
    {
        if (!EnsureReady()) return;
        if (Phase != ResortPhase.WAVE_COMPLETE)
        {
            Debug.LogWarning("Finish the current wave before preparing the next one.", this);
            return;
        }

        if (!rewardClaimed || LastWaveReward == null)
        {
            Debug.LogWarning("Claim the completed wave rewards before continuing.", this);
            return;
        }

        if (waveCatalog.GetWave(Data.nextWaveNumber + 1) == null)
        {
            Debug.LogWarning($"No definition configured for wave {Data.nextWaveNumber + 1}.", this);
            return;
        }

        // Prototype shortcut; reward collection and upgrade UI can use these phases later.
        session.TryEnterUpgrade();
        ClearActiveWave();
        session.TryPrepareNextWave();
    }

    internal bool TrySelectWaveForTesting(int waveNumber)
    {
        if (!EnsureReady() || GetWaveDefinition(waveNumber) == null) return false;

        ClearActiveWave();
        LastWaveEvaluation = null;
        LastWaveReward = null;
        LastWaveBottleneck = null;
        LastWaveBottleneckSeconds = 0d;
        LastUpgradeSuggestion = null;
        upgradeSuggestionPending = false;
        rewardClaimed = false;
        UpdateBuildingPurchaseAvailability(waveNumber - 1);
        session.PrepareWaveForTesting(waveNumber);
        return true;
    }

    private void UpdateBuildingPurchaseAvailability(int completedWaveNumber)
    {
        foreach (BuildingData building in Data.buildings)
        {
            int requiredWave = GetBuildingDefinition(building.type).RequiredCompletedWave;
            if (requiredWave > 0)
                building.availableToPurchase = !building.owned && completedWaveNumber >= requiredWave;
        }
    }

    private void ClearActiveWave()
    {
        DetachWaveSimulation();
        waveSimulation = null;
        activeWaveData = null;
        ServiceLoad = null;
    }

    private void LoadBuildingDefinitions()
    {
        if (buildingDefinitions == null)
            throw new InvalidOperationException("Assign building definitions in the Inspector.");

        definitions.Clear();
        foreach (BuildingDefinition definition in buildingDefinitions)
        {
            if (definition == null)
                throw new InvalidOperationException("A building definition is missing.");
            if (definitions.ContainsKey(definition.Type))
                throw new InvalidOperationException($"Duplicate definition for {definition.Type}.");
            definitions.Add(definition.Type, definition);
        }

    }

    private void ValidateBuildingLevels()
    {
        foreach (BuildingData building in Data.buildings)
        {
            if (!definitions.TryGetValue(building.type, out BuildingDefinition definition))
                throw new InvalidOperationException($"Missing definition for {building.type}.");
            definition.GetCapacity(building.level);
        }
    }

    private bool EnsureReady()
    {
        if (Application.isPlaying && isActiveAndEnabled && IsInitialized)
            return true;

        Debug.LogWarning("Enter Play mode and allow the GameController to initialize first.", this);
        return false;
    }

    private void HandleNeedGenerated(WaveNeedData need, NeedUnit unit)
    {
        if (!logGeneratedNeeds) return;
        string destination = unit.seatedAtSeconds >= 0 ? "in service" : "queued";
        Debug.Log(
            $"[Wave {activeWaveData.waveNumber} @ {unit.arrivedAtSeconds:F2}s] Generated {unit.need} unit #{unit.id} -> {unit.building}: {destination} ({need.OccupiedSlots}/{need.capacity} slots, queue {need.QueueCount}).",
            this);
    }

    private void HandleArrivalsClosed()
    {
        Debug.Log(
            $"Wave {activeWaveData.waveNumber}: arrivals closed at {activeWaveData.arrivalDurationSeconds:F2}s; buildings continue serving until all slots and queues are empty.",
            this);
    }

    private void HandleServiceLoadChanged(ServiceLoad load)
    {
        if (!logServiceLoad) return;
        string cause = load.State == ServiceLoadState.BOTTLENECK ? $" ({load.Cause})" : string.Empty;
        Debug.Log(
            $"[Wave {activeWaveData.waveNumber} @ {activeWaveData.elapsedSeconds:F2}s] {load.Need} at {load.Building}: {load.State}{cause}, queue {load.QueueCount}.",
            this);
    }

    private void HandlePhaseChanged(ResortPhase phase)
    {
        if (logPhaseChanges)
            Debug.Log($"Wave {Data.nextWaveNumber}: {phase}", this);

        RefreshStartWaveButton();

        PhaseChanged?.Invoke(phase);

        if (phase == ResortPhase.AWAITING_WAVE || phase == ResortPhase.WAVE_ACTIVE ||
            phase == ResortPhase.WAVE_PAUSED || phase == ResortPhase.WAVE_COMPLETE)
            RequestSave($"phase {phase}");
    }

    private void DetachWaveSimulation()
    {
        if (waveSimulation == null) return;
        waveSimulation.NeedGenerated -= HandleNeedGenerated;
        waveSimulation.ArrivalsClosed -= HandleArrivalsClosed;
        waveSimulation.SuddenRushStarted -= HandleSuddenRushStarted;
        waveSimulation.SuddenRushSkipped -= HandleSuddenRushSkipped;
        if (ServiceLoad != null) ServiceLoad.StateChanged -= HandleServiceLoadChanged;
    }

    private void OnDestroy()
    {
        DetachWaveSimulation();
        if (Workers != null)
            Workers.Changed -= CheckSuddenRushStaffed;
        if (session != null)
            session.PhaseChanged -= HandlePhaseChanged;
    }
}