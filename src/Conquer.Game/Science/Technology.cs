namespace Conquer.Game.Science;

/// <summary>The advances a nation can research. The order is the order they are listed in.</summary>
public enum Tech
{
    Agriculture,
    Carpentry,
    Mining,
    Writing,
    Mythology,
    Irrigation,
    Medicine,
    Currency,
}

/// <summary>
/// What an advance improves for its owner. Shares are added to the normal amount (0.2 = +20 %);
/// the effects of every known advance add up.
/// </summary>
public sealed record TechEffects
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
    /// <summary>Mood points in every province.</summary>
    public double Mood { get; init; }
    /// <summary>Share of hunger deaths avoided.</summary>
    public double FamineSurvival { get; init; }

    public static TechEffects operator +(TechEffects a, TechEffects b) => new()
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

/// <param name="Cost">Science points needed to discover it.</param>
/// <param name="Requires">Advances that must be known before researching it.</param>
public sealed record TechInfo(string Name, double Cost, Tech[] Requires, string Description, TechEffects Effects);

public static class Techs
{
    public static readonly Tech[] All = Enum.GetValues<Tech>();

    private static readonly Dictionary<Tech, TechInfo> Table = new()
    {
        [Tech.Agriculture] = new("Agricultura", 60, [], "+20 % de comida.", new() { Food = 0.2 }),
        [Tech.Carpentry] = new("Carpintería", 60, [], "+50 % de madera.", new() { Wood = 0.5 }),
        [Tech.Mining] = new("Minería", 100, [], "+50 % de producción de los yacimientos (se agotan antes).", new() { Deposits = 0.5 }),
        [Tech.Writing] = new("Escritura", 80, [], "+30 % de ciencia.", new() { Science = 0.3 }),
        [Tech.Mythology] = new("Mitología", 100, [], "+5 de humor en todas tus provincias.", new() { Mood = 5 }),
        [Tech.Irrigation] = new("Irrigación", 250, [Tech.Agriculture], "La tierra alimenta un 25 % más de gente.", new() { Capacity = 0.25 }),
        [Tech.Medicine] = new("Medicina", 250, [Tech.Writing], "+10 % de fertilidad y el hambre mata la mitad.", new() { Fertility = 0.1, FamineSurvival = 0.5 }),
        [Tech.Currency] = new("Moneda", 300, [Tech.Writing, Tech.Mining], "+30 % de oro de los impuestos.", new() { Taxes = 0.3 }),
    };

    public static TechInfo Info(this Tech tech) => Table[tech];
}
