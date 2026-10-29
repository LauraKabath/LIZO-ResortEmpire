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
public sealed class WaveCatalog
{
    public WaveDefinition[] waves;

    public void Validate()
    {
        if (waves == null || waves.Length == 0)
            throw new ArgumentException("Configure at least one wave in the wave definitions file.");

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
