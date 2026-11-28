// Shared identifiers for the prototype. Do not rename serialized enum values after saves exist.
public enum BuildingType
{
    Reception,
    Restaurant,
    BeachBar,
    Pool,
    Bungalows
}

public enum NeedType
{
    Rooms,
    Food,
    Drinks,
    Relax,
    OceanView
}

// The player's answer to the Sudden Rush prompt. NONE means it is still waiting for an answer.
public enum SuddenRushChoice
{
    NONE,
    MOVE_WORKER,
    USE_ENERGY,
    IGNORE
}

public enum ResortPhase
{
    AWAITING_WAVE,
    WAVE_INCOMING,
    WAVE_ACTIVE,
    WAVE_PAUSED,
    WAVE_RESOLVING,
    WAVE_COMPLETE,
    UPGRADE
}
