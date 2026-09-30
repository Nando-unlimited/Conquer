namespace Conquer.Game.Military;

/// <summary>What a general is good at; it helps the units under their HQ that are within its range.</summary>
public enum GeneralTrait
{
    /// <summary>Hits harder when attacking.</summary>
    Offensive,
    /// <summary>Hits harder when defending.</summary>
    Defensive,
    /// <summary>Gets the troops back in order sooner.</summary>
    Organiser,
    /// <summary>Loses less organisation in battle.</summary>
    Tactician,
}

/// <summary>
/// The general at the head of an HQ: a name, a trait and a skill of 1 to <see cref="MaxSkill"/> stars. Every
/// <see cref="VictoriesPerStar"/> victories of the units under them earn another star.
/// </summary>
public sealed class General
{
    public const int MaxSkill = 5;
    public const int VictoriesPerStar = 3;

    public string Name { get; }
    public GeneralTrait Trait { get; }
    /// <summary>Stars when appointed (1 to 3).</summary>
    public int StartingSkill { get; }
    public int Victories { get; set; }

    public General(string name, GeneralTrait trait, int startingSkill, int victories = 0)
    {
        Name = name;
        Trait = trait;
        StartingSkill = startingSkill;
        Victories = victories;
    }

    public int Skill => Math.Min(MaxSkill, StartingSkill + Victories / VictoriesPerStar);

    /// <summary>What the trait adds per star.</summary>
    public const double AttackPerStar = 0.1, DefensePerStar = 0.1, RecoveryPerStar = 0.25, ShieldPerStar = 0.1;

    /// <summary>Extra fire of the units under them: attacking for an offensive general, defending for a defensive one.</summary>
    public double FireBonus(bool attacking) => Trait switch
    {
        GeneralTrait.Offensive when attacking => AttackPerStar * Skill,
        GeneralTrait.Defensive when !attacking => DefensePerStar * Skill,
        _ => 0,
    };

    /// <summary>Extra organisation their units recover each day.</summary>
    public double RecoveryBonus => Trait == GeneralTrait.Organiser ? RecoveryPerStar * Skill : 0;

    /// <summary>Share of the organisation their units would lose in battle that they keep.</summary>
    public double Shield => Trait == GeneralTrait.Tactician ? Math.Min(0.5, ShieldPerStar * Skill) : 0;

    public static string TraitName(GeneralTrait trait) => trait switch
    {
        GeneralTrait.Offensive => "Ofensivo",
        GeneralTrait.Defensive => "Defensivo",
        GeneralTrait.Organiser => "Organizador",
        GeneralTrait.Tactician => "Táctico",
        _ => trait.ToString(),
    };

    public static string TraitDescription(GeneralTrait trait) => trait switch
    {
        GeneralTrait.Offensive => $"+{AttackPerStar:P0} de ataque por estrella",
        GeneralTrait.Defensive => $"+{DefensePerStar:P0} de defensa por estrella",
        GeneralTrait.Organiser => $"+{RecoveryPerStar:P0} de recuperación de organización por estrella",
        GeneralTrait.Tactician => $"-{ShieldPerStar:P0} de organización perdida en combate por estrella",
        _ => "",
    };

    /// <summary>"Ofensivo **" : the trait and the stars, in characters the font can draw.</summary>
    public string Summary => $"{TraitName(Trait)} {new string('*', Skill)}";

    private static readonly string[] FirstNames =
    [
        "Álvaro", "Beatriz", "Carlos", "Diego", "Elena", "Fernando", "Gonzalo", "Hernán", "Inés", "Jaime", "Lucía", "Martín",
        "Nuño", "Olga", "Pedro", "Ramiro", "Sancho", "Teresa", "Urbano", "Vicente", "Ximena", "Rodrigo", "Leonor", "Mencía",
    ];

    private static readonly string[] Surnames =
    [
        "de Alarcón", "Barrantes", "Castro", "Dávila", "Enríquez", "Figueroa", "Guzmán", "Haro", "Iturbe", "Lara", "Mendoza", "Núñez",
        "Osorio", "Pacheco", "Quiñones", "Ribera", "Salcedo", "Téllez", "Ulloa", "Velasco", "Zúñiga", "de Mena", "Bazán", "Cortés",
    ];

    /// <summary>A newly appointed general: a random name, trait and 1 to 3 stars.</summary>
    public static General Appoint(Random random) => new(
        $"{FirstNames[random.Next(FirstNames.Length)]} {Surnames[random.Next(Surnames.Length)]}",
        (GeneralTrait)random.Next(Enum.GetValues<GeneralTrait>().Length),
        1 + random.Next(3));
}
