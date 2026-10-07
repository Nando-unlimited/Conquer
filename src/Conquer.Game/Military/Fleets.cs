using Conquer.Game.Entities;

namespace Conquer.Game.Military;

/// <summary>The sizes of a fleet by its ships: a flotilla has up to three, an escuadra up to three flotillas and a fuerza up to three escuadras.</summary>
public enum NavalEchelon
{
    /// <summary>Flotilla: up to 3 ships.</summary>
    Flotilla,
    /// <summary>Escuadra: up to 3 flotillas (9 ships).</summary>
    Squadron,
    /// <summary>Fuerza: up to 3 escuadras (27 ships).</summary>
    Force,
}

public static class NavalEchelons
{
    /// <summary>Ships each size holds at most.</summary>
    public const int ShipsPerFlotilla = 3, ShipsPerSquadron = 9, ShipsPerForce = 27;

    public static NavalEchelon Of(int ships) => ships <= ShipsPerFlotilla ? NavalEchelon.Flotilla : ships <= ShipsPerSquadron ? NavalEchelon.Squadron : NavalEchelon.Force;

    public static string Name(this NavalEchelon size) => size switch
    {
        NavalEchelon.Flotilla => "Flotilla",
        NavalEchelon.Squadron => "Escuadra",
        _ => "Fuerza",
    };
}

/// <summary>
/// A naval HQ: a Flota, which commands the fleets within its range of its port, or the nation's single Armada, which
/// commands its flotas. It sits in a port and has an admiral.
/// </summary>
public sealed class NavalHeadquarters(int id, Player owner, int level, int number, int baseProvinceId)
{
    public int Id { get; } = id;
    public Player Owner { get; } = owner;
    public int OwnerId => Owner.Id;
    /// <summary>1: Flota; 2: Armada.</summary>
    public int Level { get; } = level;
    public int Number { get; } = number;
    public int BaseProvinceId { get; set; } = baseProvinceId;
    public Officer? Officer { get; set; }

    public bool IsNavy => Level == 2;
    public string Name => IsNavy ? "Armada" : $"{Number}.ª Flota";
}
