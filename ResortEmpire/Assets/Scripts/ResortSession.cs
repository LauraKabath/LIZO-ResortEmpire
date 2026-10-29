using System;

// Owns game phase rules independently of Unity scene objects.
public sealed class ResortSession
{
    public ResortData Data { get; }
    public ResortPhase Phase => Data.phase;
    public bool CanStartWave => Phase == ResortPhase.AWAITING_WAVE;

    public event Action<ResortPhase> PhaseChanged;

    public ResortSession(ResortData data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public bool TryStartWave() => TryTransition(ResortPhase.AWAITING_WAVE, ResortPhase.WAVE_INCOMING);

    public bool TryBeginWave() => TryTransition(ResortPhase.WAVE_INCOMING, ResortPhase.WAVE_ACTIVE);

    public bool TryPauseWave() => TryTransition(ResortPhase.WAVE_ACTIVE, ResortPhase.WAVE_PAUSED);

    public bool TryResumeWave() => TryTransition(ResortPhase.WAVE_PAUSED, ResortPhase.WAVE_ACTIVE);

    public bool TryResolveWave() => TryTransition(ResortPhase.WAVE_ACTIVE, ResortPhase.WAVE_RESOLVING);

    public bool TryCompleteWave() => TryTransition(ResortPhase.WAVE_RESOLVING, ResortPhase.WAVE_COMPLETE);

    public bool TryEnterUpgrade() => TryTransition(ResortPhase.WAVE_COMPLETE, ResortPhase.UPGRADE);

    public bool TryPrepareNextWave()
    {
        if (Phase != ResortPhase.UPGRADE) return false;
        Data.nextWaveNumber++;
        return TryTransition(ResortPhase.UPGRADE, ResortPhase.AWAITING_WAVE);
    }

    private bool TryTransition(ResortPhase expected, ResortPhase next)
    {
        if (Phase != expected)
            return false;

        Data.phase = next;
        PhaseChanged?.Invoke(next);
        return true;
    }
}
