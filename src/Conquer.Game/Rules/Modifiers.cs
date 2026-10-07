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
    /// <summary>Damage dealt by those defending the province (walls and castles).</summary>
    public double Defense { get; init; }
    /// <summary>How fast buildings and cities go up: 0.25 finishes them in 1/1.25 of the days.</summary>
    public double BuildSpeed { get; init; }
    /// <summary>Share of the mood lost to distance from the capital that is avoided.</summary>
    public double DistanceMood { get; init; }
    /// <summary>Size of the nation's reserve of recruits.</summary>
    public double Manpower { get; init; }
    /// <summary>Share of the deaths and the spread of epidemics avoided.</summary>
    public double PlagueResistance { get; init; }

    public static readonly Modifiers None = new();

    /// <summary>Every improvement times <paramref name="share"/>.</summary>
    public Modifiers Times(double share) => share == 1 ? this : new()
    {
        Food = Food * share, Wood = Wood * share, Deposits = Deposits * share, Taxes = Taxes * share, Science = Science * share,
        Capacity = Capacity * share, Fertility = Fertility * share, Mood = Mood * share, FamineSurvival = FamineSurvival * share,
        Defense = Defense * share, BuildSpeed = BuildSpeed * share, DistanceMood = DistanceMood * share, Manpower = Manpower * share,
        PlagueResistance = PlagueResistance * share,
    };

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
        Defense = a.Defense + b.Defense,
        BuildSpeed = a.BuildSpeed + b.BuildSpeed,
        DistanceMood = a.DistanceMood + b.DistanceMood,
        Manpower = a.Manpower + b.Manpower,
        PlagueResistance = a.PlagueResistance + b.PlagueResistance,
    };
}
