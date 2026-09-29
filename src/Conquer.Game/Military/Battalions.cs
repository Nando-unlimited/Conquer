using Conquer.Game.Economy;
using Conquer.Game.Science;

namespace Conquer.Game.Military;

/// <summary>The kinds of battalion a city can train. Regiments are built from 1 to 4 of them.</summary>
public enum BattalionType
{
    Warriors,
    Archers,
    BronzeSpearmen,
    Horsemen,
    Chariots,
    ChariotArchers,
    IronInfantry,
    Legionaries,
    Catapults,
    Cataphracts,
}

/// <param name="Men">Citizens the battalion takes from its city, and its full strength.</param>
/// <param name="TrainingDays">Days from paying for it until it is ready.</param>
/// <param name="Requires">Advances needed to train it (all of them).</param>
/// <param name="Attack">Damage it deals per hour when attacking a province.</param>
/// <param name="Defense">Damage it deals per hour when defending one.</param>
/// <param name="MaxOrganisation">How long it keeps fighting; at zero it breaks and retreats.</param>
/// <param name="Speed">Marching speed as a multiple of a walking citizen's.</param>
/// <param name="Mounted">Horses and chariots fight badly in forests, marshes and mountains.</param>
public sealed record BattalionInfo(
    string Name, string Symbol, int Men, ResourceCost Cost, int TrainingDays, Tech[] Requires,
    double Attack, double Defense, double MaxOrganisation, double Speed, bool Mounted);

public static class Battalions
{
    public static readonly BattalionType[] All = Enum.GetValues<BattalionType>();

    private static readonly Dictionary<BattalionType, BattalionInfo> Table = new()
    {
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
        [BattalionType.Cataphracts] = new("Catafractos", "F", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 60), (ResourceType.Iron, 30)), 40, [Tech.HeavyCavalry], 10, 5, 45, 1.6, true),
    };

    public static BattalionInfo Info(this BattalionType type) => Table[type];
}

/// <summary>One battalion of a regiment: how many men it has left and how much fight is left in them.</summary>
public sealed class Battalion
{
    public BattalionType Type { get; }
    public double Strength { get; set; }
    public double Organisation { get; set; }

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
}
