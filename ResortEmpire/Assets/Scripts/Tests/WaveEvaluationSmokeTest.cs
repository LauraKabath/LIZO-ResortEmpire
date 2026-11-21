using System;
using UnityEngine;

public sealed class WaveEvaluationSmokeTest : MonoBehaviour
{
    [ContextMenu("Run SCRUM-41 Smoke Tests")]
    public void RunTests()
    {
        int passed = 0;
        int failed = 0;
        var settings = new WaveEvaluationSettings();

        Check("All zero waits produce 5 stars", () =>
            WaveEvaluator.Evaluate(CreateWave(0, 0), settings).Stars == 5, ref passed, ref failed);
        Check("Longer waits lower satisfaction and never increase stars", () =>
        {
            WaveEvaluationResult quick = WaveEvaluator.Evaluate(CreateWave(0, 2), settings);
            WaveEvaluationResult slow = WaveEvaluator.Evaluate(CreateWave(50, 70), settings);
            return slow.SatisfactionPercent < quick.SatisfactionPercent && slow.Stars < quick.Stars;
        }, ref passed, ref failed);
        Check("Result contains successes and areas for improvement", () =>
        {
            WaveEvaluationResult result = WaveEvaluator.Evaluate(CreateWave(5, 60), settings);
            return !string.IsNullOrWhiteSpace(result.Success) &&
                   !string.IsNullOrWhiteSpace(result.AreaToImprove) && result.Needs.Count == 2;
        }, ref passed, ref failed);
        Check("Need weights affect the total score", () =>
        {
            var emphasis = new WaveEvaluationSettings { drinksWeight = 5f };
            double regular = WaveEvaluator.Evaluate(CreateWave(0, 60), settings).SatisfactionPercent;
            double weighted = WaveEvaluator.Evaluate(CreateWave(0, 60), emphasis).SatisfactionPercent;
            return weighted < regular;
        }, ref passed, ref failed);
        Check("An unfinished wave cannot be evaluated", () =>
        {
            WaveData wave = CreateWave(0, 0);
            wave.isComplete = false;
            try { WaveEvaluator.Evaluate(wave, settings); }
            catch (InvalidOperationException) { return true; }
            return false;
        }, ref passed, ref failed);
        Check("Stars remain in the 1 to 5 range", () =>
        {
            for (int wait = 0; wait <= 300; wait += 10)
            {
                int stars = WaveEvaluator.Evaluate(CreateWave(wait, wait), settings).Stars;
                if (stars < 1 || stars > 5) return false;
            }
            return true;
        }, ref passed, ref failed);

        Debug.Log($"[SCRUM-41] FINAL: {passed} PASSED, {failed} FAILED", this);
    }

    private static WaveData CreateWave(double roomsWait, double drinksWait)
    {
        var wave = new WaveData { waveNumber = 1, guestCount = 2, isComplete = true };
        wave.needs.Add(CreateNeed(NeedType.Rooms, roomsWait));
        wave.needs.Add(CreateNeed(NeedType.Drinks, drinksWait));
        return wave;
    }

    private static WaveNeedData CreateNeed(NeedType need, double waiting)
    {
        var record = new WaveNeedData { need = need, demandTotal = 1, demandArrived = 1, filled = 1 };
        record.completedUnits.Add(new NeedUnit
        {
            need = need,
            arrivedAtSeconds = 1,
            seatedAtSeconds = 1 + waiting,
            completedAtSeconds = 2 + waiting
        });
        return record;
    }

    private void Check(string description, Func<bool> test, ref int passed, ref int failed)
    {
        try
        {
            if (test()) { passed++; Debug.Log($"[SCRUM-41] [PASS] {description}", this); }
            else { failed++; Debug.LogError($"[SCRUM-41] [FAIL] {description}", this); }
        }
        catch (Exception ex)
        {
            failed++;
            Debug.LogError($"[SCRUM-41] [FAIL] {description}: {ex.Message}", this);
        }
    }
}
