using Conquer.Game.Economy;
using Conquer.Game.Rules;

namespace Conquer.Game.Science;

/// <summary>The three lines research advances along. Each has levels of one to three advances to choose from, as in Civilization.</summary>
public enum TechBranch
{
    Economy,
    Society,
    Military,
}

/// <summary>
/// The advances a nation can research. Each belongs to a branch and a level in it (<see cref="TechInfo"/>).
/// New ones go at the end: saved games keep research progress by position.
/// </summary>
public enum Tech
{
    Agriculture,
    Carpentry,
    Mining,
    BronzeWorking,
    IronWorking,
    HorsebackRiding,
    TheWheel,
    Archery,
    Writing,
    Mythology,
    Irrigation,
    Medicine,
    Currency,
    Pottery,
    CodeOfLaws,
}

/// <param name="Level">Its level in its branch, from 1; a level opens once enough of the one below is known (<see cref="GameRules.LevelUnlockShare"/>).</param>
/// <param name="Requires">Advances it also needs, from its own branch or another.</param>
/// <param name="Reveals">Resources its owner can see and mine from then on.</param>
public sealed record TechInfo(string Name, TechBranch Branch, int Level, string Description, Modifiers Effects,
    Tech[]? Requires = null, ResourceType[]? Reveals = null)
{
    public Tech[] Requires { get; } = Requires ?? [];
    public ResourceType[] Reveals { get; } = Reveals ?? [];
    /// <summary>Science points needed to discover it, before any discount; the same for every branch at a level.</summary>
    public double Cost => Techs.LevelCost(Level);
}

public static class Techs
{
    public static readonly Tech[] All = Enum.GetValues<Tech>();
    public static readonly TechBranch[] Branches = Enum.GetValues<TechBranch>();

    /// <summary>What each level costs. <b>Balance research speed here.</b></summary>
    private static readonly double[] LevelCosts = [70, 160, 300];

    public static double LevelCost(int level) => LevelCosts[Math.Min(level, LevelCosts.Length) - 1];

    private static readonly Dictionary<Tech, TechInfo> Table = new()
    {
        [Tech.Agriculture] = new("Agricultura", TechBranch.Economy, 1, "+20 % de comida.", new() { Food = 0.2 }),
        [Tech.Carpentry] = new("Carpintería", TechBranch.Economy, 1, "+50 % de madera.", new() { Wood = 0.5 }),
        [Tech.Mining] = new("Minería", TechBranch.Economy, 2, "Descubre el carbón; yacimientos +50 % (se agotan antes).",
            new() { Deposits = 0.5 }, Reveals: [ResourceType.Coal]),
        [Tech.Irrigation] = new("Irrigación", TechBranch.Economy, 2, "La tierra alimenta un 25 % más de gente.", new() { Capacity = 0.25 }, [Tech.Agriculture]),
        [Tech.Currency] = new("Moneda", TechBranch.Economy, 3, "+30 % de oro de los impuestos.", new() { Taxes = 0.3 }, [Tech.Writing, Tech.Mining]),

        [Tech.Writing] = new("Escritura", TechBranch.Society, 1, "+30 % de ciencia.", new() { Science = 0.3 }),
        [Tech.Mythology] = new("Mitología", TechBranch.Society, 1, "+5 de humor en todas tus provincias.", new() { Mood = 5 }),
        [Tech.Pottery] = new("Alfarería", TechBranch.Society, 2, "Vasijas para guardar el grano.", Modifiers.None),
        [Tech.Medicine] = new("Medicina", TechBranch.Society, 2, "+10 % de fertilidad y el hambre mata la mitad.", new() { Fertility = 0.1, FamineSurvival = 0.5 }, [Tech.Writing]),
        [Tech.CodeOfLaws] = new("Código de leyes", TechBranch.Society, 3, "+10 % de oro de los impuestos.", new() { Taxes = 0.1 }),

        [Tech.Archery] = new("Tiro con arco", TechBranch.Military, 1, "Arqueros a pie y, con la rueda, en carro.", Modifiers.None),
        [Tech.HorsebackRiding] = new("Doma del caballo", TechBranch.Military, 1, "Guerreros a caballo, más rápidos.", Modifiers.None),
        [Tech.BronzeWorking] = new("Trabajo del bronce", TechBranch.Military, 2, "Armas y corazas de bronce.", Modifiers.None, [Tech.Mining]),
        [Tech.TheWheel] = new("La rueda", TechBranch.Military, 2, "Carros de guerra tirados por caballos.", Modifiers.None, [Tech.HorsebackRiding]),
        [Tech.IronWorking] = new("Trabajo del hierro", TechBranch.Military, 3, "Descubre el hierro.", Modifiers.None, [Tech.BronzeWorking], [ResourceType.Iron]),
    };

    public static TechInfo Info(this Tech tech) => Table[tech];

    /// <summary>The advances of a branch, level by level.</summary>
    public static IEnumerable<Tech> InBranch(TechBranch branch) => All.Where(t => t.Info().Branch == branch).OrderBy(t => t.Info().Level);

    public static IEnumerable<Tech> InLevel(TechBranch branch, int level) => InBranch(branch).Where(t => t.Info().Level == level);

    /// <summary>How many levels a branch has.</summary>
    public static int Levels(TechBranch branch) => InBranch(branch).Max(t => t.Info().Level);

    /// <summary>Advances of a level that must be known to open the next one: <see cref="GameRules.LevelUnlockShare"/> of them, at least one.</summary>
    public static int NeededToOpenNext(TechBranch branch, int level) =>
        Math.Max(1, (int)Math.Ceiling(InLevel(branch, level).Count() * GameRules.LevelUnlockShare));

    public static string Name(this TechBranch branch) => branch switch
    {
        TechBranch.Economy => "Economía",
        TechBranch.Society => "Sociedad",
        TechBranch.Military => "Militar",
        _ => branch.ToString(),
    };
}
