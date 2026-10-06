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
    Transport,
    LineShip,
    Escort,
    Submarine,
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
    bool Naval = false, int Capacity = 0, BuildingType? Shipyard = null)
{
    /// <summary>
    /// The pieces of equipment a battalion of this model needs at full strength: a weapon per man, a horse per rider,
    /// 5 catapults, 10 tanks. Workshops and factories make them; ships need none (they are built whole).
    /// </summary>
    public int Pieces { get; init; } = Naval ? 0 : Men;
    /// <summary>What its pieces are called: "armas", "caballos", "catapultas"...; a shared supply names them itself.</summary>
    public string PieceName { get; init; } = "armas";
    public bool NeedsEquipment => Pieces > 0;

    /// <summary>
    /// The supply it shares with other models, if any: the weapons of its age for the infantry, the general supplies
    /// for scouts, engineers and medics (<see cref="Supplies"/>). The rest have equipment of their own.
    /// </summary>
    public SupplyInfo? Supply { get; init; }

    /// <summary>Where its pieces are kept in the nation's stockpile: under its shared supply's key, or its own.</summary>
    public string SupplyKey => Supply?.Key ?? Key;

    /// <summary>
    /// The supply a workshop makes for this model, as its title: "Armas clásicas", "Suministros", "Catapultas",
    /// "Tanques", or "Caballos (jinetes)" when the kind alone would not tell them apart.
    /// </summary>
    public string SupplyName => Supply?.Name ?? char.ToUpperInvariant(PieceName[0]) + PieceName[1..] + (PieceName == "caballos" ? $" ({Name.ToLowerInvariant()})" : "");

    /// <summary>What training a battalion costs: its gold; the rest of its cost goes into its equipment. A ship costs all of it.</summary>
    public ResourceCost TrainingCost => NeedsEquipment ? new([.. Cost.Items.Where(i => i.Type == ResourceType.Gold)]) : Cost;
    /// <summary>What a whole battalion's equipment costs to make: its cost but the gold.</summary>
    public ResourceCost EquipmentCost => NeedsEquipment ? new([.. Cost.Items.Where(i => i.Type != ResourceType.Gold)]) : new();
    /// <summary>"100 armas clásicas", "50 suministros", "100 caballos de jinetes", "5 catapultas".</summary>
    public string PiecesText(double pieces) =>
        Supply != null ? $"{pieces:N0} {Supply.Name.ToLowerInvariant()}"
        : PieceName == "caballos" ? $"{pieces:N0} {PieceName} de {Name.ToLowerInvariant()}"
        : $"{pieces:N0} {PieceName}";
}

/// <summary>
/// A supply several models share, as in Hearts of Iron: what it is called, its key in the stockpile and what each
/// piece costs to make.
/// </summary>
public sealed record SupplyInfo(string Key, string Name, ResourceCost PieceCost);

/// <summary>The shared supplies: the infantry's weapons, one kind per age, and the general supplies.</summary>
public static class Supplies
{
    private const ResourceType W = ResourceType.Wood, Cu = ResourceType.Copper, Fe = ResourceType.Iron, C = ResourceType.Coal, Rub = ResourceType.Rubber,
        S = ResourceType.Sulfur, Sp = ResourceType.Saltpeter;

    /// <summary>Per piece: a battalion of 100 takes a hundred times as much.</summary>
    private static SupplyInfo Of(string key, string name, params (ResourceType, double PerHundred)[] cost) =>
        new(key, name, new([.. cost.Select(c => (c.Item1, c.PerHundred / 100))]));

    /// <summary>For scouts, engineers and medics, one per man.</summary>
    public static readonly SupplyInfo General = Of("supplies", "Suministros", (W, 20));

    public static readonly SupplyInfo AncientArms = Of("arms-ancient", "Armas antiguas", (W, 30));
    public static readonly SupplyInfo ClassicalArms = Of("arms-classical", "Armas clásicas", (W, 20), (Cu, 15));
    public static readonly SupplyInfo MedievalArms = Of("arms-medieval", "Armas medievales", (W, 25), (Fe, 20));
    public static readonly SupplyInfo Firearms = Of("arms-gunpowder", "Armas de pólvora", (W, 20), (Fe, 20), (S, 5), (Sp, 10));
    public static readonly SupplyInfo Rifles = Of("arms-rifles", "Fusiles", (W, 20), (Fe, 30), (C, 10), (Sp, 5));
    public static readonly SupplyInfo ModernArms = Of("arms-modern", "Armas modernas", (Fe, 30), (C, 10), (Rub, 5), (Sp, 5));

    public static readonly SupplyInfo[] All = [General, AncientArms, ClassicalArms, MedievalArms, Firearms, Rifles, ModernArms];
}

/// <summary>A line: its name, its group and its models from the oldest to the newest.</summary>
public sealed record LineInfo(string Name, BattalionGroup Group, BattalionInfo[] Models);

public static class Battalions
{
    public static readonly BattalionType[] All = Enum.GetValues<BattalionType>();

    private static ResourceCost Cost(params (ResourceType, double)[] items) => new(items);

    /// <summary>A model that takes a shared supply: its cost is its gold plus the supply's pieces for its men.</summary>
    private static BattalionInfo Takes(SupplyInfo supply, BattalionInfo model) =>
        model with { Cost = new([.. model.Cost.Items.Concat(supply.PieceCost.Times(model.Pieces).Items).OrderBy(i => i.Type)]), Supply = supply };

    private const ResourceType W = ResourceType.Wood, G = ResourceType.Gold, Cu = ResourceType.Copper, Fe = ResourceType.Iron,
        C = ResourceType.Coal, Oil = ResourceType.Oil, Rub = ResourceType.Rubber, Al = ResourceType.Aluminium, S = ResourceType.Sulfur,
        Sp = ResourceType.Saltpeter, H = ResourceType.Horses;

    /// <summary>The light infantry's last model, which the heavy infantry also becomes once rifles leave no room for armour.</summary>
    private static readonly BattalionInfo LightInfantry = Takes(Supplies.Rifles, new("light-infantry", "Infantería ligera", "L", 100, Cost((G, 70)), 35,
        [Tech.Rifling], 18, 16, 60, 1.1));

    private static readonly Dictionary<BattalionType, LineInfo> Table = new()
    {
        // Infantry, scouts, engineers and medics take shared supplies (Takes): the weapons of their age, or the general
        // supplies; their cost below is their gold, and Takes adds the supply's.
        // Few men, cheap and quick on their feet: they explore and claim land, but barely fight.
        [BattalionType.Scouts] = new("Exploradores", BattalionGroup.Support,
        [
            Takes(Supplies.General, new("scouts", "Exploradores", "S", 50, Cost((G, 5)), 7, [], 1, 1, 15, 1.5)),
        ]),
        [BattalionType.LightInfantry] = new("Infantería ligera", BattalionGroup.Infantry,
        [
            Takes(Supplies.AncientArms, new("warriors", "Guerreros", "G", 100, Cost((G, 15)), 15, [], 2, 3, 30, 1)),
            Takes(Supplies.ClassicalArms, new("velites", "Vélites", "V", 100, Cost((G, 25)), 20, [Tech.MilitaryTactics], 4, 5, 35, 1.1)),
            Takes(Supplies.Firearms, new("arquebusiers", "Arcabuceros", "Q", 100, Cost((G, 50)), 35, [Tech.Gunpowder], 13, 9, 45, 1)),
            LightInfantry,
        ]),
        [BattalionType.HeavyInfantry] = new("Infantería pesada", BattalionGroup.Infantry,
        [
            Takes(Supplies.AncientArms, new("swordsmen", "Espadachines", "D", 100, Cost((G, 20)), 25, [Tech.BronzeWorking], 3, 6, 35, 1)),
            Takes(Supplies.ClassicalArms, new("phalanx", "Falanges", "F", 100, Cost((G, 30)), 30, [Tech.MilitaryTactics], 5, 8, 40, 0.9)),
            Takes(Supplies.ClassicalArms, new("legionaries", "Legionarios", "M", 100, Cost((G, 40)), 35, [Tech.Drill], 7, 9, 50, 1)),
            Takes(Supplies.MedievalArms, new("heavy-infantry", "Infantería pesada", "H", 100, Cost((G, 50)), 40, [Tech.Armouries], 10, 12, 55, 0.9)),
            Takes(Supplies.Firearms, new("pikemen", "Piqueros", "P", 100, Cost((G, 50)), 35, [Tech.Gunpowder], 11, 15, 55, 0.9)),
            LightInfantry,
        ]),
        [BattalionType.RangedInfantry] = new("Infantería a distancia", BattalionGroup.Infantry,
        [
            Takes(Supplies.AncientArms, new("archers", "Arqueros", "A", 100, Cost((G, 20)), 20, [Tech.Archery], 4, 2, 25, 1)),
            Takes(Supplies.MedievalArms, new("crossbowmen", "Ballesteros", "B", 100, Cost((G, 40)), 30, [Tech.Machinery], 10, 5, 35, 1)),
            Takes(Supplies.Firearms, new("musketeers", "Mosqueteros", "U", 100, Cost((G, 60)), 35, [Tech.MilitaryScience], 16, 13, 55, 1)),
            Takes(Supplies.Rifles, new("riflemen", "Fusileros", "R", 100, Cost((G, 70)), 35, [Tech.Rifling], 20, 17, 60, 1)),
            Takes(Supplies.Rifles, new("machine-gunners", "Ametralladores", "Z", 100, Cost((G, 80)), 40, [Tech.MachineGuns], 22, 30, 60, 0.9)),
        ]),
        // At home in the mountains, hills, forests and marshes (MilitaryRules.MountainTroopsRoughTerrain).
        [BattalionType.MountainInfantry] = new("Tropas de montaña", BattalionGroup.Infantry,
        [
            Takes(Supplies.ClassicalArms, new("mountaineers", "Montañeses", "O", 100, Cost((G, 30)), 30, [Tech.MilitaryTactics], 4, 6, 40, 1)),
            Takes(Supplies.MedievalArms, new("almogavars", "Almogávares", "O", 100, Cost((G, 40)), 35, [Tech.Armouries], 9, 9, 45, 1.1)),
            Takes(Supplies.Firearms, new("mountain-hunters", "Cazadores de montaña", "O", 100, Cost((G, 55)), 35, [Tech.Gunpowder], 14, 12, 50, 1)),
            Takes(Supplies.Rifles, new("alpine-hunters", "Cazadores alpinos", "O", 100, Cost((G, 75)), 40, [Tech.Rifling], 19, 18, 60, 1)),
            Takes(Supplies.ModernArms, new("mountain-troops", "Tropas de montaña", "O", 100, Cost((G, 100)), 40, [Tech.Combustion], 24, 22, 65, 1.1)),
        ]),
        [BattalionType.Paratroopers] = new("Paracaidistas", BattalionGroup.Infantry,
        [
            Takes(Supplies.ModernArms, new("paratroopers", "Paracaidistas", "Y", 100, Cost((G, 120)), 45, [Tech.Aviation], 26, 18, 60, 1.2)),
        ]),
        [BattalionType.Cavalry] = new("Caballería", BattalionGroup.Cavalry,
        [
            new("horsemen", "Jinetes", "J", 100, Cost((W, 10), (G, 40), (H, 100)), 30, [Tech.HorsebackRiding], 5, 2, 30, 1.8, Mounted: true) { PieceName = "caballos" },
            new("cataphracts", "Catafractos", "K", 100, Cost((W, 10), (G, 60), (Fe, 30), (H, 100)), 40, [Tech.HeavyCavalry], 10, 5, 45, 1.6, Mounted: true) { PieceName = "caballos" },
            new("knights", "Caballeros", "N", 100, Cost((W, 10), (G, 80), (Fe, 35), (H, 100)), 45, [Tech.Stirrup], 14, 6, 50, 1.5, Mounted: true) { PieceName = "caballos" },
            new("mechanised-cavalry", "Caballería mecanizada", "W", 100, Cost((G, 100), (Fe, 30), (Oil, 20), (Rub, 10)), 40, [Tech.Combustion], 24, 20, 65, 2.2) { Pieces = 20, PieceName = "vehículos" },
        ]),
        [BattalionType.Armour] = new("Carros y tanques", BattalionGroup.Cavalry,
        [
            new("chariots", "Carros de guerra", "C", 100, Cost((W, 40), (G, 40), (Cu, 10), (H, 60)), 35, [Tech.TheWheel], 7, 3, 30, 1.5, Mounted: true) { Pieces = 30, PieceName = "carros" },
            new("tanks", "Tanques", "X", 50, Cost((G, 150), (Fe, 80), (Oil, 40), (Rub, 20)), 50, [Tech.Armour], 45, 25, 60, 2, Mounted: true, Machine: true) { Pieces = 10, PieceName = "tanques" },
        ]),
        [BattalionType.Artillery] = new("Artillería", BattalionGroup.Artillery,
        [
            new("catapults", "Catapultas", "T", 50, Cost((W, 90), (G, 40), (Cu, 10)), 40, [Tech.SiegeEngines], 12, 1, 20, 0.7, Machine: true) { Pieces = 5, PieceName = "catapultas" },
            new("trebuchets", "Trabuquetes", "T", 50, Cost((W, 120), (G, 50), (Fe, 10)), 45, [Tech.SiegeWorkshops], 16, 2, 22, 0.6, Machine: true) { Pieces = 4, PieceName = "trabuquetes" },
            new("cannons", "Cañones", "T", 80, Cost((W, 60), (G, 60), (Fe, 40), (S, 10), (Sp, 15)), 45, [Tech.Metallurgy], 20, 3, 25, 0.6, Machine: true) { Pieces = 8, PieceName = "cañones" },
            new("field-artillery", "Artillería de campaña", "T", 100, Cost((W, 40), (G, 80), (Fe, 60), (C, 20), (S, 5), (Sp, 10)), 45, [Tech.Steel], 28, 5, 30, 0.7, Machine: true) { Pieces = 12, PieceName = "cañones de campaña" },
            new("heavy-artillery", "Artillería pesada", "T", 100, Cost((G, 120), (Fe, 80), (C, 20), (Oil, 10), (S, 5), (Sp, 10)), 50, [Tech.HeavyArtillery], 38, 6, 30, 0.6, Machine: true) { Pieces = 8, PieceName = "obuses" },
        ]),
        // Guns pointed at the sky: they shoot down aircraft and shield the troops beside them (MilitaryRules.AntiAirShield).
        [BattalionType.AntiAir] = new("Antiaérea", BattalionGroup.Artillery,
        [
            new("anti-air", "Artillería antiaérea", "Á", 80, Cost((G, 100), (Fe, 50), (C, 10), (S, 5), (Sp, 5)), 40, [Tech.Aviation], 8, 12, 40, 0.8, Machine: true) { Pieces = 12, PieceName = "cañones antiaéreos" },
        ]),
        // Sappers and bridge builders: behind the line they blunt the defenders' terrain; they alone build roads and railways.
        [BattalionType.Engineers] = new("Ingenieros", BattalionGroup.Support,
        [
            Takes(Supplies.General, new("engineers", "Ingenieros", "E", 100, Cost((G, 40)), 30, [Tech.Engineering], 2, 3, 30, 1)),
        ]),
        // Behind the line they save some of the wounded (MilitaryRules.MedicsSaving).
        [BattalionType.Medics] = new("Médicos", BattalionGroup.Support,
        [
            Takes(Supplies.General, new("medics", "Médicos", "+", 50, Cost((G, 30)), 20, [Tech.Medicine], 0, 1, 20, 1)),
        ]),
        [BattalionType.Bombers] = new("Bombarderos", BattalionGroup.Air,
        [
            new("bombers", "Bombarderos", "V", 40, Cost((G, 200), (Al, 40), (Oil, 40)), 60, [Tech.Aviation], 55, 8, 40, 4, Machine: true, Flies: true) { Pieces = 10, PieceName = "bombarderos" },
        ]),

        // Ships (lower-case symbols) sail at their speed times the sailing speed; their attack is their fire at sea. Each
        // line has a model per age, like the army's, and its ships are refitted to the newest in port.
        [BattalionType.Transport] = Fleet("Transportes",
        [
            new("transport", "Barco de transporte", "t", 50, Cost((W, 70), (G, 20)), 30, [Tech.Navigation], 1, 2, 20, 0.9, Naval: true, Capacity: 600),
            new("carrack", "Carraca", "k", 70, Cost((W, 110), (G, 40)), 40, [Tech.Cartography], 3, 5, 30, 1.1, Naval: true, Capacity: 900),
            new("steam-transport", "Vapor de transporte", "v", 80, Cost((W, 60), (G, 80), (Fe, 60), (C, 30)), 45, [Tech.SteamEngine], 2, 4, 30, 1.8,
                Naval: true, Capacity: 1500),
            new("motor-transport", "Buque de transporte", "m", 90, Cost((G, 100), (Fe, 80), (Oil, 30)), 45, [Tech.Combustion], 3, 6, 35, 2.5,
                Naval: true, Capacity: 2500),
        ]),
        // The heavy guns of the fleet: they decide the battles at sea.
        [BattalionType.LineShip] = Fleet("Buques de línea",
        [
            new("trireme", "Trirreme", "r", 150, Cost((W, 90), (G, 30)), 40, [Tech.Navigation], 8, 6, 30, 1, Naval: true),
            new("galleon", "Galeón", "g", 250, Cost((W, 160), (G, 80), (Fe, 20)), 60, [Tech.Cartography], 20, 15, 45, 1.2, Naval: true, Capacity: 200),
            new("ship-of-the-line", "Navío de línea", "n", 400, Cost((W, 200), (G, 100), (Fe, 40)), 70, [Tech.Metallurgy], 35, 25, 50, 1.2, Naval: true),
            new("ironclad", "Acorazado", "a", 400, Cost((G, 200), (Fe, 150), (C, 60)), 90, [Tech.Steel], 50, 40, 60, 2, Naval: true),
            new("battleship", "Acorazado moderno", "b", 900, Cost((G, 400), (Fe, 300), (Oil, 100)), 150, [Tech.NavalEngineering], 100, 70, 70, 2.5,
                Naval: true, Shipyard: BuildingType.DryDock),
        ]),
        // Light and fast: they screen the fleet and, with the convoys, hunt submarines.
        [BattalionType.Escort] = Fleet("Escoltas",
        [
            new("liburna", "Liburna", "l", 80, Cost((W, 50), (G, 20)), 25, [Tech.Navigation], 5, 5, 30, 1.5, Naval: true),
            new("caravel", "Carabela", "c", 60, Cost((W, 80), (G, 30)), 30, [Tech.Cartography], 9, 8, 35, 1.6, Naval: true),
            new("frigate", "Fragata", "f", 200, Cost((W, 120), (G, 60), (Fe, 20)), 45, [Tech.Metallurgy], 22, 15, 45, 1.7, Naval: true),
            new("cruiser", "Crucero", "u", 300, Cost((G, 180), (Fe, 100), (C, 40)), 60, [Tech.Steel], 40, 30, 55, 2.3, Naval: true),
            new("destroyer", "Destructor", "d", 300, Cost((G, 250), (Fe, 150), (Oil, 60)), 90, [Tech.NavalEngineering], 70, 45, 65, 3,
                Naval: true, Shipyard: BuildingType.DryDock),
        ]),
        // They strike unseen and hunt merchant ships; weak if caught.
        [BattalionType.Submarine] = Fleet("Submarinos",
        [
            new("submarine", "Submarino", "s", 60, Cost((G, 150), (Fe, 80), (Oil, 40)), 60, [Tech.Combustion], 45, 10, 50, 1.8, Naval: true),
        ]),
        [BattalionType.AircraftCarrier] = Fleet("Portaaviones",
        [
            new("aircraft-carrier", "Portaaviones", "p", 800, Cost((G, 500), (Fe, 300), (Oil, 120), (Al, 80)), 150,
                [Tech.NavalEngineering, Tech.Aviation], 120, 50, 70, 2.5, Naval: true, Shipyard: BuildingType.DryDock),
        ]),
    };

    private static LineInfo Fleet(string name, BattalionInfo[] models) => new(name, BattalionGroup.Navy, models);

    /// <summary>
    /// A batch of convoys (<see cref="Rules.MilitaryRules.ConvoysPerOrder"/>) as the shipyards build it: merchant ships
    /// that carry the shipments over the sea and do not form fleets. Its men are the crews of the whole batch.
    /// </summary>
    public static readonly BattalionInfo ConvoyBatch = new("convoys", "Convoyes", "c", 50, Cost((W, 60), (G, 20)), 15, [Tech.Navigation], 0, 1, 10, 1, Naval: true);

    public static LineInfo Line(this BattalionType type) => Table[type];

    private static readonly Dictionary<string, (BattalionType Type, int Index)> ByKeys =
        Table.SelectMany(line => line.Value.Models.Select((m, i) => (ModelKey: m.Key, Type: line.Key, Index: i))).GroupBy(x => x.ModelKey)
            .ToDictionary(g => g.Key, g => (g.First().Type, g.First().Index));

    /// <summary>The line and the place in it of the model with this key (<see cref="BattalionInfo.Key"/>); null if there is none.</summary>
    public static (BattalionType Type, int Index)? ByKey(string? key) => key != null && ByKeys.TryGetValue(key, out var found) ? found : null;

    /// <summary>The models that take a shared supply, in the order of their lines.</summary>
    public static IEnumerable<BattalionInfo> UsersOf(SupplyInfo supply) =>
        Table.Values.SelectMany(l => l.Models).Where(m => m.Supply == supply).Distinct();

    /// <summary>The first model kept under this stockpile key (a shared supply's or a model's own), with its line; null if none.</summary>
    public static (BattalionType Type, BattalionInfo Model)? BySupply(string key) =>
        Table.SelectMany(l => l.Value.Models.Select(m => (l.Key, m))).Where(x => x.m.SupplyKey == key).Cast<(BattalionType, BattalionInfo)?>().FirstOrDefault();

    /// <summary>The model with this key; null if there is none.</summary>
    public static BattalionInfo? ModelByKey(string? key) => ByKey(key) is var (type, index) ? Table[type].Models[index] : null;

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
