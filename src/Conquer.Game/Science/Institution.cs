using Conquer.Game.Rules;

namespace Conquer.Game.Science;

/// <summary>
/// Ideas that open each age, as in Europa Universalis. One is born somewhere in the world, spreads from
/// province to province and a nation adopts it once enough of its people have it, or earlier for gold.
/// </summary>
public enum Institution
{
    Urbanism,
}

/// <param name="Opens">The age whose advances cost more until the nation adopts it.</param>
/// <param name="Birth">Where it appears, for the interface.</param>
/// <param name="Bonus">What it gives a nation that adopts it.</param>
/// <param name="Color">0xAARRGGBB, for the map.</param>
/// <param name="Feminine">Spanish gender, for its article: "el Urbanismo", "la Imprenta".</param>
public sealed record InstitutionInfo(string Name, Era Opens, string Birth, string Description, Modifiers Bonus, uint Color, bool Feminine = false)
{
    /// <summary>Its name with the article: "el Urbanismo".</summary>
    public string The => (Feminine ? "la " : "el ") + Name;
}

public static class Institutions
{
    public static readonly Institution[] All = Enum.GetValues<Institution>();

    /// <summary>The institutions. <b>Balance them here.</b></summary>
    private static readonly Dictionary<Institution, InstitutionInfo> Table = new()
    {
        [Institution.Urbanism] = new("Urbanismo", Era.Classical,
            $"Nace en la primera ciudad que llega a {GameRules.UrbanismBirthPopulation:N0} habitantes.",
            "Da +10 % de ciencia y +10 % de fertilidad.", new() { Science = 0.1, Fertility = 0.1 }, 0xFFD9A441),
    };

    public static InstitutionInfo Info(this Institution institution) => Table[institution];
}
