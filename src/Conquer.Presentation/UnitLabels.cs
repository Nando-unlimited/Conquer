namespace Conquer.Presentation;

/// <summary>Unit names cut short to fit on their counters.</summary>
public static class UnitLabels
{
    /// <summary>Longest a short name gets; past it, it is cut and ends in a point.</summary>
    public const int MaxShortName = 12;

    private static readonly (string Word, string Short)[] Abbreviations =
    [
        ("Grupo de ejércitos", "G. Ej."), ("Regimiento", "Rgto."), ("Brigada", "Brig."), ("División", "Div."), ("Cuerpo", "Cpo."),
        ("Ejército", "Ej."), ("Flotilla", "Flot."), ("Escuadra", "Esc."), ("Fuerza", "F."),
    ];

    /// <summary>"3.er Rgto.", "II Cpo.", "1.ª Esc."; a name of the player's longer than <see cref="MaxShortName"/> is cut ("Los Tercios d.").</summary>
    public static string Short(string name)
    {
        foreach (var (word, abbreviation) in Abbreviations) name = name.Replace(word, abbreviation);
        return name.Length <= MaxShortName ? name : name[..(MaxShortName - 1)].TrimEnd() + ".";
    }
}
