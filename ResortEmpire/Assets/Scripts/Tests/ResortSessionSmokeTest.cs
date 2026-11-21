using System;
using System.Collections.Generic;
using UnityEngine;

/// SCRUM-18: Isolated smoke tests of phase transitions and wave draining.
/// Run through the component's context menu in a dedicated test scene.
/// Does not use or modify GameController or the main game scene.
public sealed class ResortSessionSmokeTest : MonoBehaviour
{
    [SerializeField] private TextAsset wavesJson;

    private int passed;
    private int failed;

    [ContextMenu("Run SCRUM-18 Smoke Tests")]
    public void RunTests()
    {
        passed = 0;
        failed = 0;

        RunCase("Session transitions and action availability", TestSessionTransitions);
        RunCase("90-second arrivals and queue draining", TestArrivalsAndDraining);

        Debug.Log($"[SCRUM-18] FINAL: {passed} PASSED, {failed} FAILED", this);
    }

    private void RunCase(string label, Action test)
    {
        try
        {
            test();
        }
        catch (Exception exception)
        {
            failed++;
            Debug.LogError($"[SCRUM-18] [FAIL] {label}: {exception}", this);
        }
    }

    private void TestSessionTransitions()
    {
        ResortData data = ResortData.CreatePrototypeStart(100, 50, 0);
        var session = new ResortSession(data);
        int phaseChangeCount = 0;
        session.PhaseChanged += _ => phaseChangeCount++;

        Check("01: New game starts awaiting wave",
            session.Phase == ResortPhase.AWAITING_WAVE && data.nextWaveNumber == 1);
        Check("02: Preparation allows start, workers and upgrades",
            session.CanStartWave && session.CanManageWorkers && session.CanUpgradeBuildings);
        Check("03: Cannot skip directly to the next wave",
            !session.TryPrepareNextWave() && data.nextWaveNumber == 1);

        Check("04: Starting a wave enters WAVE_INCOMING",
            session.TryStartWave() && session.Phase == ResortPhase.WAVE_INCOMING);
        Check("05: Cannot start a second wave simultaneously",
            !session.TryStartWave() && !session.CanStartWave);
        Check("06: Incoming phase does not allow workers or upgrades",
            !session.CanManageWorkers && !session.CanUpgradeBuildings);

        Check("07: Beginning the wave enters WAVE_ACTIVE",
            session.TryBeginWave() && session.Phase == ResortPhase.WAVE_ACTIVE);
        Check("08: Active wave allows workers but blocks upgrades",
            session.CanManageWorkers && !session.CanUpgradeBuildings);
        Check("09: Cannot complete an active wave without resolving",
            !session.TryCompleteWave());

        Check("10: Pausing the wave enters WAVE_PAUSED",
            session.TryPauseWave() && session.Phase == ResortPhase.WAVE_PAUSED);
        Check("11: Paused wave blocks starting, workers and upgrades",
            !session.CanStartWave && !session.CanManageWorkers && !session.CanUpgradeBuildings);
        Check("12: Resuming the wave returns to WAVE_ACTIVE",
            session.TryResumeWave() && session.Phase == ResortPhase.WAVE_ACTIVE);

        Check("13: Resolving enters WAVE_RESOLVING",
            session.TryResolveWave() && session.Phase == ResortPhase.WAVE_RESOLVING);
        Check("14: Resolving blocks new waves and upgrades",
            !session.CanStartWave && !session.CanUpgradeBuildings);
        Check("15: Completing enters WAVE_COMPLETE",
            session.TryCompleteWave() && session.Phase == ResortPhase.WAVE_COMPLETE);
        Check("16: Results phase blocks a new wave and worker changes",
            !session.CanStartWave && !session.CanManageWorkers && !session.CanUpgradeBuildings);
        Check("17: Cannot prepare next wave before entering UPGRADE",
            !session.TryPrepareNextWave() && data.nextWaveNumber == 1);

        Check("18: Entering upgrade phase succeeds",
            session.TryEnterUpgrade() && session.Phase == ResortPhase.UPGRADE);
        Check("19: Upgrade phase allows worker changes and upgrades",
            session.CanManageWorkers && session.CanUpgradeBuildings && !session.CanStartWave);
        Check("20: Preparing the next wave increments its number once",
            session.TryPrepareNextWave() && session.Phase == ResortPhase.AWAITING_WAVE &&
            data.nextWaveNumber == 2);
        Check("21: A repeated next-wave request is rejected",
            !session.TryPrepareNextWave() && data.nextWaveNumber == 2);
        Check("22: PhaseChanged fires once per successful transition",
            phaseChangeCount == 8);
    }

    private void TestArrivalsAndDraining()
    {
        if (wavesJson == null)
        {
            Check("23: Waves.json assigned for simulation test", false);
            Debug.LogWarning("[SCRUM-18] Assign Waves.json in the Inspector to run wave tests.", this);
            return;
        }

        WaveCatalog catalog = JsonUtility.FromJson<WaveCatalog>(wavesJson.text);
        if (catalog == null) throw new InvalidOperationException("Cannot parse Waves.json.");
        catalog.Validate();
        WaveDefinition wave = catalog.GetWave(1);
        if (wave == null || wave.rooms < 2)
            throw new InvalidOperationException("Wave 1 must contain at least two Rooms needs.");

        ResortData data = ResortData.CreatePrototypeStart(100, 50, 0);
        // Slow service intentionally ensures a waiting queue after arrivals stop.
        var services = new List<WaveBuildingService>
        {
            new WaveBuildingService(data.GetBuilding(BuildingType.Reception),
                NeedType.Rooms, capacity: 1, serviceTimeSeconds: 100,
                utilizationWithoutWorker: 1)
        };
        var simulation = new WaveSimulation(wave, 90, services);
        int closedEvents = 0;
        simulation.ArrivalsClosed += () => closedEvents++;

        simulation.Tick(89);
        Check("23: Arrivals are still open before 90 seconds",
            !simulation.Data.arrivalsClosed && !simulation.Data.isComplete);

        simulation.Tick(1);
        WaveNeedData rooms = simulation.Data.GetNeed(NeedType.Rooms);
        Check("24: Arrivals close at 90 seconds, with all demand generated",
            simulation.Data.arrivalsClosed && closedEvents == 1 && rooms != null &&
            rooms.demandArrived == rooms.demandTotal);
        Check("25: Wave stays active while the queue is being served",
            !simulation.Data.isComplete && rooms != null &&
            (rooms.QueueCount > 0 || rooms.OccupiedSlots > 0));

        simulation.Tick(2000);
        Check("26: Wave completes only after all Rooms needs are served",
            simulation.Data.isComplete && rooms != null &&
            rooms.filled == rooms.demandTotal && rooms.QueueCount == 0 &&
            rooms.OccupiedSlots == 0);
        Check("27: ArrivalsClosed event fires only once", closedEvents == 1);
    }

    private void Check(string label, bool condition)
    {
        if (condition)
        {
            passed++;
            Debug.Log($"[SCRUM-18] [PASS] {label}", this);
        }
        else
        {
            failed++;
            Debug.LogError($"[SCRUM-18] [FAIL] {label}", this);
        }
    }
}
