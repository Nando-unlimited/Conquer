using Conquer.Game.Economy;
using Conquer.Game.Science;

namespace Conquer.Game.Military;

/// <summary>The kinds of brigade a city can train. Divisions are built from 1 to 4 of them.</summary>
public enum BrigadeType
{
    Warriors,
    Archers,
    BronzeSpearmen,
    Horsemen,
    Chariots,
    ChariotArchers,
    IronInfantry,
}

/// <param name="Men">Citizens the brigade takes from its city, and its full strength.</param>
/// <param name="TrainingDays">Days from paying for it until it is ready.</param>
/// <param name="Requires">Advances needed to train it (all of them).</param>
/// <param name="Attack">Damage it deals per hour when attacking a province.</param>
/// <param name="Defense">Damage it deals per hour when defending one.</param>
/// <param name="MaxOrganisation">How long it keeps fighting; at zero it breaks and retreats.</param>
/// <param name="Speed">Marching speed as a multiple of a walking citizen's.</param>
/// <param name="Mounted">Horses and chariots fight badly in forests, marshes and mountains.</param>
public sealed record BrigadeInfo(
    string Name, string Symbol, int Men, ResourceCost Cost, int TrainingDays, Tech[] Requires,
    double Attack, double Defense, double MaxOrganisation, double Speed, bool Mounted);

public static class Brigades
{
    public static readonly BrigadeType[] All = Enum.GetValues<BrigadeType>();

    private static readonly Dictionary<BrigadeType, BrigadeInfo> Table = new()
    {
        [BrigadeType.Warriors] = new("Guerreros", "G", 100,
            new ResourceCost((ResourceType.Wood, 30), (ResourceType.Gold, 15)), 15, [], 2, 3, 30, 1, false),
        [BrigadeType.Archers] = new("Arqueros", "A", 100,
            new ResourceCost((ResourceType.Wood, 30), (ResourceType.Gold, 20)), 20, [Tech.Archery], 4, 2, 25, 1, false),
        [BrigadeType.BronzeSpearmen] = new("Lanceros de bronce", "L", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 20), (ResourceType.Copper, 15)), 25, [Tech.BronzeWorking], 3, 6, 35, 1, false),
        [BrigadeType.Horsemen] = new("Jinetes", "J", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 40)), 30, [Tech.HorsebackRiding], 5, 2, 30, 1.8, true),
        [BrigadeType.Chariots] = new("Carros de guerra", "C", 100,
            new ResourceCost((ResourceType.Wood, 60), (ResourceType.Gold, 40), (ResourceType.Copper, 10)), 35, [Tech.TheWheel], 7, 3, 30, 1.5, true),
        [BrigadeType.ChariotArchers] = new("Carros de arqueros", "K", 100,
            new ResourceCost((ResourceType.Wood, 50), (ResourceType.Gold, 40), (ResourceType.Copper, 10)), 35, [Tech.TheWheel, Tech.Archery], 6, 2, 30, 1.5, true),
        [BrigadeType.IronInfantry] = new("Infantería de hierro", "H", 100,
            new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 30), (ResourceType.Iron, 20)), 30, [Tech.IronWorking], 5, 7, 40, 1, false),
    };

    public static BrigadeInfo Info(this BrigadeType type) => Table[type];
}

/// <summary>One brigade of a division: how many men it has left and how much fight is left in them.</summary>
public sealed class Brigade
{
    public BrigadeType Type { get; }
    public double Strength { get; set; }
    public double Organisation { get; set; }

    public Brigade(BrigadeType type)
    {
        Type = type;
        Strength = type.Info().Men;
        Organisation = type.Info().MaxOrganisation;
    }

    public BrigadeInfo Info => Type.Info();
    /// <summary>0..1 of full strength.</summary>
    public double StrengthShare => Strength / Info.Men;
    /// <summary>0..1 of full organisation.</summary>
    public double OrganisationShare => Organisation / Info.MaxOrganisation;
}
