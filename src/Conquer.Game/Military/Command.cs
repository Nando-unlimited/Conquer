using Conquer.Game.Economy;

namespace Conquer.Game.Military;

/// <summary>
/// The chain of command, as in Hearts of Iron III: divisions report to a corps HQ, corps to an army,
/// armies to an army group and army groups to a theatre. Level 0 is the division itself.
/// </summary>
/// <param name="Level">1 corps … 4 theatre.</param>
/// <param name="RangeKm">An HQ commands its subordinates only while they are this close.</param>
/// <param name="MaxSubordinates">Units of the level below it can command.</param>
/// <param name="Staff">Citizens that form the HQ.</param>
public sealed record CommandLevelInfo(int Level, string Name, string Symbol, double RangeKm, int MaxSubordinates, int Staff, ResourceCost Cost, int TrainingDays);

public static class CommandLevels
{
    public const int Division = 0;
    public const int Highest = 4;

    private static readonly CommandLevelInfo[] Table =
    [
        new(1, "Cuerpo", "XXX", 300, 5, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 30)), 20),
        new(2, "Ejército", "XXXX", 600, 5, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 60)), 25),
        new(3, "Grupo de ejércitos", "XXXXX", 1200, 5, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 90)), 30),
        new(4, "Teatro", "T", 2500, 5, 50, new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 120)), 35),
    ];

    public static IReadOnlyList<CommandLevelInfo> All => Table;

    public static CommandLevelInfo Info(int level) => Table[level - 1];

    /// <summary>What a unit of this level is called: "división" or its HQ's name.</summary>
    public static string NameOf(int level) => level == Division ? "División" : Info(level).Name;
}

/// <summary>Something a city is training: a brigade, or an HQ of the given level.</summary>
public sealed class TrainingOrder
{
    public BrigadeType? Brigade { get; }
    public int HeadquartersLevel { get; }
    public int DaysLeft { get; set; }
    public int TotalDays { get; }

    public TrainingOrder(BrigadeType brigade)
    {
        Brigade = brigade;
        DaysLeft = TotalDays = brigade.Info().TrainingDays;
    }

    public TrainingOrder(int headquartersLevel)
    {
        HeadquartersLevel = headquartersLevel;
        DaysLeft = TotalDays = CommandLevels.Info(headquartersLevel).TrainingDays;
    }

    public string Name => Brigade is BrigadeType b ? b.Info().Name : $"Cuartel general de {CommandLevels.Info(HeadquartersLevel).Name.ToLowerInvariant()}";
}

/// <summary>
/// A fight for a province: divisions attacking it from their neighbouring provinces against the
/// enemy divisions inside. It is resolved hour by hour until one side runs out of divisions.
/// </summary>
public sealed class Battle
{
    public int ProvinceId { get; }
    public int AttackerId { get; }
    public int DefenderId { get; }
    /// <summary>Divisions attacking; they stay in their own provinces until they win.</summary>
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
