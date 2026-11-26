using System;

public static class ResortValueCalculator
{
    // Reward for successfully completing a wave.
    public static int CalculateWaveGain(int stars, int valuePerStar)
    {
        if (stars < 1 || stars > 5 || valuePerStar < 0)
            throw new ArgumentOutOfRangeException();

        return checked(stars * valuePerStar);
    }

    // Resort Value increases when the player invests Coins.
    public static int CalculateInvestmentGain(
        int investmentCoins, float percentage)
    {
        if (investmentCoins < 0 ||
            float.IsNaN(percentage) ||
            float.IsInfinity(percentage) ||
            percentage < 0f)
            throw new ArgumentOutOfRangeException();

        double gain = investmentCoins * (double)percentage / 100d;

        return checked((int)Math.Round(
            gain, MidpointRounding.AwayFromZero));
    }
}