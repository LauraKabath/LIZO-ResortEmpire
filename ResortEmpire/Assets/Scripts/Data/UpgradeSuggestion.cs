using System;

public sealed class UpgradeSuggestion
{
    public int WaveNumber { get; }
    public BuildingType Building { get; }
    public double CapacityBottleneckSeconds { get; }

    public UpgradeSuggestion(
        int waveNumber,
        BuildingType building,
        double capacityBottleneckSeconds)
    {
        if (waveNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(waveNumber));

        if (double.IsNaN(capacityBottleneckSeconds) ||
            double.IsInfinity(capacityBottleneckSeconds) ||
            capacityBottleneckSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacityBottleneckSeconds));
        }

        WaveNumber = waveNumber;
        Building = building;
        CapacityBottleneckSeconds = capacityBottleneckSeconds;
    }
}