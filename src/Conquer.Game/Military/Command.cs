using Conquer.Game.Economy;

namespace Conquer.Game.Military;

/// <summary>
/// The chain of command, as in Hearts of Iron III: regiments report to a brigade HQ, brigades to a
/// division, divisions to a corps, corps to an army and armies to an army group. Level 0 is the
/// regiment itself. Names depend on the era (<see cref="Formations"/>).
/// </summary>
/// <param name="Level">1 brigade … 5 army group.</param>
/// <param name="RangeKm">An HQ commands its subordinates only while they are this close.</param>
/// <param name="MaxSubordinates">Units of the level below it can command.</param>
/// <param name="Staff">Citizens that form the HQ.</param>
public sealed record CommandLevelInfo(int Level, string Symbol, double RangeKm, int MaxSubordinates, int Staff, ResourceCost Cost, int TrainingDays);

public static class CommandLevels
{
    public const int Regiment = 0;
    public const int Highest = 5;

    private static readonly CommandLevelInfo[] Table =
    [
        new(1, "X", 150, 4, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 20)), 15),
        new(2, "XX", 300, 4, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 40)), 20),
        new(3, "XXX", 600, 5, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 60)), 25),
        new(4, "XXXX", 1200, 5, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 90)), 30),
        new(5, "XXXXX", 2500, 5, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 120)), 35),
    ];

    public static IReadOnlyList<CommandLevelInfo> All => Table;

    public static CommandLevelInfo Info(int level) => Table[level - 1];
}

/// <summary>Something a city is training: a battalion, a whole regiment from a template, or an HQ of the given level.</summary>
public sealed class TrainingOrder
{
    public BattalionType? Battalion { get; }
    /// <summary>A whole regiment trained from a template: its name and battalions.</summary>
    public string? TemplateName { get; }
    public IReadOnlyList<BattalionType> TemplateBattalions { get; } = [];
    public int HeadquartersLevel { get; }
    public int DaysLeft { get; set; }
    public int TotalDays { get; }

    public TrainingOrder(BattalionType battalion)
    {
        Battalion = battalion;
        DaysLeft = TotalDays = battalion.Info().TrainingDays;
    }

    public TrainingOrder(RegimentTemplate template)
    {
        TemplateName = template.Name;
        TemplateBattalions = [.. template.Battalions];
        DaysLeft = TotalDays = template.TrainingDays;
    }

    public TrainingOrder(int headquartersLevel)
    {
        HeadquartersLevel = headquartersLevel;
        DaysLeft = TotalDays = CommandLevels.Info(headquartersLevel).TrainingDays;
    }

    /// <summary>An order as it was saved, part-way through its training.</summary>
    internal TrainingOrder(BattalionType? battalion, string? templateName, IReadOnlyList<BattalionType> templateBattalions,
        int headquartersLevel, int daysLeft, int totalDays)
    {
        Battalion = battalion;
        TemplateName = templateName;
        TemplateBattalions = [.. templateBattalions];
        HeadquartersLevel = headquartersLevel;
        DaysLeft = daysLeft;
        TotalDays = totalDays;
    }

    /// <summary>"Cohorte de arqueros", "Legión (Plantilla II)", "Cuartel general de vexilación"… in the era's names.</summary>
    public string Name(ArmyEra era) =>
        Battalion is BattalionType b ? Formations.BattalionName(b.Info(), era)
        : TemplateName != null ? $"{Formations.LevelName(CommandLevels.Regiment, era)} ({TemplateName})"
        : $"Cuartel general de {Formations.LevelName(HeadquartersLevel, era).ToLowerInvariant()}";
}

/// <summary>
/// A fight for a province: regiments attacking it from their neighbouring provinces against the
/// enemy regiments inside. It is resolved hour by hour until one side runs out of regiments.
/// </summary>
public sealed class Battle
{
    public int ProvinceId { get; }
    public int AttackerId { get; }
    public int DefenderId { get; }
    /// <summary>Regiments attacking; they stay in their own provinces until they win.</summary>
    public List<int> Attackers { get; } = [];
    public long StartHours { get; }

    public Battle(int provinceId, int attackerId, int defenderId, long startHours)
    {
        ProvinceId = provinceId;
        AttackerId = attackerId;
        DefenderId = defenderId;
        StartHours = startHours;
    }
}
