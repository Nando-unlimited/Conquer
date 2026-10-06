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
    /// <summary>The model each battalion was given the equipment of, in order (one for a lone battalion); empty for an HQ.</summary>
    public IReadOnlyList<int> Models { get; } = [];
    public int HeadquartersLevel { get; }
    public int DaysLeft { get; set; }
    public int TotalDays { get; }

    /// <param name="days">Days it takes the nation (see <see cref="Simulation.GameSession.TrainingDays(Entities.Player, BattalionType)"/>).</param>
    /// <param name="model">The model whose equipment it was given.</param>
    public TrainingOrder(BattalionType battalion, int days, int model = 0)
    {
        Battalion = battalion;
        Models = [model];
        DaysLeft = TotalDays = days;
    }

    public TrainingOrder(RegimentTemplate template, int days, IReadOnlyList<int>? models = null)
    {
        TemplateName = template.Name;
        TemplateBattalions = [.. template.Battalions];
        Models = [.. models ?? []];
        DaysLeft = TotalDays = days;
    }

    public TrainingOrder(int headquartersLevel)
    {
        HeadquartersLevel = headquartersLevel;
        DaysLeft = TotalDays = CommandLevels.Info(headquartersLevel).TrainingDays;
    }

    /// <summary>An order as it was saved, part-way through its training.</summary>
    internal TrainingOrder(BattalionType? battalion, string? templateName, IReadOnlyList<BattalionType> templateBattalions,
        int headquartersLevel, int daysLeft, int totalDays, IReadOnlyList<int>? models = null)
    {
        Battalion = battalion;
        TemplateName = templateName;
        TemplateBattalions = [.. templateBattalions];
        Models = [.. models ?? []];
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

/// <summary>
/// A ship in the nation's construction queue: its line and the model ordered, the port it was ordered from (if any),
/// the port whose slip builds it once one takes it, and the days of work done so far.
/// </summary>
public sealed class ShipOrder
{
    public required int Id { get; init; }
    public required BattalionType Type { get; init; }
    public required int Model { get; init; }
    /// <summary>A batch of <see cref="Rules.MilitaryRules.ConvoysPerOrder"/> convoys rather than a ship (its type and model are then unused).</summary>
    public bool Convoys { get; init; }
    public int? PreferredPortId { get; init; }
    public int? PortId { get; set; }
    public double DaysDone { get; set; }
    /// <summary>The nation has been told the finished ship waits for its crew.</summary>
    public bool WaitingForCrew { get; set; }

    public BattalionInfo Info => Convoys ? Battalions.ConvoyBatch : Type.Models()[Model];
    public double Progress => Math.Min(1, DaysDone / Info.TrainingDays);
}
