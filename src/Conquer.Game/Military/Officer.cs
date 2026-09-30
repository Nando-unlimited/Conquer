namespace Conquer.Game.Military;

/// <summary>
/// An officer's rank, set by the size of what they lead: a colonel a regiment, a brigadier a brigade, a major general
/// a division, then a lieutenant general a corps, a general an army and a marshal an army group.
/// </summary>
public enum OfficerRank
{
    Colonel,
    Brigadier,
    MajorGeneral,
    LieutenantGeneral,
    General,
    Marshal,
}

/// <summary>
/// What an officer is good or bad at. The first six are virtues and the last six their matching flaws, in the same
/// order, so <c>(int)trait % 6</c> is what the trait affects. Written by name in saves, so the order may change.
/// </summary>
public enum OfficerTrait
{
    /// <summary>Hits harder when attacking.</summary>
    Offensive,
    /// <summary>Hits harder when defending.</summary>
    Defensive,
    /// <summary>Gets the troops back in order sooner.</summary>
    Organiser,
    /// <summary>Loses less organisation in battle.</summary>
    Tactician,
    /// <summary>Marches faster.</summary>
    Marcher,
    /// <summary>Costs less to keep.</summary>
    Quartermaster,
    /// <summary>Hits softer when attacking.</summary>
    Timid,
    /// <summary>Hits softer when defending.</summary>
    Reckless,
    /// <summary>Gets the troops back in order more slowly.</summary>
    Disorganised,
    /// <summary>Loses more organisation in battle.</summary>
    Indecisive,
    /// <summary>Marches more slowly.</summary>
    Slow,
    /// <summary>Costs more to keep.</summary>
    Corrupt,
}

/// <summary>
/// An officer who leads a combat unit or an HQ (as its general): a name, a rank, 1 to <see cref="MaxSkill"/> stars and
/// one or two virtues, sometimes with a flaw. Virtues grow with the stars; flaws weigh the same whatever the stars.
/// Every <see cref="VictoriesPerStar"/> victories earn another star. A nation keeps its idle officers in reserve.
/// </summary>
public sealed class Officer
{
    public const int MaxSkill = 5;
    public const int VictoriesPerStar = 3;

    /// <summary>What each virtue adds per star.</summary>
    public const double AttackPerStar = 0.1, DefensePerStar = 0.1, RecoveryPerStar = 0.25, ShieldPerStar = 0.1,
                        SpeedPerStar = 0.05, UpkeepPerStar = 0.05;
    /// <summary>What each flaw takes away.</summary>
    public const double TimidAttack = 0.15, RecklessDefense = 0.15, DisorganisedRecovery = 0.25, IndecisiveLoss = 0.2,
                        SlowSpeed = 0.15, CorruptUpkeep = 0.2;
    /// <summary>The most organisation a tactician can spare in battle.</summary>
    public const double MaxShield = 0.5;

    public int Id { get; }
    public string Name { get; }
    public IReadOnlyList<OfficerTrait> Traits { get; }
    /// <summary>Stars when recruited (1 to 3).</summary>
    public int StartingSkill { get; }
    public int Victories { get; set; }
    /// <summary>Rises on its own when the officer is put at the head of something bigger; it never falls.</summary>
    public OfficerRank Rank { get; set; }

    public Officer(int id, string name, IReadOnlyList<OfficerTrait> traits, int startingSkill, int victories = 0, OfficerRank rank = OfficerRank.Colonel)
    {
        Id = id;
        Name = name;
        Traits = traits;
        StartingSkill = startingSkill;
        Victories = victories;
        Rank = rank;
    }

    public int Skill => Math.Min(MaxSkill, StartingSkill + Victories / VictoriesPerStar);

    public static bool IsFlaw(OfficerTrait trait) => (int)trait >= 6;

    private bool Has(OfficerTrait trait) => Traits.Contains(trait);

    /// <summary>Extra fire (negative for less) of the units they lead: attacking or defending.</summary>
    public double FireBonus(bool attacking) => attacking
        ? (Has(OfficerTrait.Offensive) ? AttackPerStar * Skill : 0) - (Has(OfficerTrait.Timid) ? TimidAttack : 0)
        : (Has(OfficerTrait.Defensive) ? DefensePerStar * Skill : 0) - (Has(OfficerTrait.Reckless) ? RecklessDefense : 0);

    /// <summary>Extra organisation (negative for less) their units recover each day, as a share of the usual.</summary>
    public double RecoveryBonus =>
        (Has(OfficerTrait.Organiser) ? RecoveryPerStar * Skill : 0) - (Has(OfficerTrait.Disorganised) ? DisorganisedRecovery : 0);

    /// <summary>Change in the organisation their units lose in battle: below zero spares some, above zero loses more.</summary>
    public double OrganisationLoss =>
        (Has(OfficerTrait.Indecisive) ? IndecisiveLoss : 0) - (Has(OfficerTrait.Tactician) ? Math.Min(MaxShield, ShieldPerStar * Skill) : 0);

    /// <summary>Change in the speed of the unit they lead, as a share.</summary>
    public double SpeedBonus => (Has(OfficerTrait.Marcher) ? SpeedPerStar * Skill : 0) - (Has(OfficerTrait.Slow) ? SlowSpeed : 0);

    /// <summary>Change in the upkeep of the unit they lead, as a share.</summary>
    public double UpkeepChange => (Has(OfficerTrait.Corrupt) ? CorruptUpkeep : 0) - (Has(OfficerTrait.Quartermaster) ? UpkeepPerStar * Skill : 0);

    public static string TraitName(OfficerTrait trait) => trait switch
    {
        OfficerTrait.Offensive => "Ofensivo",
        OfficerTrait.Defensive => "Defensivo",
        OfficerTrait.Organiser => "Organizador",
        OfficerTrait.Tactician => "Táctico",
        OfficerTrait.Marcher => "Marchador",
        OfficerTrait.Quartermaster => "Intendente",
        OfficerTrait.Timid => "Timorato",
        OfficerTrait.Reckless => "Temerario",
        OfficerTrait.Disorganised => "Desorganizado",
        OfficerTrait.Indecisive => "Indeciso",
        OfficerTrait.Slow => "Lento",
        OfficerTrait.Corrupt => "Corrupto",
        _ => trait.ToString(),
    };

    public static string TraitDescription(OfficerTrait trait) => trait switch
    {
        OfficerTrait.Offensive => $"+{AttackPerStar:P0} de ataque por estrella",
        OfficerTrait.Defensive => $"+{DefensePerStar:P0} de defensa por estrella",
        OfficerTrait.Organiser => $"+{RecoveryPerStar:P0} de recuperación por estrella",
        OfficerTrait.Tactician => $"-{ShieldPerStar:P0} de organización perdida por estrella",
        OfficerTrait.Marcher => $"+{SpeedPerStar:P0} de velocidad por estrella",
        OfficerTrait.Quartermaster => $"-{UpkeepPerStar:P0} de mantenimiento por estrella",
        OfficerTrait.Timid => $"-{TimidAttack:P0} de ataque",
        OfficerTrait.Reckless => $"-{RecklessDefense:P0} de defensa",
        OfficerTrait.Disorganised => $"-{DisorganisedRecovery:P0} de recuperación",
        OfficerTrait.Indecisive => $"+{IndecisiveLoss:P0} de organización perdida",
        OfficerTrait.Slow => $"-{SlowSpeed:P0} de velocidad",
        OfficerTrait.Corrupt => $"+{CorruptUpkeep:P0} de mantenimiento",
        _ => "",
    };

    public static string RankName(OfficerRank rank) => rank switch
    {
        OfficerRank.Colonel => "Coronel",
        OfficerRank.Brigadier => "Brigadier",
        OfficerRank.MajorGeneral => "General de división",
        OfficerRank.LieutenantGeneral => "Teniente general",
        OfficerRank.General => "General",
        OfficerRank.Marshal => "Mariscal",
        _ => rank.ToString(),
    };

    /// <summary>"Coronel Hernán Ulloa".</summary>
    public string Title => $"{RankName(Rank)} {Name}";

    /// <summary>"Ofensivo, Lento **": the traits and the stars, in characters the font can draw.</summary>
    public string Summary => $"{string.Join(", ", Traits.Select(TraitName))} {new string('*', Skill)}";

    /// <summary>Every trait with what it does, one per line, flaws marked, for tooltips.</summary>
    public string TraitsDescription => string.Join("\n", Traits.Select(t => $"{TraitName(t)}{(IsFlaw(t) ? " (defecto)" : "")}: {TraitDescription(t)}."));

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

    /// <summary>A new officer: a random name, 1 to 3 stars, one virtue, sometimes a second, and sometimes a flaw that does not undo a virtue.</summary>
    public static Officer Recruit(int id, Random random, OfficerRank rank = OfficerRank.Colonel)
    {
        var traits = new List<OfficerTrait> { (OfficerTrait)random.Next(6) };
        if (random.NextDouble() < 0.4)
        {
            var second = (OfficerTrait)random.Next(6);
            if (second != traits[0]) traits.Add(second);
        }
        if (random.NextDouble() < 0.5)
        {
            var flaw = (OfficerTrait)(6 + random.Next(6));
            if (traits.All(t => (int)t != (int)flaw - 6)) traits.Add(flaw);
        }
        return new Officer(id, $"{FirstNames[random.Next(FirstNames.Length)]} {Surnames[random.Next(Surnames.Length)]}", traits, 1 + random.Next(3), rank: rank);
    }
}
