namespace Conquer.Game.Simulation;

/// <summary>What happened, for whoever listens (sounds, mainly): it carries no rules of its own.</summary>
public enum GameEventKind
{
    WarDeclared,
    PeaceSigned,
    BattleStarted,
    CityFounded,
    BuildingFinished,
    AdvanceDiscovered,
    SiegeLaid,
    Revolt,
    GiftSent,
}

/// <summary>Something that happened to a nation, and to whom else it happened (-1 if nobody).</summary>
public readonly record struct GameEvent(GameEventKind Kind, int PlayerId, int OtherId = -1)
{
    public bool Involves(int playerId) => PlayerId == playerId || OtherId == playerId;
}

public sealed partial class GameSession
{
    /// <summary>Raised when something worth hearing happens: wars and peace, battles, cities, buildings, advances, sieges, revolts and gifts.</summary>
    public event Action<GameEvent>? Happened;

    private void Raise(GameEventKind kind, int playerId, int otherId = -1) => Happened?.Invoke(new GameEvent(kind, playerId, otherId));
}
