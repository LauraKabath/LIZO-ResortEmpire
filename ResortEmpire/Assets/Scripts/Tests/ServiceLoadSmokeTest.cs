using System;
using System.Collections.Generic;
using UnityEngine;

/// SCRUM-175: overload detection on waves with known rates. Run from the component context menu.
/// The Reception uses Lv1 tuning: 3 slots, 12 s per guest, 33% speed without a Worker,
/// so it serves 0.25 needs/s with a Worker and 0.0825 needs/s without one.
public sealed class ServiceLoadSmokeTest : MonoBehaviour
{
    private int passed, failed;

    [ContextMenu("SCRUM-175/Run Service Load Tests")]
    public void RunTests()
    {
        passed = 0;
        failed = 0;
        try
        {
            CheckHealthyService();
            CheckMissingWorker();
            CheckInsufficientCapacity();
            CheckCauseAfterAssigningWorker();
            CheckNoFlicker();
        }
        catch (Exception exception)
        {
            failed++;
            Debug.LogError("[SCRUM-175] Unexpected test exception: " + exception);
        }
        Debug.Log($"[SCRUM-175] FINAL: {passed} PASSED, {failed} FAILED");
    }

    // 21 needs in 90 s is 0.233/s, just under what a staffed Reception serves.
    private void CheckHealthyService()
    {
        var run = new Scenario(21, staffed: true);
        run.RunUntil(1000);
        Check("A service that keeps up is never flagged", run.Simulation.Data.isComplete &&
            run.Changes.Count == 0 && run.Load.State == ServiceLoadState.NORMAL &&
            run.Load.SecondsAsBottleneck == 0, run);
    }

    // 20 needs in 90 s is 0.222/s: too much without a Worker, enough with one.
    private void CheckMissingWorker()
    {
        var run = new Scenario(20, staffed: false);
        run.RunUntil(45);
        ServiceLoad load = run.Load;
        Check("A growing queue at an unstaffed building is a bottleneck caused by the missing Worker",
            load.State == ServiceLoadState.BOTTLENECK && load.Cause == BottleneckCause.MISSING_WORKER &&
            load.QueueCount > 0 && load.SecondsAsBottleneck > 0, run);
        Check("The rates show why: too slow now, fast enough with a Worker",
            load.ServiceRate < load.ArrivalRate && load.ArrivalRate < load.ServiceRateWithWorker, run);

        int before = run.Changes.Count;
        WorkerAllocation.TryAssign(run.Resort, 1, BuildingType.Reception, out _);
        run.RunUntil(46);
        Check("Assigning the Worker turns the bottleneck into recovery while guests still wait",
            load.State == ServiceLoadState.RECOVERING && load.Cause == BottleneckCause.NONE && load.QueueCount > 0, run);
        run.RunUntil(1000);
        Check("Recovery ends once the queue is empty, without a new bottleneck",
            run.Simulation.Data.isComplete && load.State == ServiceLoadState.NORMAL &&
            !run.Changes.GetRange(before, run.Changes.Count - before).Exists(change => change.StartsWith("BOTTLENECK")), run);
    }

    // 40 needs in 90 s is 0.444/s: too much even with a Worker.
    private void CheckInsufficientCapacity()
    {
        var run = new Scenario(40, staffed: true);
        run.RunUntil(89);
        ServiceLoad load = run.Load;
        Check("A staffed building that cannot keep up lacks capacity",
            load.State == ServiceLoadState.BOTTLENECK && load.Cause == BottleneckCause.INSUFFICIENT_CAPACITY &&
            load.ServiceRate < load.ArrivalRate, run);
        run.RunUntil(95);
        Check("When arrivals stop, the remaining queue is recovering",
            run.Simulation.Data.arrivalsClosed && load.State == ServiceLoadState.RECOVERING && load.ArrivalRate == 0, run);
        run.RunUntil(1000);
        Check("The wave still finishes and the result keeps the peak queue and bottleneck time",
            run.Simulation.Data.isComplete && load.State == ServiceLoadState.NORMAL &&
            load.PeakQueue >= 10 && load.SecondsAsBottleneck > 30, run);
    }

    private void CheckCauseAfterAssigningWorker()
    {
        var run = new Scenario(40, staffed: false);
        run.RunUntil(40);
        bool missing = run.Load.Cause == BottleneckCause.MISSING_WORKER;
        int before = run.Changes.Count;
        WorkerAllocation.TryAssign(run.Resort, 1, BuildingType.Reception, out _);
        run.RunUntil(41);
        Check("With a Worker but still too slow, the cause changes to capacity", missing &&
            run.Load.State == ServiceLoadState.BOTTLENECK && run.Load.Cause == BottleneckCause.INSUFFICIENT_CAPACITY &&
            run.Changes.Count == before + 1, run);
    }

    // 8 needs in 90 s is 0.089/s, barely above 0.0825/s, so the queue keeps emptying and refilling.
    private void CheckNoFlicker()
    {
        var run = new Scenario(8, staffed: false);
        run.RunUntil(89);
        int bottlenecks = run.Changes.FindAll(change => change.StartsWith("BOTTLENECK")).Count;
        Check("A slightly overloaded service is flagged once and does not flicker",
            bottlenecks == 1 && run.Load.State == ServiceLoadState.BOTTLENECK, run);
    }

    private void Check(string name, bool ok, Scenario run)
    {
        if (ok) { passed++; Debug.Log("[PASS] " + name); }
        else { failed++; Debug.LogError($"[FAIL] {name} | changes: {string.Join(", ", run.Changes)}"); }
    }

    // The Reception serving Rooms over a 90 s wave, ticked like a 10 fps game loop.
    private sealed class Scenario
    {
        public readonly ResortData Resort = ResortData.CreatePrototypeStart(100, 5, 0);
        public readonly WaveSimulation Simulation;
        public readonly ServiceLoadMonitor Monitor;
        public readonly ServiceLoad Load;
        public readonly List<string> Changes = new List<string>();

        public Scenario(int rooms, bool staffed)
        {
            BuildingData reception = Resort.GetBuilding(BuildingType.Reception);
            if (!staffed) WorkerAllocation.TryUnassign(Resort, reception.assignedWorkerId, out _);
            var wave = new WaveDefinition { waveNumber = 1, guestCount = rooms, rooms = rooms };
            Simulation = new WaveSimulation(wave, 90,
                new[] { new WaveBuildingService(reception, NeedType.Rooms, 3, 12, 0.33) });
            Monitor = new ServiceLoadMonitor(Simulation.Data, Resort);
            Monitor.StateChanged += load =>
                Changes.Add($"{load.State}/{load.Cause}@{Simulation.Data.elapsedSeconds:F1}");
            Load = Monitor.Get(NeedType.Rooms);
        }

        public void RunUntil(double seconds)
        {
            while (!Simulation.Data.isComplete && Simulation.Data.elapsedSeconds < seconds)
            {
                Simulation.Tick(0.1);
                Monitor.Update();
            }
        }
    }
}
