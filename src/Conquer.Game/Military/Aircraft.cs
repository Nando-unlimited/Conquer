using Conquer.Game.Entities;

namespace Conquer.Game.Military;

/// <summary>What an air wing does over its target province (see <c>GameSession.SetAirMission</c>).</summary>
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
/// A wing of aircraft: its planes (a battalion of an air line, whose men are the crews and whose pieces the planes), the
/// airfield or the aircraft carrier it is based on, and its mission. Wings are not units on the map: they fly from their
/// base to the province they are sent over, as far as their range.
/// </summary>
public sealed class AirWing
{
    public int Id { get; }
    public Player Owner { get; }
    public int OwnerId => Owner.Id;
    public Battalion Planes { get; }
    /// <summary>Its number among its nation's wings: 1.ª Ala, 2.ª Ala…</summary>
    public int Number { get; }
    /// <summary>The province of the airfield it is based at; null when it is on a carrier.</summary>
    public int? BaseProvinceId { get; set; }
    /// <summary>The fleet whose aircraft carrier it is based on; null on land.</summary>
    public int? CarrierId { get; set; }
    public AirMission Mission { get; set; }
    /// <summary>The province its mission is over; null without one.</summary>
    public int? TargetProvinceId { get; set; }

    public AirWing(int id, Player owner, Battalion planes, int number)
    {
        Id = id;
        Owner = owner;
        Planes = planes;
        Number = number;
    }

    public BattalionType Type => Planes.Type;
    public BattalionInfo Info => Planes.Info;
    /// <summary>"1.ª Ala de cazas".</summary>
    public string Name => $"{Number}.ª Ala de {Type.Line().Name.ToLowerInvariant()}";
    /// <summary>Planes it has left, out of <see cref="Rules.MilitaryRules.PlanesPerWing"/>.</summary>
    public double PlaneCount => Planes.StrengthShare * Info.Pieces;
    /// <summary>Carriers only take the aircraft made for them: fighters, attack aircraft and naval aircraft.</summary>
    public bool FitsOnCarrier => Type is BattalionType.Fighters or BattalionType.CloseSupport or BattalionType.NavalBombers;
}
