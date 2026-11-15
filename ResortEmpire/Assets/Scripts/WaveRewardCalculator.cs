using System;

// Immutable preview of rewards. Calculating rewards never changes ResortData.
public sealed class WaveRewardResult
{
    public int WaveNumber { get; }
    public int CompletedNeeds { get; }
    public int Stars { get; }
    public bool IsPerfect { get; }
    public int Coins { get; }
    public int Energy { get; }
    public int ResortValue { get; }

    public WaveRewardResult(int waveNumber, int completedNeeds, int stars, bool isPerfect,
        int coins, int energy, int resortValue)
    {
        WaveNumber = waveNumber;
        CompletedNeeds = completedNeeds;
        Stars = stars;
        IsPerfect = isPerfect;
        Coins = coins;
        Energy = energy;
        ResortValue = resortValue;
    }
}

public static class WaveRewardCalculator
{
    public static WaveRewardResult Calculate(WaveData wave, WaveEvaluationResult evaluation,
        WaveRewardSettings settings)
    {
        if (wave == null) throw new ArgumentNullException(nameof(wave));
        if (evaluation == null) throw new ArgumentNullException(nameof(evaluation));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        settings.Validate();

        if (!wave.isComplete || wave.waveNumber != evaluation.WaveNumber ||
            evaluation.Stars < 1 || evaluation.Stars > 5)
            throw new InvalidOperationException("Rewards require a completed, matching wave evaluation.");
        if (wave.needs == null) throw new ArgumentException("Wave needs are missing.");

        int completed = 0;
        checked
        {
            foreach (WaveNeedData need in wave.needs)
            {
                if (need == null || need.demandTotal < 0 || need.filled != need.demandTotal)
                    throw new InvalidOperationException("All generated needs must be completed before rewards.");
                completed += need.filled;
            }
        }

        // The sixth multiplier is reserved for a truly perfect 5-star result.
        bool perfect = evaluation.Stars == 5 && evaluation.SatisfactionPercent >= 100d - 1e-7;
        int multiplierIndex = perfect ? 5 : evaluation.Stars - 1;
        double coinsValue = completed * (double)settings.coinsPerNeedUnit *
                            settings.coinMultipliersByRating[multiplierIndex];
        if (coinsValue > int.MaxValue)
            throw new OverflowException("Wave Coin reward exceeds the supported range.");
        int coins = checked((int)Math.Round(coinsValue, MidpointRounding.AwayFromZero));

        return new WaveRewardResult(wave.waveNumber, completed, evaluation.Stars, perfect,
            coins, settings.energyByStars[evaluation.Stars - 1], settings.resortValueGain);
    }
}
