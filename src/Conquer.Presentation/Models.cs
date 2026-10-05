using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;

namespace Conquer.Presentation;

/// <summary>
/// Which little isometric model stands for each thing on the map, by name (the client draws the picture of that
/// name, and its "-team" parts in the nation's colour): soldiers dressed for their times, guns, ships and planes,
/// castles and towns, and the buildings of a province.
/// </summary>
public static class Models
{
    /// <summary>The model of a kind of battalion or ship.</summary>
    public static string Of(BattalionType type) => type switch
    {
        BattalionType.Scouts => "unit-scout",
        BattalionType.Warriors or BattalionType.Archers or BattalionType.BronzeSpearmen or BattalionType.IronInfantry
            or BattalionType.Legionaries or BattalionType.Crossbowmen => "unit-infantry-ancient",
        BattalionType.Arquebusiers or BattalionType.Musketeers => "unit-infantry-musket",
        BattalionType.Riflemen or BattalionType.MachineGunners => "unit-infantry-modern",
        BattalionType.MotorisedInfantry => "unit-truck",
        BattalionType.Horsemen or BattalionType.Chariots or BattalionType.ChariotArchers or BattalionType.Cataphracts
            or BattalionType.Knights => "unit-cavalry",
        BattalionType.Catapults => "unit-catapult",
        BattalionType.Cannons => "unit-cannon",
        BattalionType.FieldArtillery or BattalionType.HeavyArtillery => "unit-artillery",
        BattalionType.Engineers => "unit-engineer",
        BattalionType.Tanks => "unit-tank",
        BattalionType.Bombers => "plane-biplane-bombers",
        BattalionType.Trireme => "ship-trireme",
        BattalionType.Transport => "ship-galley",
        BattalionType.Galleon => "ship-galleon",
        BattalionType.SteamTransport => "ship-cruiser",
        BattalionType.Ironclad => "ship-ironclad",
        BattalionType.Destroyer => "ship-destroyer",
        _ => "ship-aircraft-carrier",
    };

    /// <summary>
    /// The model of a unit: a fleet its strongest ship; a regiment its most numerous kind of battalion. Settlers and
    /// headquarters have none and keep their counter (the settlers' triangle, the HQ's letters).
    /// </summary>
    public static string? Of(Unit unit) => unit.Type switch
    {
        UnitType.Settlers or UnitType.Headquarters => null,
        _ when unit.Battalions.Count == 0 => null,
        _ when unit.IsFleet => Of(unit.Battalions.Select(b => b.Type).MaxBy(t => t.Info().Defense)),
        _ => Of(unit.Battalions.GroupBy(b => b.Type).OrderByDescending(g => g.Count()).First().Key),
    };

    /// <summary>The model of a building, if it has one: farms a windmill, workshops a forge, libraries a tower...</summary>
    public static string? Of(BuildingType type) => type switch
    {
        BuildingType.Farm or BuildingType.Granary => "building-farm",
        BuildingType.Workshop or BuildingType.Factory => "building-workshop",
        BuildingType.Library or BuildingType.University => "building-library",
        BuildingType.Market or BuildingType.Bank => "building-market",
        BuildingType.Walls or BuildingType.Castle => "building-fort",
        BuildingType.Port or BuildingType.DryDock => "building-port",
        _ => null,
    };

    /// <summary>A city's model: a castle for a capital, a village otherwise.</summary>
    public static string City(bool capital) => capital ? "city-capital" : "city-town";

    /// <summary>The city icons of Assets/BuildingIcons, from a hut to a modern city.</summary>
    public static readonly string[] CityIcons = ["cabana", "pueblo", "capital", "ciudad-moderna"];

    /// <summary>
    /// A city's icon, growing with it: a hut under 2,000 people, a village under 10,000, then a city (the capital's
    /// icon, which a capital always has); in the modern era, capitals and cities of 10,000 or more are modern cities.
    /// </summary>
    public static string CityIcon(double population, bool capital, bool modern) =>
        modern && (capital || population >= 10_000) ? "ciudad-moderna"
        : capital || population >= 10_000 ? "capital"
        : population >= 2000 ? "pueblo"
        : "cabana";
}
