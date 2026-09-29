using Conquer.Game.Economy;
using Conquer.Game.Rules;

namespace Conquer.Game.Science;

/// <summary>The ages of history. Each after the first opens with an institution (<see cref="Institution"/>).</summary>
public enum Era
{
    Ancient,
    Classical,
    Medieval,
    Renaissance,
    Industrial,
    Modern,
}

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
    Trade,
    Construction,
    Engineering,
    Philosophy,
    Mathematics,
    DramaAndPoetry,
    MilitaryTactics,
    SiegeEngines,
    HeavyCavalry,
    Fortifications,
    Administration,
    CropRotation,
    Guilds,
    Banking,
    Theology,
    Education,
    Astronomy,
    Stirrup,
    Machinery,
    Castles,
}

/// <param name="Level">Its level in its branch, from 1; a level opens once enough of the one below is known (<see cref="GameRules.LevelUnlockShare"/>).</param>
/// <param name="Requires">Advances it also needs, from its own branch or another.</param>
/// <param name="Reveals">Resources its owner can see and mine from then on.</param>
/// <param name="Era">The age it belongs to; it costs more until the nation adopts that age's institution.</param>
public sealed record TechInfo(string Name, TechBranch Branch, int Level, string Description, Modifiers Effects,
    Tech[]? Requires = null, ResourceType[]? Reveals = null, Era Era = Era.Ancient)
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
    private static readonly double[] LevelCosts = [70, 160, 300, 500, 750, 1100, 1600];

    public static double LevelCost(int level) => LevelCosts[Math.Min(level, LevelCosts.Length) - 1];

    private static readonly Dictionary<Tech, TechInfo> Table = new()
    {
        [Tech.Agriculture] = new("Agricultura", TechBranch.Economy, 1, "+20 % de comida.", new() { Food = 0.2 }),
        [Tech.Carpentry] = new("Carpintería", TechBranch.Economy, 1, "+50 % de madera.", new() { Wood = 0.5 }),
        [Tech.Mining] = new("Minería", TechBranch.Economy, 2, "Descubre el carbón; yacimientos +50 % (se agotan antes).",
            new() { Deposits = 0.5 }, Reveals: [ResourceType.Coal]),
        [Tech.Irrigation] = new("Irrigación", TechBranch.Economy, 2, "La tierra alimenta un 25 % más de gente.", new() { Capacity = 0.25 }, [Tech.Agriculture]),
        [Tech.Currency] = new("Moneda", TechBranch.Economy, 3, "+30 % de oro de los impuestos.", new() { Taxes = 0.3 }, [Tech.Writing, Tech.Mining]),
        [Tech.Trade] = new("Comercio", TechBranch.Economy, 4, "+15 % de oro de los impuestos.", new() { Taxes = 0.15 }, [Tech.Currency], Era: Era.Classical),
        [Tech.Construction] = new("Construcción", TechBranch.Economy, 4, "Las obras se terminan un 25 % antes.", new() { BuildSpeed = 0.25 }, Era: Era.Classical),
        [Tech.Engineering] = new("Ingeniería", TechBranch.Economy, 5, "Canales: la tierra alimenta un 15 % más de gente.", new() { Capacity = 0.15 },
            [Tech.Construction, Tech.Mathematics], Era: Era.Classical),
        [Tech.CropRotation] = new("Rotación de cultivos", TechBranch.Economy, 6, "+20 % de comida y la tierra alimenta un 10 % más de gente.",
            new() { Food = 0.2, Capacity = 0.1 }, [Tech.Irrigation], Era: Era.Medieval),
        [Tech.Guilds] = new("Gremios", TechBranch.Economy, 6, "+20 % de madera y de yacimientos.", new() { Wood = 0.2, Deposits = 0.2 }, [Tech.Trade], Era: Era.Medieval),
        [Tech.Banking] = new("Banca", TechBranch.Economy, 7, "Bancos para las ciudades.", Modifiers.None, [Tech.Guilds], Era: Era.Medieval),

        [Tech.Writing] = new("Escritura", TechBranch.Society, 1, "+30 % de ciencia.", new() { Science = 0.3 }),
        [Tech.Mythology] = new("Mitología", TechBranch.Society, 1, "+5 de humor en todas tus provincias.", new() { Mood = 5 }),
        [Tech.Pottery] = new("Alfarería", TechBranch.Society, 2, "Vasijas para guardar el grano.", Modifiers.None),
        [Tech.Medicine] = new("Medicina", TechBranch.Society, 2, "+10 % de fertilidad y el hambre mata la mitad.", new() { Fertility = 0.1, FamineSurvival = 0.5 }, [Tech.Writing]),
        [Tech.CodeOfLaws] = new("Código de leyes", TechBranch.Society, 3, "+10 % de oro de los impuestos.", new() { Taxes = 0.1 }),
        [Tech.Philosophy] = new("Filosofía", TechBranch.Society, 4, "+20 % de ciencia y +3 de humor.", new() { Science = 0.2, Mood = 3 }, [Tech.Mythology], Era: Era.Classical),
        [Tech.Mathematics] = new("Matemáticas", TechBranch.Society, 4, "+15 % de ciencia.", new() { Science = 0.15 }, [Tech.Writing], Era: Era.Classical),
        [Tech.DramaAndPoetry] = new("Drama y poesía", TechBranch.Society, 5, "+5 de humor en todas tus provincias.", new() { Mood = 5 }, [Tech.Philosophy], Era: Era.Classical),
        [Tech.Administration] = new("Administración", TechBranch.Society, 5, "Gobernadores: la lejanía de la capital resta la mitad de humor.", new() { DistanceMood = 0.5 },
            [Tech.CodeOfLaws], Era: Era.Classical),
        [Tech.Theology] = new("Teología", TechBranch.Society, 6, "+5 de humor en todas tus provincias.", new() { Mood = 5 }, [Tech.Philosophy], Era: Era.Medieval),
        [Tech.Education] = new("Educación", TechBranch.Society, 6, "Universidades para las ciudades.", Modifiers.None, [Tech.Philosophy], Era: Era.Medieval),
        [Tech.Astronomy] = new("Astronomía", TechBranch.Society, 7, "+20 % de ciencia.", new() { Science = 0.2 }, [Tech.Education, Tech.Mathematics], Era: Era.Medieval),

        [Tech.Archery] = new("Tiro con arco", TechBranch.Military, 1, "Arqueros a pie y, con la rueda, en carro.", Modifiers.None),
        [Tech.HorsebackRiding] = new("Doma del caballo", TechBranch.Military, 1, "Guerreros a caballo, más rápidos.", Modifiers.None),
        [Tech.BronzeWorking] = new("Trabajo del bronce", TechBranch.Military, 2, "Armas y corazas de bronce.", Modifiers.None, [Tech.Mining]),
        [Tech.TheWheel] = new("La rueda", TechBranch.Military, 2, "Carros de guerra tirados por caballos.", Modifiers.None, [Tech.HorsebackRiding]),
        [Tech.IronWorking] = new("Trabajo del hierro", TechBranch.Military, 3, "Descubre el hierro.", Modifiers.None, [Tech.BronzeWorking], [ResourceType.Iron]),
        [Tech.MilitaryTactics] = new("Tácticas militares", TechBranch.Military, 4, "Legiones de infantería pesada.", Modifiers.None, Era: Era.Classical),
        [Tech.SiegeEngines] = new("Maquinaria de asedio", TechBranch.Military, 4, "Catapultas que rompen las defensas.", Modifiers.None, [Tech.Mathematics], Era: Era.Classical),
        [Tech.HeavyCavalry] = new("Caballería pesada", TechBranch.Military, 5, "Jinetes con armadura.", Modifiers.None,
            [Tech.HorsebackRiding, Tech.MilitaryTactics], Era: Era.Classical),
        [Tech.Fortifications] = new("Fortificaciones", TechBranch.Military, 5, "Murallas que protegen las ciudades.", Modifiers.None, [Tech.Construction], Era: Era.Classical),
        [Tech.Stirrup] = new("Estribo", TechBranch.Military, 6, "Caballeros: la caballería pesada carga con lanza.", Modifiers.None,
            [Tech.HeavyCavalry], Era: Era.Medieval),
        [Tech.Machinery] = new("Maquinaria", TechBranch.Military, 6, "Ballestas que atraviesan armaduras.", Modifiers.None, [Tech.Mathematics], Era: Era.Medieval),
        [Tech.Castles] = new("Castillos", TechBranch.Military, 7, "Castillos para las ciudades.", Modifiers.None, [Tech.Fortifications], Era: Era.Medieval),
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

    public static string Name(this Era era) => era switch
    {
        Era.Ancient => "Antigüedad",
        Era.Classical => "Clásica",
        Era.Medieval => "Medieval",
        Era.Renaissance => "Renacimiento",
        Era.Industrial => "Industrial",
        Era.Modern => "Moderna",
        _ => era.ToString(),
    };
}
