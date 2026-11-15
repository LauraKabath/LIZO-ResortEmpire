using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WaveRewardSmokeTest : MonoBehaviour
{
    [ContextMenu("Run SCRUM-203 Reward Smoke Tests")]
    public void RunTests()
    {
        int passed = 0;
        int failed = 0;
        Check("Perfect five-star wave uses the sixth Coin multiplier", () =>
        {
            WaveRewardResult reward = Calculate(0, 22);
            return reward.IsPerfect && reward.Stars == 5 && reward.CompletedNeeds == 22 &&
                   reward.Coins == 458 && reward.Energy == 5 && reward.ResortValue == 0;
        }, ref passed, ref failed);

        Check("Imperfect five-star wave uses the regular five-star multiplier", () =>
        {
            WaveRewardResult reward = Calculate(1, 22);
            return !reward.IsPerfect && reward.Stars == 5 && reward.Coins == 422;
        }, ref passed, ref failed);

        Check("Longer waits never increase Coin rewards", () =>
        {
            return Calculate(60, 22).Coins <= Calculate(1, 22).Coins;
        }, ref passed, ref failed);

        Check("Unfinished wave cannot receive rewards", () =>
        {
            WaveData unfinished = MakeWave(0, 2);
            unfinished.isComplete = false;
            try
            {
                WaveRewardCalculator.Calculate(unfinished, MakeEvaluation(MakeWave(0, 2)), Settings());
                return false;
            }
            catch (InvalidOperationException) { return true; }
        }, ref passed, ref failed);

        Check("Calculation has no side effects on wave data", () =>
        {
            WaveData wave = MakeWave(0, 3);
            int completedBefore = wave.needs[0].filled;
            WaveRewardCalculator.Calculate(wave, MakeEvaluation(wave), Settings());
            return wave.needs[0].filled == completedBefore && wave.isComplete;
        }, ref passed, ref failed);

        Check("Reward preview matches completed wave number", () =>
        {
            return Calculate(0, 8).WaveNumber == 1;
        }, ref passed, ref failed);

        Debug.Log($"[SCRUM-203] FINAL: {passed} PASSED, {failed} FAILED", this);
    }

    private static WaveRewardSettings Settings() => new WaveRewardSettings
    {
        coinsPerNeedUnit = 16,
        coinMultipliersByRating = new[] { .4f, .6f, .8f, 1f, 1.2f, 1.3f },
        energyByStars = new[] { 0, 1, 2, 3, 5 },
        resortValueGain = 0
    };

    private static WaveData MakeWave(double wait, int count)
    {
        var wave = new WaveData { waveNumber = 1, isComplete = true };
        var need = new WaveNeedData { need = NeedType.Rooms, demandTotal = count, filled = count };
        for (int i = 0; i < count; i++)
            need.completedUnits.Add(new NeedUnit
            {
                arrivedAtSeconds = 0,
                seatedAtSeconds = wait,
                completedAtSeconds = wait + 12
            });
        wave.needs.Add(need);
        return wave;
    }

    private static WaveEvaluationResult MakeEvaluation(WaveData wave) =>
        WaveEvaluator.Evaluate(wave, new WaveEvaluationSettings());

    private static WaveRewardResult Calculate(double wait, int count)
    {
        WaveData wave = MakeWave(wait, count);
        return WaveRewardCalculator.Calculate(wave, MakeEvaluation(wave), Settings());
    }

    private static void Check(string title, Func<bool> test, ref int passed, ref int failed)
    {
        try
        {
            if (test()) { passed++; Debug.Log($"[SCRUM-203] [PASS] {title}"); }
            else { failed++; Debug.LogError($"[SCRUM-203] [FAIL] {title}"); }
        }
        catch (Exception ex)
        {
            failed++;
            Debug.LogError($"[SCRUM-203] [FAIL] {title}: {ex.Message}");
        }
    }
}
