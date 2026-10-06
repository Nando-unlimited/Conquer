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
    /// <summary>The model of a battalion's or ship's model (<see cref="BattalionInfo.Key"/>): soldiers dressed for their times, guns, ships and planes.</summary>
    public static string Of(BattalionInfo model) => model.Key switch
    {
        "scouts" => "unit-scout",
        "warriors" or "velites" or "swordsmen" or "phalanx" or "legionaries" or "heavy-infantry" or "archers" or "crossbowmen" => "unit-infantry-ancient",
        "arquebusiers" or "pikemen" or "musketeers" => "unit-infantry-musket",
        "light-infantry" or "riflemen" or "machine-gunners" or "medics" => "unit-infantry-modern",
        "mountaineers" or "almogavars" or "mountain-hunters" or "alpine-hunters" or "mountain-troops" => "unit-mountain",
        "paratroopers" => "unit-marines",
        "horsemen" or "cataphracts" or "knights" or "chariots" => "unit-cavalry",
        "mechanised-cavalry" => "unit-cavalry-modern",
        "catapults" or "trebuchets" => "unit-catapult",
        "cannons" => "unit-cannon",
        "field-artillery" or "heavy-artillery" => "unit-artillery",
        "anti-air" => "unit-anti-air",
        "engineers" => "unit-engineer",
        "tanks" => "unit-tank",
        "bombers" or "transport-planes" => "plane-biplane-bombers",
        "heavy-bombers" or "heavy-transports" => "plane-strategic-bombers",
        "jet-bombers" or "jet-transports" => "plane-jet-bombers",
        "biplane-fighters" => "plane-biplane-fighters",
        "monoplane-fighters" => "plane-fighters",
        "jet-fighters" or "jet-attack" => "plane-jet-fighters",
        "biplane-attack" or "dive-bombers" => "plane-dive-bombers",
        "seaplanes" or "torpedo-bombers" or "jet-naval" => "plane-naval-bombers",
        "trireme" or "liburna" => "ship-trireme",
        "transport" or "caravel" => "ship-galley",
        "galleon" or "carrack" or "ship-of-the-line" or "frigate" => "ship-galleon",
        "steam-transport" or "motor-transport" or "cruiser" => "ship-cruiser",
        "ironclad" or "battleship" => "ship-ironclad",
        "destroyer" or "submarine" => "ship-destroyer",
        _ => "ship-aircraft-carrier",
    };

    /// <summary>
    /// The model of a unit: a fleet its strongest ship; a regiment its most numerous line of battalion, in the model the
    /// first of them fights with. Settlers and headquarters have none and keep their counter (the settlers' triangle, the
    /// HQ's letters).
    /// </summary>
    public static string? Of(Unit unit) => unit.Type switch
    {
        UnitType.Settlers or UnitType.Headquarters => null,
        _ when unit.Battalions.Count == 0 => null,
        _ when unit.IsFleet => Of(unit.Battalions.MaxBy(b => b.Info.Defense)!.Info),
        _ => Of(unit.Battalions.GroupBy(b => b.Type).OrderByDescending(g => g.Count()).First().First().Info),
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
