using Conquer.Game.Economy;

namespace Conquer.Game.Military;

/// <summary>
/// The chain of command: combat units (regiments, brigades and divisions, level 0) report to a corps HQ,
/// corps to an army and armies to an army group (<see cref="Formations"/> names them).
/// </summary>
/// <param name="Level">1 corps, 2 army, 3 army group.</param>
/// <param name="RangeKm">An HQ commands its subordinates only while they are this close.</param>
/// <param name="MaxSubordinates">Units of the level below it can command.</param>
/// <param name="Staff">Citizens that form the HQ.</param>
public sealed record CommandLevelInfo(int Level, string Symbol, double RangeKm, int MaxSubordinates, int Staff, ResourceCost Cost, int TrainingDays);

public static class CommandLevels
{
    /// <summary>The level of combat units.</summary>
    public const int Combat = 0;
    public const int Highest = 3;

    private static readonly CommandLevelInfo[] Table =
    [
        new(1, "XXX", 600, 5, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 60)), 25),
        new(2, "XXXX", 1200, 5, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 90)), 30),
        new(3, "XXXXX", 2500, 5, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 120)), 35),
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

    /// <param name="days">Days it takes the nation (see <see cref="Simulation.GameSession.TrainingDays(Entities.Player, BattalionType)"/>).</param>
    public TrainingOrder(BattalionType battalion, int days)
    {
        Battalion = battalion;
        DaysLeft = TotalDays = days;
    }

    public TrainingOrder(RegimentTemplate template, int days)
    {
        TemplateName = template.Name;
        TemplateBattalions = [.. template.Battalions];
        DaysLeft = TotalDays = days;
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

    /// <summary>"Batallón de arqueros", "Brigada (Plantilla II)", "Cuartel general de cuerpo".</summary>
    public string Name =>
        Battalion is BattalionType b ? Formations.BattalionName(b)
        : TemplateName != null ? $"{Formations.CombatName(Echelon.Regiment)} ({TemplateName})"
        : $"Cuartel general de {Formations.LevelName(HeadquartersLevel).ToLowerInvariant()}";
}

/// <summary>
/// A fight for a province: combat units attacking it from their neighbouring provinces against the
/// enemy units inside. It is resolved hour by hour until one side runs out of units.
/// </summary>
public sealed class Battle
{
    public int ProvinceId { get; }
    public int AttackerId { get; }
    public int DefenderId { get; }
    /// <summary>Units attacking; they stay in their own provinces until they win.</summary>
    public List<int> Attackers { get; } = [];
    public long StartHours { get; }
    /// <summary>Men each side has lost so far.</summary>
    public double AttackerLosses { get; internal set; }
    public double DefenderLosses { get; internal set; }
    /// <summary>How each hour of the fight went, oldest first (not saved: a loaded battle starts a new record).</summary>
    public List<BattleHour> History { get; } = [];
    /// <summary>Set when it ends: whether the attackers took the province.</summary>
    public bool? AttackersWon { get; internal set; }
    public long? EndHours { get; internal set; }

    public Battle(int provinceId, int attackerId, int defenderId, long startHours)
    {
        ProvinceId = provinceId;
        AttackerId = attackerId;
        DefenderId = defenderId;
        StartHours = startHours;
    }
}

/// <summary>
/// One hour of a battle, after the fire landed: the men and average organisation (0-1) left on each side and the
/// fire each side dealt.
/// </summary>
public readonly record struct BattleHour(
    double AttackerMen, double DefenderMen, double AttackerOrganisation, double DefenderOrganisation, double AttackerFire, double DefenderFire);
