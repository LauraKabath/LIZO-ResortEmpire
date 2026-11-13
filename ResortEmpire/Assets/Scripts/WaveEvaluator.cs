using System;
using System.Collections.Generic;

[Serializable]
public sealed class WaveEvaluationSettings
{
    public float roomsWeight = 1.0f;
    public float drinksWeight = 1.0f;
    public float foodWeight = 1.0f;
    public float relaxWeight = 1.2f;
    public float oceanViewWeight = 1.2f;

    // A wait equal to this value earns 50% satisfaction for that need unit.
    public float waitingScaleSeconds = 30f;

    // Minimum overall satisfaction required for 2, 3, 4 and 5 stars.
    public float twoStarThreshold = 40f;
    public float threeStarThreshold = 60f;
    public float fourStarThreshold = 75f;
    public float fiveStarThreshold = 90f;

    public void Validate()
    {
        if (!Positive(roomsWeight) || !Positive(drinksWeight) ||
            !Positive(foodWeight) || !Positive(relaxWeight) ||
            !Positive(oceanViewWeight) || !Positive(waitingScaleSeconds))
            throw new ArgumentException("Need weights and waiting scale must be positive, finite values.");

        if (!Finite(twoStarThreshold) || !Finite(threeStarThreshold) ||
            !Finite(fourStarThreshold) || !Finite(fiveStarThreshold) ||
            twoStarThreshold <= 0 || twoStarThreshold >= threeStarThreshold ||
            threeStarThreshold >= fourStarThreshold ||
            fourStarThreshold >= fiveStarThreshold || fiveStarThreshold > 100)
            throw new ArgumentException("Rating thresholds must increase strictly and end at or below 100.");
    }

    public float GetWeight(NeedType need)
    {
        switch (need)
        {
            case NeedType.Rooms: return roomsWeight;
            case NeedType.Drinks: return drinksWeight;
            case NeedType.Food: return foodWeight;
            case NeedType.Relax: return relaxWeight;
            case NeedType.OceanView: return oceanViewWeight;
            default: throw new ArgumentOutOfRangeException(nameof(need));
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Positive(float value) => Finite(value) && value > 0f;
}

public sealed class NeedEvaluationResult
{
    public NeedType Need { get; }
    public int CompletedCount { get; }
    public double AverageWaitingSeconds { get; }
    public double SatisfactionPercent { get; }

    public NeedEvaluationResult(NeedType need, int completedCount,
        double averageWaitingSeconds, double satisfactionPercent)
    {
        Need = need;
        CompletedCount = completedCount;
        AverageWaitingSeconds = averageWaitingSeconds;
        SatisfactionPercent = satisfactionPercent;
    }
}

public sealed class WaveEvaluationResult
{
    public int WaveNumber { get; }
    public int Stars { get; }
    public double SatisfactionPercent { get; }
    public double WeightedAverageWaitingSeconds { get; }
    public IReadOnlyList<NeedEvaluationResult> Needs { get; }
    public string Success { get; }
    public string AreaToImprove { get; }

    public WaveEvaluationResult(int waveNumber, int stars, double satisfactionPercent,
        double weightedAverageWaitingSeconds, IReadOnlyList<NeedEvaluationResult> needs,
        string success, string areaToImprove)
    {
        WaveNumber = waveNumber;
        Stars = stars;
        SatisfactionPercent = satisfactionPercent;
        WeightedAverageWaitingSeconds = weightedAverageWaitingSeconds;
        Needs = needs;
        Success = success;
        AreaToImprove = areaToImprove;
    }
}

// Pure calculation: no Unity scene objects, reward changes or side effects.
public static class WaveEvaluator
{
    public static WaveEvaluationResult Evaluate(WaveData wave, WaveEvaluationSettings settings)
    {
        if (wave == null) throw new ArgumentNullException(nameof(wave));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        settings.Validate();
        if (!wave.isComplete) throw new InvalidOperationException("Only completed waves can be evaluated.");
        if (wave.needs == null) throw new ArgumentException("Wave needs cannot be null.", nameof(wave));

        var details = new List<NeedEvaluationResult>();
        double weightedScore = 0;
        double weightedWait = 0;
        double totalWeight = 0;
        NeedEvaluationResult best = null;
        NeedEvaluationResult worst = null;

        foreach (WaveNeedData need in wave.needs)
        {
            if (need == null || need.completedUnits == null)
                throw new ArgumentException("A need or its completed units are missing.", nameof(wave));
            if (need.demandTotal < 0 || need.completedUnits.Count != need.demandTotal)
                throw new ArgumentException("Completed need count does not match configured demand.", nameof(wave));
            if (need.demandTotal == 0) continue;

            double weight = settings.GetWeight(need.need);
            double waitingSum = 0;
            double satisfactionSum = 0;
            foreach (NeedUnit unit in need.completedUnits)
            {
                if (unit == null || unit.seatedAtSeconds < unit.arrivedAtSeconds ||
                    unit.completedAtSeconds < unit.seatedAtSeconds)
                    throw new ArgumentException("Completed units must have valid arrival, seating and completion times.", nameof(wave));

                double wait = unit.WaitingSeconds;
                waitingSum += wait;
                // Smooth, strictly decreasing satisfaction for increasing waiting time.
                double satisfaction = 100d / (1d + wait / settings.waitingScaleSeconds);
                satisfactionSum += satisfaction;
                weightedScore += satisfaction * weight;
                weightedWait += wait * weight;
                totalWeight += weight;
            }

            var detail = new NeedEvaluationResult(need.need, need.completedUnits.Count,
                waitingSum / need.completedUnits.Count,
                satisfactionSum / need.completedUnits.Count);
            details.Add(detail);
            if (best == null || detail.SatisfactionPercent > best.SatisfactionPercent)
                best = detail;
            if (worst == null || detail.SatisfactionPercent < worst.SatisfactionPercent)
                worst = detail;
        }

        if (totalWeight <= 0)
            throw new InvalidOperationException("A completed wave requires at least one generated need.");

        double score = weightedScore / totalWeight;
        int stars = score >= settings.fiveStarThreshold ? 5 :
                    score >= settings.fourStarThreshold ? 4 :
                    score >= settings.threeStarThreshold ? 3 :
                    score >= settings.twoStarThreshold ? 2 : 1;

        string success = $"All {best.CompletedCount} {best.Need} needs were served; " +
                         $"average queue wait: {best.AverageWaitingSeconds:F1}s.";
        string improve = worst.AverageWaitingSeconds > 0
            ? $"Longest waits: {worst.Need} ({worst.AverageWaitingSeconds:F1}s on average). " +
              "Consider assigning a Worker or improving service capacity."
            : "No queue waiting was recorded. Maintain the current service performance.";

        return new WaveEvaluationResult(wave.waveNumber, stars, score,
            weightedWait / totalWeight, details, success, improve);
    }
}
