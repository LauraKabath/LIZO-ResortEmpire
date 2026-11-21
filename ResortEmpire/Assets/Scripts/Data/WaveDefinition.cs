using System;
using System.Collections.Generic;

// Authored wave counts. Guests are metadata; each need is simulated independently.
[Serializable]
public sealed class WaveDefinition
{
    public int waveNumber;
    public int guestCount;
    public int rooms;
    public int food;
    public int drinks;
    public int relax;
    public int oceanView;

    public int GetDemand(NeedType need)
    {
        switch (need)
        {
            case NeedType.Rooms: return rooms;
            case NeedType.Food: return food;
            case NeedType.Drinks: return drinks;
            case NeedType.Relax: return relax;
            case NeedType.OceanView: return oceanView;
            default: throw new ArgumentOutOfRangeException(nameof(need));
        }
    }

    public void Validate()
    {
        if (waveNumber < 1 || guestCount < 0 || rooms < 0 || food < 0 ||
            drinks < 0 || relax < 0 || oceanView < 0)
            throw new ArgumentException("Wave numbers must be positive and wave counts cannot be negative.");
    }
}

[Serializable]
public sealed class WaveRewardSettings
{
    // Coins = completed need units × coins per unit × rating multiplier.
    public int coinsPerNeedUnit;
    public float[] coinMultipliersByRating; // 1★ through 5★, then Perfect.
    public int[] energyByStars; // 1★ through 5★. Perfect uses the 5★ entry.
    public int resortValueGain;

    internal void Validate()
    {
        if (coinsPerNeedUnit < 0 || resortValueGain < 0 ||
            coinMultipliersByRating == null || coinMultipliersByRating.Length != 6 ||
            Array.Exists(coinMultipliersByRating, value => value < 0 || float.IsNaN(value) || float.IsInfinity(value)) ||
            energyByStars == null || energyByStars.Length != 5 ||
            Array.Exists(energyByStars, value => value < 0))
            throw new ArgumentException("Wave rewards require nonnegative values, six Coin multipliers, and five Energy drops.");
    }
}

[Serializable]
public sealed class WaveCatalog
{
    public WaveRewardSettings rewards;
    public WaveDefinition[] waves;

    public void Validate()
    {
        if (waves == null || waves.Length == 0)
            throw new ArgumentException("Configure at least one wave in the wave definitions file.");

        rewards?.Validate();

        var numbers = new HashSet<int>();
        foreach (WaveDefinition wave in waves)
        {
            if (wave == null)
                throw new ArgumentException("A wave definition is missing.");
            wave.Validate();
            if (!numbers.Add(wave.waveNumber))
                throw new ArgumentException($"Duplicate wave number {wave.waveNumber}.");
        }
    }

    public WaveDefinition GetWave(int waveNumber)
    {
        return Array.Find(waves, wave => wave.waveNumber == waveNumber);
    }
}
