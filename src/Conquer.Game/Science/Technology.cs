using Conquer.Game.Economy;
using Conquer.Game.Military;
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
    Economics,
    DeepMining,
    Cartography,
    PrintingPress,
    Anatomy,
    ScientificMethod,
    Gunpowder,
    Metallurgy,
    MilitaryScience,
    SteamEngine,
    Industrialization,
    Railroad,
    Chemistry,
    Sanitation,
    PublicEducation,
    Rifling,
    Steel,
    MachineGuns,
    OilRefining,
    Fertilizers,
    AssemblyLine,
    Electricity,
    Antibiotics,
    Electronics,
    Combustion,
    HeavyArtillery,
    Armour,
    Navigation,
    Aviation,
    NavalEngineering,
    ImprovedBows,
    HorseBreeding,
    Drill,
    SiegeWorkshops,
    Armouries,
    InterchangeableParts,
    Conscription,
    ArtilleryFoundries,
    WarProduction,
}

/// <param name="Level">Its level in its branch, from 1; a level opens once enough of the one below is known (<see cref="GameRules.LevelUnlockShare"/>).</param>
/// <param name="Requires">Advances it also needs, from its own branch or another.</param>
/// <param name="Reveals">Resources its owner can see and mine from then on.</param>
/// <param name="Era">The age it belongs to; it costs more until the nation adopts that age's institution.</param>
/// <param name="FasterTraining">Battalions its owner's barracks train faster from then on (<see cref="MilitaryRules.TechTrainingSpeed"/>).</param>
public sealed record TechInfo(string Name, TechBranch Branch, int Level, string Description, Modifiers Effects,
    Tech[]? Requires = null, ResourceType[]? Reveals = null, Era Era = Era.Ancient, BattalionType[]? FasterTraining = null)
{
    public Tech[] Requires { get; } = Requires ?? [];
    public ResourceType[] Reveals { get; } = Reveals ?? [];
    public BattalionType[] FasterTraining { get; } = FasterTraining ?? [];
    /// <summary>Science points needed to discover it, before any discount; the same for every branch at a level.</summary>
    public double Cost => Techs.LevelCost(Level);
}

public static class Techs
{
    public static readonly Tech[] All = Enum.GetValues<Tech>();
    public static readonly TechBranch[] Branches = Enum.GetValues<TechBranch>();

    /// <summary>What each level costs. <b>Balance research speed here.</b></summary>
    private static readonly double[] LevelCosts = [70, 160, 300, 500, 750, 1100, 1600, 2300, 3200, 4500, 6200, 8500, 12000];

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
        [Tech.Navigation] = new("Navegación a vela", TechBranch.Economy, 5, "Barcos que llevan a tus unidades por mares costeros y lagos.", Modifiers.None,
            [Tech.Trade], Era: Era.Classical),
        [Tech.CropRotation] = new("Rotación de cultivos", TechBranch.Economy, 6, "+20 % de comida y la tierra alimenta un 10 % más de gente.",
            new() { Food = 0.2, Capacity = 0.1 }, [Tech.Irrigation], Era: Era.Medieval),
        [Tech.Guilds] = new("Gremios", TechBranch.Economy, 6, "+20 % de madera y de yacimientos.", new() { Wood = 0.2, Deposits = 0.2 }, [Tech.Trade], Era: Era.Medieval),
        [Tech.Banking] = new("Banca", TechBranch.Economy, 7, "Bancos para las ciudades.", Modifiers.None, [Tech.Guilds], Era: Era.Medieval),
        [Tech.Economics] = new("Economía", TechBranch.Economy, 8, "+20 % de oro de los impuestos.", new() { Taxes = 0.2 }, [Tech.Banking], Era: Era.Renaissance),
        [Tech.DeepMining] = new("Minería profunda", TechBranch.Economy, 8, "Galerías y bombas: +30 % de yacimientos.", new() { Deposits = 0.3 }, [Tech.Guilds], Era: Era.Renaissance),
        [Tech.Cartography] = new("Cartografía", TechBranch.Economy, 9, "Rutas de comercio y barcos que cruzan el océano: +10 % de oro de los impuestos.", new() { Taxes = 0.1 },
            [Tech.Astronomy], Era: Era.Renaissance),
        [Tech.SteamEngine] = new("Máquina de vapor", TechBranch.Economy, 10, "Bombas y máquinas: +25 % de yacimientos.", new() { Deposits = 0.25 },
            [Tech.DeepMining], Era: Era.Industrial),
        [Tech.Industrialization] = new("Industrialización", TechBranch.Economy, 10, "Fábricas: los talleres pasan a serlo.", Modifiers.None, [Tech.Economics], Era: Era.Industrial),
        [Tech.Railroad] = new("Ferrocarril", TechBranch.Economy, 11, "Vías de tren que cruzan las provincias.", Modifiers.None, [Tech.SteamEngine], Era: Era.Industrial),
        [Tech.OilRefining] = new("Refinado del petróleo", TechBranch.Economy, 12, "Descubre el petróleo.", Modifiers.None, [Tech.Chemistry], [ResourceType.Oil], Era.Modern),
        [Tech.Fertilizers] = new("Fertilizantes", TechBranch.Economy, 12, "+30 % de comida y la tierra alimenta un 20 % más de gente.",
            new() { Food = 0.3, Capacity = 0.2 }, [Tech.CropRotation, Tech.Chemistry], Era: Era.Modern),
        [Tech.AssemblyLine] = new("Producción en cadena", TechBranch.Economy, 13, "+25 % de madera y de yacimientos.", new() { Wood = 0.25, Deposits = 0.25 },
            [Tech.Industrialization], Era: Era.Modern),
        [Tech.NavalEngineering] = new("Ingeniería naval", TechBranch.Economy, 13, "Diques secos para construir destructores y portaaviones.", Modifiers.None,
            [Tech.Steel, Tech.OilRefining], Era: Era.Modern),

        [Tech.Writing] = new("Escritura", TechBranch.Society, 1, "+30 % de ciencia.", new() { Science = 0.3 }),
        [Tech.Mythology] = new("Mitología", TechBranch.Society, 1, "+5 de moral en todas tus provincias.", new() { Mood = 5 }),
        [Tech.Pottery] = new("Alfarería", TechBranch.Society, 2, "Vasijas para guardar el grano.", Modifiers.None),
        [Tech.Medicine] = new("Medicina", TechBranch.Society, 2, "+10 % de fertilidad; el hambre mata la mitad y las epidemias un 25 % menos. Médicos para el ejército.", new() { Fertility = 0.1, FamineSurvival = 0.5, PlagueResistance = 0.25 }, [Tech.Writing]),
        [Tech.CodeOfLaws] = new("Código de leyes", TechBranch.Society, 3, "+10 % de oro de los impuestos.", new() { Taxes = 0.1 }),
        [Tech.Philosophy] = new("Filosofía", TechBranch.Society, 4, "+20 % de ciencia y +3 de moral.", new() { Science = 0.2, Mood = 3 }, [Tech.Mythology], Era: Era.Classical),
        [Tech.Mathematics] = new("Matemáticas", TechBranch.Society, 4, "+15 % de ciencia.", new() { Science = 0.15 }, [Tech.Writing], Era: Era.Classical),
        [Tech.DramaAndPoetry] = new("Drama y poesía", TechBranch.Society, 5, "+5 de moral en todas tus provincias.", new() { Mood = 5 }, [Tech.Philosophy], Era: Era.Classical),
        [Tech.Administration] = new("Administración", TechBranch.Society, 5, "Gobernadores: la lejanía de la capital resta la mitad de moral.", new() { DistanceMood = 0.5 },
            [Tech.CodeOfLaws], Era: Era.Classical),
        [Tech.Theology] = new("Teología", TechBranch.Society, 6, "+5 de moral en todas tus provincias.", new() { Mood = 5 }, [Tech.Philosophy], Era: Era.Medieval),
        [Tech.Education] = new("Educación", TechBranch.Society, 6, "Universidades para las ciudades.", Modifiers.None, [Tech.Philosophy], Era: Era.Medieval),
        [Tech.Astronomy] = new("Astronomía", TechBranch.Society, 7, "+20 % de ciencia.", new() { Science = 0.2 }, [Tech.Education, Tech.Mathematics], Era: Era.Medieval),
        [Tech.PrintingPress] = new("Imprenta", TechBranch.Society, 8, "+25 % de ciencia.", new() { Science = 0.25 }, [Tech.Education], Era: Era.Renaissance),
        [Tech.Anatomy] = new("Anatomía", TechBranch.Society, 8, "+15 % de fertilidad.", new() { Fertility = 0.15 }, [Tech.Medicine], Era: Era.Renaissance),
        [Tech.ScientificMethod] = new("Método científico", TechBranch.Society, 9, "+20 % de ciencia.", new() { Science = 0.2 },
            [Tech.PrintingPress, Tech.Astronomy], Era: Era.Renaissance),
        [Tech.Chemistry] = new("Química", TechBranch.Society, 10, "Descubre el caucho.", Modifiers.None, [Tech.ScientificMethod], [ResourceType.Rubber], Era.Industrial),
        [Tech.Sanitation] = new("Salubridad", TechBranch.Society, 10, "Hospitales para las ciudades; las epidemias matan y se extienden un 25 % menos.", new() { PlagueResistance = 0.25 }, [Tech.Anatomy], Era: Era.Industrial),
        [Tech.PublicEducation] = new("Educación pública", TechBranch.Society, 11, "+20 % de ciencia.", new() { Science = 0.2 }, [Tech.ScientificMethod], Era: Era.Industrial),
        [Tech.Electricity] = new("Electricidad", TechBranch.Society, 12, "Descubre el aluminio y da +20 % de ciencia.", new() { Science = 0.2 },
            [Tech.PublicEducation], [ResourceType.Aluminium], Era.Modern),
        [Tech.Antibiotics] = new("Antibióticos", TechBranch.Society, 12, "+20 % de fertilidad; el hambre mata la mitad y las epidemias un 40 % menos.", new() { Fertility = 0.2, FamineSurvival = 0.5, PlagueResistance = 0.4 },
            [Tech.Sanitation], Era: Era.Modern),
        [Tech.Electronics] = new("Electrónica", TechBranch.Society, 13, "Descubre el silicio y da +25 % de ciencia.", new() { Science = 0.25 },
            [Tech.Electricity], [ResourceType.Silicon], Era.Modern),

        [Tech.Archery] = new("Tiro con arco", TechBranch.Military, 1, "Arqueros.", Modifiers.None),
        [Tech.HorsebackRiding] = new("Doma del caballo", TechBranch.Military, 1, "Guerreros a caballo, más rápidos.", Modifiers.None),
        [Tech.BronzeWorking] = new("Trabajo del bronce", TechBranch.Military, 2, "Armas y corazas de bronce: espadachines.", Modifiers.None, [Tech.Mining]),
        [Tech.TheWheel] = new("La rueda", TechBranch.Military, 2, "Carros de guerra tirados por caballos.", Modifiers.None, [Tech.HorsebackRiding]),
        [Tech.IronWorking] = new("Trabajo del hierro", TechBranch.Military, 3, "Descubre el hierro.", Modifiers.None, [Tech.BronzeWorking], [ResourceType.Iron]),
        [Tech.MilitaryTactics] = new("Tácticas militares", TechBranch.Military, 4, "Vélites, falanges y tropas de montaña.", Modifiers.None, Era: Era.Classical),
        [Tech.SiegeEngines] = new("Maquinaria de asedio", TechBranch.Military, 4, "Catapultas que rompen las defensas, y talleres para construirlas.", Modifiers.None, [Tech.Mathematics], Era: Era.Classical),
        [Tech.HeavyCavalry] = new("Caballería pesada", TechBranch.Military, 5, "Jinetes con armadura.", Modifiers.None,
            [Tech.HorsebackRiding, Tech.MilitaryTactics], Era: Era.Classical),
        [Tech.Fortifications] = new("Fortificaciones", TechBranch.Military, 5, "Murallas que protegen las ciudades.", Modifiers.None, [Tech.Construction], Era: Era.Classical),
        [Tech.Stirrup] = new("Estribo", TechBranch.Military, 6, "Caballeros: la caballería pesada carga con lanza.", Modifiers.None,
            [Tech.HeavyCavalry], Era: Era.Medieval),
        [Tech.Machinery] = new("Maquinaria", TechBranch.Military, 6, "Ballestas que atraviesan armaduras.", Modifiers.None, [Tech.Mathematics], Era: Era.Medieval),
        [Tech.Castles] = new("Castillos", TechBranch.Military, 7, "Castillos para las ciudades.", Modifiers.None, [Tech.Fortifications], Era: Era.Medieval),
        [Tech.Gunpowder] = new("Pólvora", TechBranch.Military, 8, "Descubre el azufre y el salitre. Arcabuces y picas: arcabuceros, piqueros y cazadores de montaña.", Modifiers.None,
            [Tech.Machinery], [ResourceType.Sulfur, ResourceType.Saltpeter], Era.Renaissance),
        [Tech.Metallurgy] = new("Metalurgia", TechBranch.Military, 8, "Cañones de hierro fundido.", Modifiers.None, [Tech.Gunpowder, Tech.Guilds], Era: Era.Renaissance),
        [Tech.MilitaryScience] = new("Ciencia militar", TechBranch.Military, 9, "Mosqueteros: infantería de fuego disciplinada.",
            Modifiers.None, [Tech.Gunpowder, Tech.PrintingPress], Era: Era.Renaissance),
        [Tech.Rifling] = new("Estriado", TechBranch.Military, 10, "Fusiles de ánima rayada: fusileros, infantería ligera y cazadores alpinos.", Modifiers.None, [Tech.MilitaryScience], Era: Era.Industrial),
        [Tech.Steel] = new("Acero", TechBranch.Military, 10, "Artillería de campaña de acero.", Modifiers.None, [Tech.Metallurgy, Tech.SteamEngine], Era: Era.Industrial),
        [Tech.MachineGuns] = new("Ametralladoras", TechBranch.Military, 11, "Ametralladores: fuego continuo que detiene los asaltos.", Modifiers.None, [Tech.Rifling], Era: Era.Industrial),
        [Tech.Combustion] = new("Motor de combustión", TechBranch.Military, 12, "Caballería mecanizada y tropas de montaña motorizadas.", Modifiers.None, [Tech.OilRefining], Era: Era.Modern),
        [Tech.HeavyArtillery] = new("Artillería pesada", TechBranch.Military, 12, "Obuses de gran calibre.", Modifiers.None, [Tech.Steel], Era: Era.Modern),
        [Tech.Armour] = new("Blindados", TechBranch.Military, 13, "Tanques.", Modifiers.None, [Tech.Combustion, Tech.Steel], Era: Era.Modern),
        [Tech.Aviation] = new("Aviación", TechBranch.Military, 13, "Bombarderos que vuelan sobre cualquier terreno y sobre el mar, paracaidistas y artillería antiaérea.", Modifiers.None,
            [Tech.Combustion, Tech.Electricity], Era: Era.Modern),

        // Better weapons and drill: the barracks train the battalions they study faster.
        [Tech.ImprovedBows] = new("Arcos mejorados", TechBranch.Military, 3, "Arcos compuestos: la infantería a distancia se instruye un 25 % antes.",
            Modifiers.None, [Tech.Archery], FasterTraining: [BattalionType.RangedInfantry]),
        [Tech.HorseBreeding] = new("Cría caballar", TechBranch.Military, 3, "Yeguadas: la caballería y los carros se instruyen un 25 % antes.",
            Modifiers.None, [Tech.HorsebackRiding], FasterTraining: [BattalionType.Cavalry, BattalionType.Armour]),
        [Tech.Drill] = new("Instrucción militar", TechBranch.Military, 5, "Legionarios; la infantería ligera y la pesada se instruyen un 25 % antes; +25 % de reclutas.",
            new Modifiers { Manpower = 0.25 }, [Tech.MilitaryTactics], Era: Era.Classical,
            FasterTraining: [BattalionType.LightInfantry, BattalionType.HeavyInfantry]),
        [Tech.SiegeWorkshops] = new("Talleres de asedio", TechBranch.Military, 6, "Trabuquetes; la artillería se construye un 25 % antes.",
            Modifiers.None, [Tech.SiegeEngines], Era: Era.Medieval, FasterTraining: [BattalionType.Artillery]),
        [Tech.Armouries] = new("Armerías", TechBranch.Military, 7, "Corazas en serie: infantería pesada y almogávares; la infantería pesada y la caballería se instruyen un 25 % antes.",
            Modifiers.None, [Tech.IronWorking], Era: Era.Medieval, FasterTraining: [BattalionType.HeavyInfantry, BattalionType.Cavalry]),
        [Tech.InterchangeableParts] = new("Piezas intercambiables", TechBranch.Military, 9, "La infantería ligera y la de a distancia se instruyen un 25 % antes.",
            Modifiers.None, [Tech.Gunpowder], Era: Era.Renaissance, FasterTraining: [BattalionType.LightInfantry, BattalionType.RangedInfantry]),
        [Tech.Conscription] = new("Servicio militar obligatorio", TechBranch.Military, 11,
            "La infantería ligera, la de a distancia y las tropas de montaña se instruyen un 25 % antes; +50 % de reclutas.",
            new Modifiers { Manpower = 0.5 }, [Tech.MilitaryScience], Era: Era.Industrial,
            FasterTraining: [BattalionType.LightInfantry, BattalionType.RangedInfantry, BattalionType.MountainInfantry]),
        [Tech.ArtilleryFoundries] = new("Fundiciones de artillería", TechBranch.Military, 11, "La artillería y la antiaérea se construyen un 25 % antes.",
            Modifiers.None, [Tech.Steel], Era: Era.Industrial, FasterTraining: [BattalionType.Artillery, BattalionType.AntiAir]),
        [Tech.WarProduction] = new("Producción bélica", TechBranch.Military, 13, "Caballería mecanizada, tanques, paracaidistas y bombarderos se instruyen un 25 % antes.",
            Modifiers.None, [Tech.Combustion], Era: Era.Modern,
            FasterTraining: [BattalionType.Cavalry, BattalionType.Armour, BattalionType.Paratroopers, BattalionType.Bombers]),
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


    /// <summary>The era with its article, to use inside a sentence: «la era Clásica», «la era de los Descubrimientos».</summary>
    public static string The(this Era era) => era switch
    {
        Era.Ancient => "la Antigüedad",
        Era.Renaissance => "la era de los Descubrimientos",
        _ => $"la era {era.Name()}",
    };

    public static string Name(this Era era) => era switch
    {
        Era.Ancient => "Antigüedad",
        Era.Classical => "Clásica",
        Era.Medieval => "Medieval",
        Era.Renaissance => "Descubrimientos",
        Era.Industrial => "Industrial",
        Era.Modern => "Moderna",
        _ => era.ToString(),
    };
}
