using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Science;

namespace Conquer.Game.Military;

/// <summary>
/// The lines of battalion a nation can raise. Each line has a model for each age (<see cref="Battalions.Models"/>):
/// warriors become velites, arquebusiers and light infantry as the advances come. A battalion keeps its line and fights
/// with the best model its nation knows.
/// </summary>
public enum BattalionType
{
    Scouts,
    LightInfantry,
    HeavyInfantry,
    RangedInfantry,
    MountainInfantry,
    Paratroopers,
    Cavalry,
    Armour,
    Artillery,
    AntiAir,
    Engineers,
    Medics,
    Bombers,
    Trireme,
    Transport,
    Galleon,
    SteamTransport,
    Ironclad,
    Destroyer,
    AircraftCarrier,
}

/// <summary>The four groups of the army, and the navy and the air force.</summary>
public enum BattalionGroup
{
    Infantry,
    Cavalry,
    Artillery,
    Support,
    Navy,
    Air,
}

/// <summary>One model of a line: what a battalion of it is like.</summary>
/// <param name="Key">Names the model for the client's pictures.</param>
/// <param name="Men">Citizens the battalion takes from its city, and its full strength.</param>
/// <param name="TrainingDays">Days from paying for it until it is ready.</param>
/// <param name="Requires">Advances needed for this model (all of them).</param>
/// <param name="Attack">Damage it deals per hour when attacking a province.</param>
/// <param name="Defense">Damage it deals per hour when defending one.</param>
/// <param name="MaxOrganisation">How long it keeps fighting; at zero it breaks and retreats.</param>
/// <param name="Speed">Marching speed as a multiple of a walking citizen's.</param>
/// <param name="Mounted">Horses, chariots and tanks fight badly in forests, marshes and mountains.</param>
/// <param name="Machine">A war machine: built in a workshop (or the factory it becomes) rather than drilled in barracks.</param>
/// <param name="Flies">Aircraft: a regiment of them alone may cross the sea.</param>
/// <param name="Naval">A ship: trained in ports, it forms fleets that sail the sea. Its men are the crew, its attack its guns.</param>
/// <param name="Capacity">Men a ship can carry: troops, staff or settlers.</param>
/// <param name="Shipyard">A building the city needs to build this ship, beyond its port.</param>
public sealed record BattalionInfo(
    string Key, string Name, string Symbol, int Men, ResourceCost Cost, int TrainingDays, Tech[] Requires,
    double Attack, double Defense, double MaxOrganisation, double Speed, bool Mounted = false, bool Machine = false, bool Flies = false,
    bool Naval = false, int Capacity = 0, BuildingType? Shipyard = null);

/// <summary>A line: its name, its group and its models from the oldest to the newest.</summary>
public sealed record LineInfo(string Name, BattalionGroup Group, BattalionInfo[] Models);

public static class Battalions
{
    public static readonly BattalionType[] All = Enum.GetValues<BattalionType>();

    private static ResourceCost Cost(params (ResourceType, double)[] items) => new(items);

    private const ResourceType W = ResourceType.Wood, G = ResourceType.Gold, Cu = ResourceType.Copper, Fe = ResourceType.Iron,
        C = ResourceType.Coal, Oil = ResourceType.Oil, Rub = ResourceType.Rubber, Al = ResourceType.Aluminium;

    /// <summary>The light infantry's last model, which the heavy infantry also becomes once rifles leave no room for armour.</summary>
    private static readonly BattalionInfo LightInfantry = new("light-infantry", "Infantería ligera", "L", 100, Cost((W, 20), (G, 70), (Fe, 25), (C, 10)), 35,
        [Tech.Rifling], 18, 16, 60, 1.1);

    private static readonly Dictionary<BattalionType, LineInfo> Table = new()
    {
        // Few men, cheap and quick on their feet: they explore and claim land, but barely fight.
        [BattalionType.Scouts] = new("Exploradores", BattalionGroup.Support,
        [
            new("scouts", "Exploradores", "S", 50, Cost((W, 10), (G, 5)), 7, [], 1, 1, 15, 1.5),
        ]),
        [BattalionType.LightInfantry] = new("Infantería ligera", BattalionGroup.Infantry,
        [
            new("warriors", "Guerreros", "G", 100, Cost((W, 30), (G, 15)), 15, [], 2, 3, 30, 1),
            new("velites", "Vélites", "V", 100, Cost((W, 25), (G, 25), (Cu, 5)), 20, [Tech.MilitaryTactics], 4, 5, 35, 1.1),
            new("arquebusiers", "Arcabuceros", "Q", 100, Cost((W, 20), (G, 50), (Fe, 20), (C, 10)), 35, [Tech.Gunpowder], 13, 9, 45, 1),
            LightInfantry,
        ]),
        [BattalionType.HeavyInfantry] = new("Infantería pesada", BattalionGroup.Infantry,
        [
            new("swordsmen", "Espadachines", "D", 100, Cost((W, 20), (G, 20), (Cu, 15)), 25, [Tech.BronzeWorking], 3, 6, 35, 1),
            new("phalanx", "Falanges", "F", 100, Cost((W, 20), (G, 30), (Cu, 20)), 30, [Tech.MilitaryTactics], 5, 8, 40, 0.9),
            new("legionaries", "Legionarios", "M", 100, Cost((W, 20), (G, 40), (Fe, 25)), 35, [Tech.Drill], 7, 9, 50, 1),
            new("heavy-infantry", "Infantería pesada", "H", 100, Cost((W, 20), (G, 50), (Fe, 35)), 40, [Tech.Armouries], 10, 12, 55, 0.9),
            new("pikemen", "Piqueros", "P", 100, Cost((W, 30), (G, 50), (Fe, 25)), 35, [Tech.Gunpowder], 11, 15, 55, 0.9),
            LightInfantry,
        ]),
        [BattalionType.RangedInfantry] = new("Infantería a distancia", BattalionGroup.Infantry,
        [
            new("archers", "Arqueros", "A", 100, Cost((W, 30), (G, 20)), 20, [Tech.Archery], 4, 2, 25, 1),
            new("crossbowmen", "Ballesteros", "B", 100, Cost((W, 40), (G, 40), (Fe, 10)), 30, [Tech.Machinery], 10, 5, 35, 1),
            new("musketeers", "Mosqueteros", "U", 100, Cost((W, 20), (G, 60), (Fe, 25), (C, 10)), 35, [Tech.MilitaryScience], 16, 13, 55, 1),
            new("riflemen", "Fusileros", "R", 100, Cost((W, 20), (G, 70), (Fe, 30), (C, 15)), 35, [Tech.Rifling], 20, 17, 60, 1),
            new("machine-gunners", "Ametralladores", "Z", 100, Cost((W, 20), (G, 80), (Fe, 40), (C, 20)), 40, [Tech.MachineGuns], 22, 30, 60, 0.9),
        ]),
        // At home in the mountains, hills, forests and marshes (MilitaryRules.MountainTroopsRoughTerrain).
        [BattalionType.MountainInfantry] = new("Tropas de montaña", BattalionGroup.Infantry,
        [
            new("mountaineers", "Montañeses", "O", 100, Cost((W, 20), (G, 30), (Cu, 10)), 30, [Tech.MilitaryTactics], 4, 6, 40, 1),
            new("almogavars", "Almogávares", "O", 100, Cost((W, 20), (G, 40), (Fe, 15)), 35, [Tech.Armouries], 9, 9, 45, 1.1),
            new("mountain-hunters", "Cazadores de montaña", "O", 100, Cost((W, 20), (G, 55), (Fe, 20), (C, 10)), 35, [Tech.Gunpowder], 14, 12, 50, 1),
            new("alpine-hunters", "Cazadores alpinos", "O", 100, Cost((W, 20), (G, 75), (Fe, 25), (C, 15)), 40, [Tech.Rifling], 19, 18, 60, 1),
            new("mountain-troops", "Tropas de montaña", "O", 100, Cost((G, 100), (Fe, 30), (C, 15), (Rub, 5)), 40, [Tech.Combustion], 24, 22, 65, 1.1),
        ]),
        [BattalionType.Paratroopers] = new("Paracaidistas", BattalionGroup.Infantry,
        [
            new("paratroopers", "Paracaidistas", "Y", 100, Cost((G, 120), (Fe, 30), (Al, 10)), 45, [Tech.Aviation], 26, 18, 60, 1.2),
        ]),
        [BattalionType.Cavalry] = new("Caballería", BattalionGroup.Cavalry,
        [
            new("horsemen", "Jinetes", "J", 100, Cost((W, 20), (G, 40)), 30, [Tech.HorsebackRiding], 5, 2, 30, 1.8, Mounted: true),
            new("cataphracts", "Catafractos", "K", 100, Cost((W, 20), (G, 60), (Fe, 30)), 40, [Tech.HeavyCavalry], 10, 5, 45, 1.6, Mounted: true),
            new("knights", "Caballeros", "N", 100, Cost((W, 20), (G, 80), (Fe, 35)), 45, [Tech.Stirrup], 14, 6, 50, 1.5, Mounted: true),
            new("mechanised-cavalry", "Caballería mecanizada", "W", 100, Cost((G, 100), (Fe, 30), (Oil, 20), (Rub, 10)), 40, [Tech.Combustion], 24, 20, 65, 2.2),
        ]),
        [BattalionType.Armour] = new("Carros y tanques", BattalionGroup.Cavalry,
        [
            new("chariots", "Carros de guerra", "C", 100, Cost((W, 60), (G, 40), (Cu, 10)), 35, [Tech.TheWheel], 7, 3, 30, 1.5, Mounted: true),
            new("tanks", "Tanques", "X", 100, Cost((G, 150), (Fe, 80), (Oil, 40), (Rub, 20)), 50, [Tech.Armour], 45, 25, 60, 2, Mounted: true, Machine: true),
        ]),
        [BattalionType.Artillery] = new("Artillería", BattalionGroup.Artillery,
        [
            new("catapults", "Catapultas", "T", 100, Cost((W, 90), (G, 40), (Cu, 10)), 40, [Tech.SiegeEngines], 12, 1, 20, 0.7, Machine: true),
            new("trebuchets", "Trabuquetes", "T", 100, Cost((W, 120), (G, 50), (Fe, 10)), 45, [Tech.SiegeWorkshops], 16, 2, 22, 0.6, Machine: true),
            new("cannons", "Cañones", "T", 100, Cost((W, 60), (G, 60), (Fe, 40), (C, 20)), 45, [Tech.Metallurgy], 20, 3, 25, 0.6, Machine: true),
            new("field-artillery", "Artillería de campaña", "T", 100, Cost((W, 40), (G, 80), (Fe, 60), (C, 30)), 45, [Tech.Steel], 28, 5, 30, 0.7, Machine: true),
            new("heavy-artillery", "Artillería pesada", "T", 100, Cost((G, 120), (Fe, 80), (C, 30), (Oil, 10)), 50, [Tech.HeavyArtillery], 38, 6, 30, 0.6, Machine: true),
        ]),
        // Guns pointed at the sky: they shoot down aircraft and shield the troops beside them (MilitaryRules.AntiAirShield).
        [BattalionType.AntiAir] = new("Antiaérea", BattalionGroup.Artillery,
        [
            new("anti-air", "Artillería antiaérea", "Á", 100, Cost((G, 100), (Fe, 50), (C, 20)), 40, [Tech.Aviation], 8, 12, 40, 0.8, Machine: true),
        ]),
        // Sappers and bridge builders: behind the line they blunt the defenders' terrain; they alone build roads and railways.
        [BattalionType.Engineers] = new("Ingenieros", BattalionGroup.Support,
        [
            new("engineers", "Ingenieros", "E", 100, Cost((W, 40), (G, 40)), 30, [Tech.Engineering], 2, 3, 30, 1),
        ]),
        // Behind the line they save some of the wounded (MilitaryRules.MedicsSaving).
        [BattalionType.Medics] = new("Médicos", BattalionGroup.Support,
        [
            new("medics", "Médicos", "+", 50, Cost((W, 10), (G, 30)), 20, [Tech.Medicine], 0, 1, 20, 1),
        ]),
        [BattalionType.Bombers] = new("Bombarderos", BattalionGroup.Air,
        [
            new("bombers", "Bombarderos", "V", 100, Cost((G, 200), (Al, 40), (Oil, 40)), 60, [Tech.Aviation], 55, 8, 40, 4, Machine: true, Flies: true),
        ]),

        // Ships (lower-case symbols) sail at their speed times the sailing speed; their attack is their fire at sea.
        [BattalionType.Trireme] = Ship(new("trireme", "Trirreme", "r", 150, Cost((W, 90), (G, 30)), 40, [Tech.Navigation], 8, 6, 30, 1, Naval: true)),
        [BattalionType.Transport] = Ship(new("transport", "Barco de transporte", "t", 50, Cost((W, 70), (G, 20)), 30, [Tech.Navigation], 1, 2, 20, 0.9,
            Naval: true, Capacity: 600)),
        [BattalionType.Galleon] = Ship(new("galleon", "Galeón", "g", 250, Cost((W, 160), (G, 80), (Fe, 20)), 60, [Tech.Cartography], 20, 15, 45, 1.2,
            Naval: true, Capacity: 200)),
        [BattalionType.SteamTransport] = Ship(new("steam-transport", "Vapor de transporte", "v", 80, Cost((W, 60), (G, 80), (Fe, 60), (C, 30)), 45,
            [Tech.SteamEngine], 2, 4, 30, 1.8, Naval: true, Capacity: 1500)),
        [BattalionType.Ironclad] = Ship(new("ironclad", "Acorazado", "a", 400, Cost((G, 200), (Fe, 150), (C, 60)), 90, [Tech.Steel], 50, 40, 60, 2, Naval: true)),
        [BattalionType.Destroyer] = Ship(new("destroyer", "Destructor", "d", 300, Cost((G, 250), (Fe, 150), (Oil, 60)), 90, [Tech.NavalEngineering], 70, 45, 65, 3,
            Naval: true, Shipyard: BuildingType.DryDock)),
        [BattalionType.AircraftCarrier] = Ship(new("aircraft-carrier", "Portaaviones", "p", 800, Cost((G, 500), (Fe, 300), (Oil, 120), (Al, 80)), 150,
            [Tech.NavalEngineering, Tech.Aviation], 120, 50, 70, 2.5, Naval: true, Shipyard: BuildingType.DryDock)),
    };

    private static LineInfo Ship(BattalionInfo model) => new(model.Name, BattalionGroup.Navy, [model]);

    public static LineInfo Line(this BattalionType type) => Table[type];

    public static IReadOnlyList<BattalionInfo> Models(this BattalionType type) => Table[type].Models;

    /// <summary>The line's first model: what a nation needs to raise the line at all.</summary>
    public static BattalionInfo First(this BattalionType type) => Table[type].Models[0];

    /// <summary>The newest model whose advances are all known, or -1 if the nation cannot raise the line yet.</summary>
    public static int BestModel(this BattalionType type, IReadOnlySet<Tech> known)
    {
        var models = Table[type].Models;
        for (int i = models.Length - 1; i >= 0; i--)
            if (models[i].Requires.All(known.Contains)) return i;
        return -1;
    }

    /// <summary>
    /// Whether a nation knowing these advances would raise the line as the very model of an earlier line (the heavy
    /// infantry once it has become light infantry), so lists of what to train leave it out.
    /// </summary>
    public static bool Redundant(this BattalionType type, IReadOnlySet<Tech> known)
    {
        var model = type.ModelFor(known);
        return All.Any(other => other < type && ReferenceEquals(other.ModelFor(known), model));
    }

    /// <summary>The model a nation knowing these advances would raise: its newest, or the first while it knows none.</summary>
    public static BattalionInfo ModelFor(this BattalionType type, IReadOnlySet<Tech> known) => Table[type].Models[Math.Max(0, type.BestModel(known))];

    /// <summary>
    /// The building a province needs to train the battalion: a workshop for the war machines, barracks for the other
    /// combat troops, and none for scouts, engineers, medics and ships (which need a port instead).
    /// </summary>
    public static BuildingType? TrainingBuilding(this BattalionInfo model, BattalionType type) =>
        model.Naval || type is BattalionType.Scouts or BattalionType.Engineers or BattalionType.Medics ? null
        : model.Machine ? BuildingType.Workshop
        : BuildingType.Barracks;

    /// <summary>What a battalion does in battle: this decides where it stands and what it adds to a mixed force.</summary>
    public static BattalionRole Role(this BattalionType type) => type switch
    {
        BattalionType.Cavalry => BattalionRole.Cavalry,
        BattalionType.Armour => BattalionRole.Armour,
        BattalionType.Artillery or BattalionType.AntiAir => BattalionRole.Artillery,
        BattalionType.Engineers => BattalionRole.Engineers,
        BattalionType.Medics => BattalionRole.Support,
        BattalionType.Bombers => BattalionRole.Air,
        _ when Table[type].Group == BattalionGroup.Navy => BattalionRole.Naval,
        _ => BattalionRole.Infantry,
    };

    public static string Name(this BattalionRole role) => role switch
    {
        BattalionRole.Infantry => "Infantería",
        BattalionRole.Cavalry => "Caballería",
        BattalionRole.Artillery => "Artillería",
        BattalionRole.Armour => "Blindados",
        BattalionRole.Air => "Aviación",
        BattalionRole.Engineers => "Ingenieros",
        BattalionRole.Support => "Apoyo",
        BattalionRole.Naval => "Marina",
        _ => role.ToString(),
    };

    public static string Name(this BattalionGroup group) => group switch
    {
        BattalionGroup.Infantry => "Infantería",
        BattalionGroup.Cavalry => "Caballería",
        BattalionGroup.Artillery => "Artillería",
        BattalionGroup.Support => "Apoyo",
        BattalionGroup.Navy => "Marina",
        BattalionGroup.Air => "Aviación",
        _ => group.ToString(),
    };
}

/// <summary>One battalion of a regiment: its line and model, how many men it has left and how much fight is left in them.</summary>
public sealed class Battalion
{
    public BattalionType Type { get; }
    /// <summary>Which of its line's models it fights with (<see cref="Battalions.Models"/>).</summary>
    public int Model { get; private set; }
    public double Strength { get; set; }
    public double Organisation { get; set; }
    /// <summary>0 (raw recruits) to 1 (elite): earned in battle, watered down by fresh recruits.</summary>
    public double Experience { get; set; }

    public Battalion(BattalionType type, int model = 0)
    {
        Type = type;
        Model = Math.Clamp(model, 0, type.Models().Count - 1);
        Strength = Info.Men;
        Organisation = Info.MaxOrganisation;
    }

    public BattalionInfo Info => Type.Models()[Model];
    /// <summary>0..1 of full strength.</summary>
    public double StrengthShare => Strength / Info.Men;
    /// <summary>0..1 of full organisation.</summary>
    public double OrganisationShare => Organisation / Info.MaxOrganisation;

    /// <summary>Takes up a newer model of its line, keeping its men and its share of organisation; never an older one.</summary>
    public bool Modernise(int model)
    {
        if (model <= Model || model >= Type.Models().Count) return false;
        double organisation = OrganisationShare;
        Model = model;
        Organisation = organisation * Info.MaxOrganisation;
        Strength = Math.Min(Strength, Info.Men);
        return true;
    }

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
    /// <summary>Medics, behind the front line: they do not fight and add nothing to a mixed force, but save some of the wounded.</summary>
    Support,
}
