using System;
using System.Collections.Generic;
using UnityEngine;

// Scene entry point. Add this only to scenes that should run a resort session.
[DisallowMultipleComponent]
public sealed class GameController : MonoBehaviour
{
    [Header("Building definitions")]
    [SerializeField] private BuildingDefinition[] buildingDefinitions;

    [Header("Wave settings")]
    [SerializeField] private TextAsset waveDefinitionsFile;
    [SerializeField, Range(45f, 180f)] private float waveDurationSeconds = 90f;

    [Header("Prototype starting resources")]
    [SerializeField, Min(0)] private int startingCoins = 100;
    [SerializeField, Min(0)] private int startingEnergy = 5;
    [SerializeField, Min(0)] private int startingResortValue;

    [Header("Debugging")]
    [SerializeField] private bool logPhaseChanges = true;
    [SerializeField] private bool logGeneratedNeeds = true;

    [Header("Runtime data (created on Play)")]
    [SerializeField] private ResortData resortData;
    [SerializeField] private WaveData activeWaveData;

    private readonly Dictionary<BuildingType, BuildingDefinition> definitions =
        new Dictionary<BuildingType, BuildingDefinition>();
    private ResortSession session;
    private WaveCatalog waveCatalog;
    private WaveSimulation waveSimulation;

    public bool IsInitialized => session != null;
    public ResortData Data => resortData;
    public WaveData ActiveWave => activeWaveData;
    public ResortPhase Phase => session?.Phase ?? ResortPhase.AWAITING_WAVE;
    public bool CanStartWave => IsInitialized && session.CanStartWave &&
        waveCatalog.GetWave(Data.nextWaveNumber) != null;

    public event Action<ResortPhase> PhaseChanged;

    private void Start()
    {
        try
        {
            // Persistence will plug in here; each Play currently creates a fresh prototype.
            resortData = ResortData.CreatePrototypeStart(
                startingCoins, startingEnergy, startingResortValue);
            LoadBuildingDefinitions();
            if (waveDefinitionsFile == null)
                throw new InvalidOperationException("Assign the wave definitions JSON file in the Inspector.");
            waveCatalog = JsonUtility.FromJson<WaveCatalog>(waveDefinitionsFile.text);
            if (waveCatalog == null)
                throw new InvalidOperationException("The wave definitions file is empty or invalid.");
            waveCatalog.Validate();
            session = new ResortSession(resortData);
            session.PhaseChanged += HandlePhaseChanged;
            HandlePhaseChanged(session.Phase);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Game initialization failed: {exception.Message}", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // Pausing stops both arrivals and service without changing Unity's global time scale.
        if (Phase != ResortPhase.WAVE_ACTIVE || waveSimulation == null) return;
        waveSimulation.Tick(Time.deltaTime);
        if (!activeWaveData.isComplete) return;

        // Keep the completed result even if a phase listener immediately prepares the next wave.
        WaveData completedWave = activeWaveData;
        session.TryResolveWave();
        // Satisfaction and rewards will be added to resolving later.
        session.TryCompleteWave();
        Debug.Log($"Wave {completedWave.waveNumber} complete after {completedWave.elapsedSeconds:F2}s: all generated needs served.", this);
    }

    public BuildingDefinition GetBuildingDefinition(BuildingType type) => definitions[type];
    public WaveDefinition GetWaveDefinition(int waveNumber) => waveCatalog?.GetWave(waveNumber);

    // Void entry points can also be connected directly to UI Button OnClick events.
    [ContextMenu("Wave/Start Wave")]
    public void StartWave()
    {
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
            var services = new List<WaveBuildingService>();
            foreach (BuildingData building in Data.buildings)
            {
                BuildingDefinition definition = GetBuildingDefinition(building.type);
                if (!building.owned)
                {
                    if (wave.GetDemand(definition.ServicedNeed) > 0)
                        Debug.Log($"Wave {wave.waveNumber}: {definition.ServicedNeed} stays locked because {building.type} is not owned.", this);
                    continue;
                }
                services.Add(new WaveBuildingService(building, definition.ServicedNeed,
                    definition.GetCapacity(building.level), definition.ServiceTimeSeconds,
                    definition.UtilizationWithoutWorker));
            }
            nextSimulation = new WaveSimulation(wave, waveDurationSeconds, services);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Cannot start wave: {exception.Message}", this);
            return;
        }

        DetachWaveSimulation();
        waveSimulation = nextSimulation;
        activeWaveData = nextSimulation.Data;
        waveSimulation.NeedGenerated += HandleNeedGenerated;
        waveSimulation.ArrivalsClosed += HandleArrivalsClosed;
        session.TryStartWave();
        // There is no incoming presentation yet, so enter the active phase immediately.
        session.TryBeginWave();
        Debug.Log($"Wave {wave.waveNumber} started: {wave.guestCount} guests; needs arrive over {waveDurationSeconds:F2}s.", this);
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

    [ContextMenu("Wave/Next Wave")]
    public void NextWave()
    {
        if (!EnsureReady()) return;
        if (Phase != ResortPhase.WAVE_COMPLETE)
        {
            Debug.LogWarning("Finish the current wave before preparing the next one.", this);
            return;
        }
        if (waveCatalog.GetWave(Data.nextWaveNumber + 1) == null)
        {
            Debug.LogWarning($"No definition configured for wave {Data.nextWaveNumber + 1}.", this);
            return;
        }

        // Prototype shortcut; reward collection and upgrade UI can use these phases later.
        session.TryEnterUpgrade();
        DetachWaveSimulation();
        waveSimulation = null;
        activeWaveData = null;
        session.TryPrepareNextWave();
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
        Debug.Log($"[Wave {activeWaveData.waveNumber} @ {unit.arrivedAtSeconds:F2}s] Generated {unit.need} unit #{unit.id} -> {unit.building}: {destination} ({need.OccupiedSlots}/{need.capacity} slots, queue {need.QueueCount}).", this);
    }

    private void HandleArrivalsClosed()
    {
        Debug.Log($"Wave {activeWaveData.waveNumber}: arrivals closed at {activeWaveData.arrivalDurationSeconds:F2}s; buildings continue serving until all slots and queues are empty.", this);
    }

    private void HandlePhaseChanged(ResortPhase phase)
    {
        if (logPhaseChanges)
            Debug.Log($"Wave {Data.nextWaveNumber}: {phase}", this);
        PhaseChanged?.Invoke(phase);
    }

    private void DetachWaveSimulation()
    {
        if (waveSimulation == null) return;
        waveSimulation.NeedGenerated -= HandleNeedGenerated;
        waveSimulation.ArrivalsClosed -= HandleArrivalsClosed;
    }

    private void OnDestroy()
    {
        DetachWaveSimulation();
        if (session != null)
            session.PhaseChanged -= HandlePhaseChanged;
    }
}
