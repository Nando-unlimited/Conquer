namespace Conquer.Game.Military;

/// <summary>
/// The formations of an army. Battalions, trained in cities, form the units that march and fight, named
/// by their size: a regiment (1 to 3 battalions), a brigade (4 to 6) or a division (7 to 12); merging
/// and splitting them moves them up and down. Above them only headquarters: corps command combat units,
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

    /// <summary>A combat unit of so many battalions: regiment up to 3, brigade up to 6, division beyond.</summary>
    private static Names CombatSize(int battalions) => battalions <= 3 ? Regiment : battalions <= 6 ? Brigade : Division;

    /// <summary>"Regimiento", "Brigada" or "División" for a combat unit of so many battalions.</summary>
    public static string CombatName(int battalions) => CombatSize(battalions).Singular;

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

    /// <summary>A combat unit's name from its number and size: "3.er Regimiento", "3.ª Brigada", "3.ª División".</summary>
    public static string CombatUnitName(int number, int battalions)
    {
        var names = CombatSize(battalions);
        return $"{number}{Ordinal(number, names.Feminine)} {names.Singular}";
    }

    /// <summary>An HQ's name: corps keep Roman numerals ("II Cuerpo"), the rest an ordinal ("1.er Ejército").</summary>
    public static string HeadquartersName(int level, int number)
    {
        var names = Headquarters[level - 1];
        return level == 1 ? $"{Roman(number)} {names.Singular}" : $"{number}{Ordinal(number, names.Feminine)} {names.Singular}";
    }

    /// <summary>A fleet: "2.ª Flota".</summary>
    public static string FleetName(int number) => $"{number}.ª Flota";

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
