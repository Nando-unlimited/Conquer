using Conquer.Game.Economy;
using Conquer.Game.Rules;

namespace Conquer.Game.Science;

/// <summary>The advances a nation can research. The order is the order they are listed in.</summary>
public enum Tech
{
    Agriculture,
    Carpentry,
    Mining,
    IronWorking,
    Writing,
    Mythology,
    Irrigation,
    Medicine,
    Currency,
}

/// <param name="Cost">Science points needed to discover it.</param>
/// <param name="Requires">Advances that must be known before researching it.</param>
/// <param name="Reveals">Resources its owner can see and mine from then on.</param>
public sealed record TechInfo(string Name, double Cost, Tech[] Requires, string Description, Modifiers Effects, ResourceType[]? Reveals = null)
{
    public ResourceType[] Reveals { get; } = Reveals ?? [];
}

public static class Techs
{
    public static readonly Tech[] All = Enum.GetValues<Tech>();

    private static readonly Dictionary<Tech, TechInfo> Table = new()
    {
        [Tech.Agriculture] = new("Agricultura", 60, [], "+20 % de comida.", new() { Food = 0.2 }),
        [Tech.Carpentry] = new("Carpintería", 60, [], "+50 % de madera.", new() { Wood = 0.5 }),
        [Tech.Mining] = new("Minería", 100, [], "Descubre el carbón. +50 % de producción de los yacimientos (se agotan antes).",
            new() { Deposits = 0.5 }, [ResourceType.Coal]),
        [Tech.IronWorking] = new("Trabajo del hierro", 200, [Tech.Mining], "Descubre el hierro.", Modifiers.None, [ResourceType.Iron]),
        [Tech.Writing] = new("Escritura", 80, [], "+30 % de ciencia.", new() { Science = 0.3 }),
        [Tech.Mythology] = new("Mitología", 100, [], "+5 de humor en todas tus provincias.", new() { Mood = 5 }),
        [Tech.Irrigation] = new("Irrigación", 250, [Tech.Agriculture], "La tierra alimenta un 25 % más de gente.", new() { Capacity = 0.25 }),
        [Tech.Medicine] = new("Medicina", 250, [Tech.Writing], "+10 % de fertilidad y el hambre mata la mitad.", new() { Fertility = 0.1, FamineSurvival = 0.5 }),
        [Tech.Currency] = new("Moneda", 300, [Tech.Writing, Tech.Mining], "+30 % de oro de los impuestos.", new() { Taxes = 0.3 }),
    };

    public static TechInfo Info(this Tech tech) => Table[tech];
}
