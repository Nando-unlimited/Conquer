namespace Conquer.Game.Military;

/// <summary>
/// The formations of an army. Battalions, trained in cities, form regiments (up to 5); regiments form brigades (up
/// to 4) and brigades and regiments form divisions (up to 5 of them, 10,000 men). Each regiment, brigade or division
/// goes about the map as one unit (<see cref="Echelon"/>). Above them only headquarters: corps command combat units,
/// armies command corps and army groups command armies (the HQ levels 1-3).
/// </summary>
public static class Formations
{
    /// <param name="Feminine">Spanish gender, for ordinals: "1.ª Brigada", "1.er Regimiento".</param>
    private sealed record Names(string Singular, string Plural, bool Feminine);

    private static readonly Names Regiment = new("Regimiento", "regimientos", false);
    private static readonly Names Brigade = new("Brigada", "brigadas", true);
    private static readonly Names Division = new("División", "divisiones", true);

    /// <summary>The HQ levels, 1 to 3.</summary>
    private static readonly Names[] Headquarters =
    [
        new("Cuerpo", "cuerpos", false),
        new("Ejército", "ejércitos", false),
        new("Grupo de ejércitos", "grupos de ejércitos", false),
    ];

    private static Names Size(Echelon echelon) => echelon switch
    {
        Echelon.Regiment => Regiment,
        Echelon.Brigade => Brigade,
        _ => Division,
    };

    /// <summary>NATO echelon marks over a combat unit: "III" regiment, "X" brigade, "XX" division.</summary>
    public static string CombatEchelon(Echelon echelon) => echelon switch
    {
        Echelon.Regiment => "III",
        Echelon.Brigade => "X",
        _ => "XX",
    };

    /// <summary>
    /// The NATO symbol a combat unit shows for its battalions: the role most of them share (ties go to the front line);
    /// tanks with infantry or cavalry make mechanised infantry.
    /// </summary>
    public static UnitFunction Function(IEnumerable<Battalion> battalions) => Function(battalions.Select(b => FunctionOf(b.Type, b.Info)));

    /// <summary>The same for a design (<see cref="RegimentTemplate"/>), which names lines only: its carros are taken for tanks.</summary>
    public static UnitFunction Function(IEnumerable<BattalionType> battalions) => Function(battalions.Select(FunctionOf));

    private static UnitFunction Function(IEnumerable<UnitFunction> functions)
    {
        var counts = new Dictionary<UnitFunction, int>();
        foreach (var f in functions) counts[f] = counts.GetValueOrDefault(f) + 1;
        if (counts.Count == 0) return UnitFunction.Infantry;
        if (counts.ContainsKey(UnitFunction.Armour) && (counts.ContainsKey(UnitFunction.Infantry) || counts.ContainsKey(UnitFunction.Cavalry)))
            return UnitFunction.Mechanised;
        return counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key).First().Key;
    }

    /// <summary>The NATO symbol of one battalion: its line's, but horse-drawn war chariots are cavalry, not armour.</summary>
    public static UnitFunction FunctionOf(BattalionType type, BattalionInfo model) =>
        type == BattalionType.Armour && !model.Machine ? UnitFunction.Cavalry : FunctionOf(type);

    /// <summary>The NATO symbol of one line of battalion.</summary>
    public static UnitFunction FunctionOf(BattalionType type) => type switch
    {
        BattalionType.MountainInfantry => UnitFunction.Mountain,
        BattalionType.Paratroopers => UnitFunction.Airborne,
        BattalionType.AntiAir => UnitFunction.AntiAir,
        BattalionType.Medics => UnitFunction.Medical,
        // Scouts are reconnaissance, drawn like cavalry.
        BattalionType.Scouts => UnitFunction.Cavalry,
        _ => type.Role() switch
        {
            BattalionRole.Cavalry => UnitFunction.Cavalry,
            BattalionRole.Armour => UnitFunction.Armour,
            BattalionRole.Artillery => UnitFunction.Artillery,
            BattalionRole.Engineers => UnitFunction.Engineers,
            BattalionRole.Air => UnitFunction.Air,
            BattalionRole.Naval => UnitFunction.Naval,
            _ => UnitFunction.Infantry,
        },
    };

    /// <summary>"Regimiento", "Brigada" or "División".</summary>
    public static string CombatName(Echelon echelon) => Size(echelon).Singular;

    /// <summary>"regimientos", "brigadas", "divisiones".</summary>
    public static string CombatPluralOf(Echelon echelon) => Size(echelon).Plural;

    /// <summary>"1 regimiento", "3 brigadas".</summary>
    public static string Count(int n, Echelon echelon) => n == 1 ? $"1 {Size(echelon).Singular.ToLowerInvariant()}" : $"{n} {Size(echelon).Plural}";

    /// <summary>What combat units are called together, for headings and messages.</summary>
    public const string CombatPlural = "unidades de combate";

    /// <summary>"Cuerpo", "Ejército", "Grupo de ejércitos" for HQ levels 1-3.</summary>
    public static string LevelName(int level) => Headquarters[level - 1].Singular;

    public static string LevelPlural(int level) => Headquarters[level - 1].Plural;

    /// <summary>The name of what an HQ of this level commands: combat units for a corps, then the level below.</summary>
    public static string SubordinatesPlural(int level) => level == 1 ? CombatPlural : LevelPlural(level - 1);

    /// <summary>"1 batallón", "3 batallones".</summary>
    public static string BattalionCount(int n) => n == 1 ? "1 batallón" : $"{n} batallones";

    /// <summary>"Batallón de arqueros"; a ship is just its kind ("Trirreme").</summary>
    public static string BattalionName(BattalionInfo info) => info.Naval ? info.Name : "Batallón de " + info.Name.ToLowerInvariant();

    /// <summary>"Batallón de infantería ligera", by its line, whatever the model; a ship is its kind.</summary>
    public static string BattalionName(BattalionType type) =>
        type.Line().Group == BattalionGroup.Navy ? type.Line().Name : "Batallón de " + type.Line().Name.ToLowerInvariant();

    /// <summary>A combat unit's name from its number and size: "3.er Regimiento", "3.ª Brigada", "3.ª División".</summary>
    public static string CombatUnitName(int number, Echelon echelon)
    {
        var names = Size(echelon);
        return $"{number}{Ordinal(number, names.Feminine)} {names.Singular}";
    }

    /// <summary>An HQ's name: corps keep Roman numerals ("II Cuerpo"), the rest an ordinal ("1.er Ejército").</summary>
    public static string HeadquartersName(int level, int number)
    {
        var names = Headquarters[level - 1];
        return level == 1 ? $"{Roman(number)} {names.Singular}" : $"{number}{Ordinal(number, names.Feminine)} {names.Singular}";
    }


    /// <summary>"1 barco", "3 barcos".</summary>
    public static string ShipCount(int n) => n == 1 ? "1 barco" : $"{n} barcos";

    private static string Ordinal(int n, bool feminine)
    {
        if (feminine) return ".ª";
        int last = n % 10, lastTwo = n % 100;
        return (last is 1 or 3) && lastTwo is not (11 or 13) ? ".er" : ".º";
    }

    public static string Roman(int n)
    {
        (int Value, string Digits)[] table = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var text = new System.Text.StringBuilder();
        foreach (var (value, digits) in table)
            for (; n >= value; n -= value) text.Append(digits);
        return text.ToString();
    }
}

/// <summary>What a unit's NATO symbol shows inside its frame (see <see cref="Formations.Function"/>).</summary>
public enum UnitFunction
{
    /// <summary>A cross (X).</summary>
    Infantry,
    /// <summary>The infantry cross with a mountain at its foot.</summary>
    Mountain,
    /// <summary>The infantry cross with a parachute's canopy.</summary>
    Airborne,
    /// <summary>The infantry cross with the tracks of armour.</summary>
    Mechanised,
    /// <summary>A diagonal slash: horse, chariots and reconnaissance.</summary>
    Cavalry,
    /// <summary>An oval: tracks.</summary>
    Armour,
    /// <summary>A filled dot.</summary>
    Artillery,
    /// <summary>The artillery dot under an arch: guns aimed at the sky.</summary>
    AntiAir,
    /// <summary>A bridge: a bar with three legs.</summary>
    Engineers,
    /// <summary>A cross of two bars: the medics.</summary>
    Medical,
    /// <summary>Wings.</summary>
    Air,
    Naval,
}
