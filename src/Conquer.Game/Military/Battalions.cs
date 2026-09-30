using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Science;

namespace Conquer.Game.Military;

/// <summary>The kinds of battalion a city can train. Regiments are built from 1 to 4 of them.</summary>
public enum BattalionType
{
    Scouts,
    Warriors,
    Archers,
    BronzeSpearmen,
    Horsemen,
    Chariots,
    ChariotArchers,
    IronInfantry,
    Legionaries,
    Catapults,
    Engineers,
    Cataphracts,
    Knights,
    Crossbowmen,
    Arquebusiers,
    Cannons,
    Musketeers,
    Riflemen,
    FieldArtillery,
    MachineGunners,
    MotorisedInfantry,
    HeavyArtillery,
    Tanks,
    Bombers,
    Trireme,
    Transport,
    Galleon,
    SteamTransport,
    Ironclad,
    Destroyer,
    AircraftCarrier,
}

/// <param name="Men">Citizens the battalion takes from its city, and its full strength.</param>
/// <param name="TrainingDays">Days from paying for it until it is ready.</param>
/// <param name="Requires">Advances needed to train it (all of them).</param>
/// <param name="Attack">Damage it deals per hour when attacking a province.</param>
/// <param name="Defense">Damage it deals per hour when defending one.</param>
/// <param name="MaxOrganisation">How long it keeps fighting; at zero it breaks and retreats.</param>
/// <param name="Speed">Marching speed as a multiple of a walking citizen's.</param>
/// <param name="Mounted">Horses and chariots fight badly in forests, marshes and mountains.</param>
/// <param name="Flies">Aircraft: a regiment of them alone may cross the sea.</param>
/// <param name="Naval">A ship: trained in ports, it forms fleets that sail the sea. Its men are the crew, its attack its guns.</param>
/// <param name="Capacity">Men a ship can carry: troops, staff or settlers.</param>
/// <param name="Shipyard">A building the city needs to build this ship, beyond its port.</param>
public sealed record BattalionInfo(
    string Name, string Symbol, int Men, ResourceCost Cost, int TrainingDays, Tech[] Requires,
    double Attack, double Defense, double MaxOrganisation, double Speed, bool Mounted, bool Flies = false, bool Naval = false, int Capacity = 0,
    BuildingType? Shipyard = null);

public static class Battalions
{
    public static readonly BattalionType[] All = Enum.GetValues<BattalionType>();

    private static readonly Dictionary<BattalionType, BattalionInfo> Table = new()
    {
        // Few men, cheap and quick on their feet: they explore and claim land, but barely fight.
        [BattalionType.Scouts] = new("Exploradores", "S", 50,
            new ResourceCost((ResourceType.Wood, 10), (ResourceType.Gold, 5)), 7, [], 1, 1, 15, 1.5, false),
        [BattalionType.Warriors] = new("Guerreros", "G", 100,
            new ResourceCost((ResourceType.Wood, 30), (ResourceType.Gold, 15)), 15, [], 2, 3, 30, 1, false),
        [BattalionType.Archers] = new("Arqueros", "A", 100,
            new ResourceCost((ResourceType.Wood, 30), (ResourceType.Gold, 20)), 20, [Tech.Archery], 4, 2, 25, 1, false),
        [BattalionType.BronzeSpearmen] = new("Lanceros de bronce", "L", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 20), (ResourceType.Copper, 15)), 25, [Tech.BronzeWorking], 3, 6, 35, 1, false),
        [BattalionType.Horsemen] = new("Jinetes", "J", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 40)), 30, [Tech.HorsebackRiding], 5, 2, 30, 1.8, true),
        [BattalionType.Chariots] = new("Carros de guerra", "C", 100,
            new ResourceCost((ResourceType.Wood, 60), (ResourceType.Gold, 40), (ResourceType.Copper, 10)), 35, [Tech.TheWheel], 7, 3, 30, 1.5, true),
        [BattalionType.ChariotArchers] = new("Carros de arqueros", "K", 100,
            new ResourceCost((ResourceType.Wood, 50), (ResourceType.Gold, 40), (ResourceType.Copper, 10)), 35, [Tech.TheWheel, Tech.Archery], 6, 2, 30, 1.5, true),
        [BattalionType.IronInfantry] = new("Infantería de hierro", "H", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 30), (ResourceType.Iron, 20)), 30, [Tech.IronWorking], 5, 7, 40, 1, false),
        [BattalionType.Legionaries] = new("Legionarios", "M", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 40), (ResourceType.Iron, 25)), 35, [Tech.MilitaryTactics], 7, 9, 50, 1, false),
        [BattalionType.Catapults] = new("Catapultas", "T", 100,
            new ResourceCost((ResourceType.Wood, 90), (ResourceType.Gold, 40), (ResourceType.Copper, 10)), 40, [Tech.SiegeEngines], 12, 1, 20, 0.7, false),
        // Sappers and bridge builders: behind the line they blunt the defenders' terrain; they alone build roads and railways.
        [BattalionType.Engineers] = new("Ingenieros", "E", 100,
            new ResourceCost((ResourceType.Wood, 40), (ResourceType.Gold, 40)), 30, [Tech.Engineering], 2, 3, 30, 1, false),
        [BattalionType.Cataphracts] = new("Catafractos", "F", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 60), (ResourceType.Iron, 30)), 40, [Tech.HeavyCavalry], 10, 5, 45, 1.6, true),
        [BattalionType.Knights] = new("Caballeros", "N", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 80), (ResourceType.Iron, 35)), 45, [Tech.Stirrup], 14, 6, 50, 1.5, true),
        [BattalionType.Crossbowmen] = new("Ballesteros", "B", 100,
            new ResourceCost((ResourceType.Wood, 40), (ResourceType.Gold, 40), (ResourceType.Iron, 10)), 30, [Tech.Machinery], 10, 5, 35, 1, false),
        [BattalionType.Arquebusiers] = new("Arcabuceros", "Q", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 50), (ResourceType.Iron, 20), (ResourceType.Coal, 10)), 35, [Tech.Gunpowder], 13, 9, 45, 1, false),
        [BattalionType.Cannons] = new("Cañones", "O", 100,
            new ResourceCost((ResourceType.Wood, 60), (ResourceType.Gold, 60), (ResourceType.Iron, 40), (ResourceType.Coal, 20)), 45, [Tech.Metallurgy], 20, 3, 25, 0.6, false),
        [BattalionType.Musketeers] = new("Mosqueteros", "U", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 60), (ResourceType.Iron, 25), (ResourceType.Coal, 10)), 35, [Tech.MilitaryScience], 16, 13, 55, 1, false),
        [BattalionType.Riflemen] = new("Fusileros", "R", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 70), (ResourceType.Iron, 30), (ResourceType.Coal, 15)), 35, [Tech.Rifling], 20, 17, 60, 1, false),
        [BattalionType.FieldArtillery] = new("Artillería de campaña", "Y", 100,
            new ResourceCost((ResourceType.Wood, 40), (ResourceType.Gold, 80), (ResourceType.Iron, 60), (ResourceType.Coal, 30)), 45, [Tech.Steel], 28, 5, 30, 0.7, false),
        [BattalionType.MachineGunners] = new("Ametralladoras", "Z", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 80), (ResourceType.Iron, 40), (ResourceType.Coal, 20)), 40, [Tech.MachineGuns], 14, 28, 55, 0.9, false),
        [BattalionType.MotorisedInfantry] = new("Infantería motorizada", "I", 100,
            new ResourceCost((ResourceType.Gold, 100), (ResourceType.Iron, 30), (ResourceType.Oil, 20), (ResourceType.Rubber, 10)), 40, [Tech.Combustion], 24, 20, 65, 2.2, false),
        [BattalionType.HeavyArtillery] = new("Artillería pesada", "W", 100,
            new ResourceCost((ResourceType.Gold, 120), (ResourceType.Iron, 80), (ResourceType.Coal, 30), (ResourceType.Oil, 10)), 50, [Tech.HeavyArtillery], 38, 6, 30, 0.6, false),
        [BattalionType.Tanks] = new("Tanques", "X", 100,
            new ResourceCost((ResourceType.Gold, 150), (ResourceType.Iron, 80), (ResourceType.Oil, 40), (ResourceType.Rubber, 20)), 50, [Tech.Armour], 45, 25, 60, 2, true),
        [BattalionType.Bombers] = new("Bombarderos", "V", 100,
            new ResourceCost((ResourceType.Gold, 200), (ResourceType.Aluminium, 40), (ResourceType.Oil, 40)), 60, [Tech.Aviation], 55, 8, 40, 4, false, Flies: true),

        // Ships (lower-case symbols) sail at their speed times the sailing speed; their attack is their fire at sea.
        [BattalionType.Trireme] = new("Trirreme", "r", 150,
            new ResourceCost((ResourceType.Wood, 90), (ResourceType.Gold, 30)), 40, [Tech.Navigation], 8, 6, 30, 1, false, Naval: true),
        [BattalionType.Transport] = new("Barco de transporte", "t", 50,
            new ResourceCost((ResourceType.Wood, 70), (ResourceType.Gold, 20)), 30, [Tech.Navigation], 1, 2, 20, 0.9, false, Naval: true, Capacity: 600),
        [BattalionType.Galleon] = new("Galeón", "g", 250,
            new ResourceCost((ResourceType.Wood, 160), (ResourceType.Gold, 80), (ResourceType.Iron, 20)), 60, [Tech.Cartography], 20, 15, 45, 1.2, false, Naval: true, Capacity: 200),
        [BattalionType.SteamTransport] = new("Vapor de transporte", "v", 80,
            new ResourceCost((ResourceType.Wood, 60), (ResourceType.Gold, 80), (ResourceType.Iron, 60), (ResourceType.Coal, 30)), 45, [Tech.SteamEngine], 2, 4, 30, 1.8, false,
            Naval: true, Capacity: 1500),
        [BattalionType.Ironclad] = new("Acorazado", "a", 400,
            new ResourceCost((ResourceType.Gold, 200), (ResourceType.Iron, 150), (ResourceType.Coal, 60)), 90, [Tech.Steel], 50, 40, 60, 2, false, Naval: true),
        [BattalionType.Destroyer] = new("Destructor", "d", 300,
            new ResourceCost((ResourceType.Gold, 250), (ResourceType.Iron, 150), (ResourceType.Oil, 60)), 90, [Tech.NavalEngineering], 70, 45, 65, 3, false,
            Naval: true, Shipyard: BuildingType.DryDock),
        [BattalionType.AircraftCarrier] = new("Portaaviones", "p", 800,
            new ResourceCost((ResourceType.Gold, 500), (ResourceType.Iron, 300), (ResourceType.Oil, 120), (ResourceType.Aluminium, 80)), 150,
            [Tech.NavalEngineering, Tech.Aviation], 120, 50, 70, 2.5, false, Naval: true, Shipyard: BuildingType.DryDock),
    };

    public static BattalionInfo Info(this BattalionType type) => Table[type];

    private static readonly HashSet<BattalionType> Artillery =
        [BattalionType.Catapults, BattalionType.Cannons, BattalionType.FieldArtillery, BattalionType.HeavyArtillery];

    /// <summary>What a battalion does in battle: this decides where it stands and what it adds to a mixed force.</summary>
    public static BattalionRole Role(this BattalionType type)
    {
        var info = type.Info();
        return info.Naval ? BattalionRole.Naval
            : info.Flies ? BattalionRole.Air
            : Artillery.Contains(type) ? BattalionRole.Artillery
            : type == BattalionType.Engineers ? BattalionRole.Engineers
            : type == BattalionType.Tanks ? BattalionRole.Armour
            : info.Mounted ? BattalionRole.Cavalry
            : BattalionRole.Infantry;
    }

    public static string Name(this BattalionRole role) => role switch
    {
        BattalionRole.Infantry => "Infantería",
        BattalionRole.Cavalry => "Caballería",
        BattalionRole.Artillery => "Artillería",
        BattalionRole.Armour => "Blindados",
        BattalionRole.Air => "Aviación",
        BattalionRole.Engineers => "Ingenieros",
        BattalionRole.Naval => "Marina",
        _ => role.ToString(),
    };
}

/// <summary>One battalion of a regiment: how many men it has left and how much fight is left in them.</summary>
public sealed class Battalion
{
    public BattalionType Type { get; }
    public double Strength { get; set; }
    public double Organisation { get; set; }
    /// <summary>0 (raw recruits) to 1 (elite): earned in battle, watered down by fresh recruits.</summary>
    public double Experience { get; set; }

    public Battalion(BattalionType type)
    {
        Type = type;
        Strength = type.Info().Men;
        Organisation = type.Info().MaxOrganisation;
    }

    public BattalionInfo Info => Type.Info();
    /// <summary>0..1 of full strength.</summary>
    public double StrengthShare => Strength / Info.Men;
    /// <summary>0..1 of full organisation.</summary>
    public double OrganisationShare => Organisation / Info.MaxOrganisation;

    /// <summary>"Novato", "Regular", "Veterano" or "Élite".</summary>
    public static string ExperienceName(double experience) =>
        experience < 0.2 ? "Novato" : experience < 0.5 ? "Regular" : experience < 0.8 ? "Veterano" : "Élite";
}

/// <summary>What a battalion does in battle (see <see cref="Battalions.Role"/>).</summary>
public enum BattalionRole
{
    /// <summary>Holds the front line.</summary>
    Infantry,
    /// <summary>Fast and hard-hitting on open ground; in the front line.</summary>
    Cavalry,
    /// <summary>Fires from behind the front line and is hard to reach.</summary>
    Artillery,
    /// <summary>Tanks; in the front line.</summary>
    Armour,
    /// <summary>Aircraft: fire from above, hard to reach.</summary>
    Air,
    /// <summary>Ships, which fight only at sea.</summary>
    Naval,
    /// <summary>Behind the front line; attacking, they blunt the defenders' terrain and river (<see cref="Rules.MilitaryRules.DefenseMultiplier"/>).</summary>
    Engineers,
}
