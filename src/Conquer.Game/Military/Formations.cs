using Conquer.Game.Science;

namespace Conquer.Game.Military;

/// <summary>Which names a nation gives its formations: Roman ones until an advance modernises its army.</summary>
public enum ArmyEra
{
    Classical,
    Modern,
}

/// <summary>
/// The formations of an army, smallest first: battalions (trained in cities) form regiments, the
/// smallest unit that marches and fights; regiments report to brigades, then divisions, corps, armies
/// and army groups (the HQ levels 1-5). Each has a Roman name and a modern one.
/// </summary>
public static class Formations
{
    /// <param name="Feminine">Spanish gender, for ordinals: "1.ª Brigada", "1.er Regimiento".</param>
    private sealed record Names(string Modern, string ModernPlural, bool Feminine, string Roman, string RomanPlural);

    /// <summary>Level 0 is the regiment; 1-5 are the HQ levels.</summary>
    private static readonly Names[] Levels =
    [
        new("Regimiento", "regimientos", false, "Legión", "legiones"),
        new("Brigada", "brigadas", true, "Vexilación", "vexilaciones"),
        new("División", "divisiones", true, "Ejército consular", "ejércitos consulares"),
        new("Cuerpo", "cuerpos", false, "Ejército provincial", "ejércitos provinciales"),
        new("Ejército", "ejércitos", false, "Ejército de campaña", "ejércitos de campaña"),
        new("Grupo de ejércitos", "grupos de ejércitos", false, "Prefectura", "prefecturas"),
    ];

    /// <summary>The advance that brings modern names.</summary>
    public static Tech? ModernisedBy => Tech.MilitaryScience;

    public static ArmyEra EraOf(IReadOnlySet<Tech> techs) => ModernisedBy is Tech t && techs.Contains(t) ? ArmyEra.Modern : ArmyEra.Classical;

    /// <summary>"Legión", "Vexilación"… or "Regimiento", "Brigada"…</summary>
    public static string LevelName(int level, ArmyEra era) => era == ArmyEra.Modern ? Levels[level].Modern : Levels[level].Roman;

    public static string LevelPlural(int level, ArmyEra era) => era == ArmyEra.Modern ? Levels[level].ModernPlural : Levels[level].RomanPlural;

    /// <summary>A battalion: "cohorte" (or "ala" if mounted) in Roman times, "batallón" in modern ones.</summary>
    public static string BattalionWord(BattalionInfo info, ArmyEra era) => era == ArmyEra.Modern ? "batallón" : info.Mounted ? "ala" : "cohorte";

    public static string BattalionPlural(ArmyEra era) => era == ArmyEra.Modern ? "batallones" : "cohortes";

    /// <summary>"1 cohorte", "3 cohortes", "2 batallones".</summary>
    public static string BattalionCount(int n, ArmyEra era) =>
        $"{n} {(n == 1 ? (era == ArmyEra.Modern ? "batallón" : "cohorte") : BattalionPlural(era))}";

    /// <summary>"Cohorte de guerreros", "Ala de jinetes", "Batallón de arqueros"; a ship is just its kind ("Trirreme").</summary>
    public static string BattalionName(BattalionInfo info, ArmyEra era)
    {
        if (info.Naval) return info.Name;
        string word = BattalionWord(info, era);
        return char.ToUpperInvariant(word[0]) + word[1..] + " de " + info.Name.ToLowerInvariant();
    }

    /// <summary>
    /// A unit's name from its level and number: Roman numerals after the name in Roman times
    /// ("Legión III", "Vexilación I"), a Spanish ordinal before it in modern ones ("3.er Regimiento",
    /// "1.ª Brigada"), except corps, which keep Roman numerals ("II Cuerpo").
    /// </summary>
    public static string UnitName(int level, int number, ArmyEra era)
    {
        var names = Levels[level];
        if (era == ArmyEra.Classical) return $"{names.Roman} {Roman(number)}";
        if (level == 3) return $"{Roman(number)} {names.Modern}";
        return $"{number}{Ordinal(number, names.Feminine)} {names.Modern}";
    }

    /// <summary>A fleet: "Classis II" in Roman times, "2.ª Flota" in modern ones.</summary>
    public static string FleetName(int number, ArmyEra era) => era == ArmyEra.Classical ? $"Classis {Roman(number)}" : $"{number}.ª Flota";

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
