namespace Conquer.Game.Rules;

/// <summary>
/// Improvements to the normal rules. Advances apply them to the whole nation and buildings to their
/// province; they all add up. Shares are added to the normal amount (0.2 = +20 %).
/// </summary>
public sealed record Modifiers
{
    public double Food { get; init; }
    public double Wood { get; init; }
    /// <summary>Output of deposits (their pockets empty faster too).</summary>
    public double Deposits { get; init; }
    /// <summary>Gold from taxes.</summary>
    public double Taxes { get; init; }
    public double Science { get; init; }
    /// <summary>Citizens the land can feed.</summary>
    public double Capacity { get; init; }
    public double Fertility { get; init; }
    /// <summary>Mood points.</summary>
    public double Mood { get; init; }
    /// <summary>Share of hunger deaths avoided.</summary>
    public double FamineSurvival { get; init; }

    public static readonly Modifiers None = new();

    public static Modifiers operator +(Modifiers a, Modifiers b) => new()
    {
        Food = a.Food + b.Food,
        Wood = a.Wood + b.Wood,
        Deposits = a.Deposits + b.Deposits,
        Taxes = a.Taxes + b.Taxes,
        Science = a.Science + b.Science,
        Capacity = a.Capacity + b.Capacity,
        Fertility = a.Fertility + b.Fertility,
        Mood = a.Mood + b.Mood,
        FamineSurvival = a.FamineSurvival + b.FamineSurvival,
    };
}
