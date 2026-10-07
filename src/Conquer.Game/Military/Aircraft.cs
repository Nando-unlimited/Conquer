using Conquer.Game.Entities;
using Conquer.Game.Rules;

namespace Conquer.Game.Military;

/// <summary>What an air unit does over its target province (see <c>GameSession.SetAirMission</c>).</summary>
public enum AirMission
{
    None,
    /// <summary>Fighters: they win the sky over the area and shoot down the enemy's aircraft there.</summary>
    AirSuperiority,
    /// <summary>They bomb the enemy troops fighting in the area.</summary>
    CloseSupport,
    /// <summary>They bomb the enemy's provinces in the area: buildings, workshops and mood.</summary>
    StrategicBombing,
    /// <summary>They strike the enemy's fleets and convoys at sea in the area.</summary>
    NavalStrike,
    /// <summary>Transports: they drop paratroopers on the target.</summary>
    Paradrop,
}

/// <summary>
/// The sizes of an air unit, by its escuadrillas of <see cref="MilitaryRules.PlanesPerFlight"/> planes: an escuadrón has up
/// to three, a grupo up to six escuadrones and an ala up to three grupos.
/// </summary>
public enum AirEchelon
{
    /// <summary>Escuadrilla: the basic unit.</summary>
    Flight,
    /// <summary>Escuadrón: up to 3 escuadrillas.</summary>
    Squadron,
    /// <summary>Grupo: up to 6 escuadrones (18 escuadrillas).</summary>
    Group,
    /// <summary>Ala: up to 3 grupos (54 escuadrillas).</summary>
    Wing,
}

public static class AirEchelons
{
    /// <summary>Escuadrillas each size holds at most.</summary>
    public const int FlightsPerSquadron = 3, FlightsPerGroup = 18, FlightsPerWing = 54;

    public static AirEchelon Of(int flights) =>
        flights <= 1 ? AirEchelon.Flight : flights <= FlightsPerSquadron ? AirEchelon.Squadron : flights <= FlightsPerGroup ? AirEchelon.Group : AirEchelon.Wing;

    public static string Name(this AirEchelon size) => size switch
    {
        AirEchelon.Flight => "Escuadrilla",
        AirEchelon.Squadron => "Escuadrón",
        AirEchelon.Group => "Grupo",
        _ => "Ala",
    };

    /// <summary>"1.ª Escuadrilla", "1.er Escuadrón", "2.º Grupo", "3.ª Ala".</summary>
    public static string Numbered(this AirEchelon size, int n) => size is AirEchelon.Flight or AirEchelon.Wing
        ? $"{n}.ª {size.Name()}"
        : $"{n}{(n % 10 is 1 or 3 && n % 100 is not (11 or 13) ? ".er" : ".º")} {size.Name()}";
}

/// <summary>
/// An air unit: its escuadrillas, each a battalion of an air line (its men the crews, its pieces the planes), all of one
/// kind; the airfield or aircraft carrier it is based on, its mission and the air HQ it reports to. Air units are not on
/// the map: they fly from their base to the province they are sent over, as far as their range.
/// </summary>
public sealed class AirUnit
{
    public int Id { get; }
    public Player Owner { get; }
    public int OwnerId => Owner.Id;
    /// <summary>Its escuadrillas; never empty.</summary>
    public List<Battalion> Flights { get; } = [];
    /// <summary>Its number among its nation's air units of its size: 1.ª Escuadrilla, 2.º Grupo…</summary>
    public int Number { get; set; }
    /// <summary>The province of the airfield it is based at; null when it is on a carrier.</summary>
    public int? BaseProvinceId { get; set; }
    /// <summary>The fleet whose aircraft carriers it is based on; null on land.</summary>
    public int? CarrierId { get; set; }
    public AirMission Mission { get; set; }
    /// <summary>The province its mission is over; null without one.</summary>
    public int? TargetProvinceId { get; set; }
    /// <summary>The División aérea it reports to; null while unattached.</summary>
    public int? CommanderId { get; set; }

    public AirUnit(int id, Player owner, IEnumerable<Battalion> flights, int number)
    {
        Id = id;
        Owner = owner;
        Flights.AddRange(flights);
        Number = number;
    }

    public BattalionType Type => Flights[0].Type;
    /// <summary>The model of its newest escuadrilla, to show.</summary>
    public BattalionInfo Info => Flights.MaxBy(f => f.Model)!.Info;
    public AirEchelon Size => AirEchelons.Of(Flights.Count);
    /// <summary>"1.ª Escuadrilla de cazas", "2.º Grupo de bombarderos estratégicos".</summary>
    public string Name => $"{Size.Numbered(Number)} de {Type.Line().Name.ToLowerInvariant()}";
    /// <summary>Planes it has left, and at full strength.</summary>
    public double PlaneCount => Flights.Sum(f => f.StrengthShare * f.Info.Pieces);
    public int FullPlanes => Flights.Sum(f => f.Info.Pieces);
    public double StrengthShare => FullPlanes == 0 ? 0 : PlaneCount / FullPlanes;
    public double OrganisationShare => Flights.Average(f => f.OrganisationShare);
    /// <summary>Crews it has left.</summary>
    public double Crews => Flights.Sum(f => f.Strength);
    /// <summary>Carriers only take the aircraft made for them: fighters, dive bombers and naval aircraft.</summary>
    public bool FitsOnCarrier => Type is BattalionType.Fighters or BattalionType.CloseSupport or BattalionType.NavalBombers;
}

/// <summary>
/// An air HQ: a División aérea, which commands air units based within its range, or the nation's single Mando aéreo,
/// which commands its divisions. It sits at an airfield and has a general of the air force.
/// </summary>
public sealed class AirHeadquarters
{
    public int Id { get; }
    public Player Owner { get; }
    public int OwnerId => Owner.Id;
    /// <summary>1: División aérea; 2: Mando aéreo.</summary>
    public int Level { get; }
    public int Number { get; }
    public int BaseProvinceId { get; set; }
    public Officer? Officer { get; set; }

    public AirHeadquarters(int id, Player owner, int level, int number, int baseProvinceId)
    {
        Id = id;
        Owner = owner;
        Level = level;
        Number = number;
        BaseProvinceId = baseProvinceId;
    }

    public bool IsCommand => Level == 2;
    public string Name => IsCommand ? "Mando aéreo" : $"{Number}.ª División aérea";
}
