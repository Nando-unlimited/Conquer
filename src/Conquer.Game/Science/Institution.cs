using Conquer.Game.Rules;

namespace Conquer.Game.Science;

/// <summary>
/// Ideas that open each age, as in Europa Universalis. One is born somewhere in the world, spreads from
/// province to province and a nation adopts it once enough of its people have it, or earlier for gold.
/// </summary>
public enum Institution
{
    Urbanism,
    Feudalism,
    Humanism,
    Industrialization,
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
        [Institution.Feudalism] = new("Feudalismo", Era.Medieval,
            $"Nace en la capital de la primera nación que llega a {GameRules.FeudalismBirthCities} ciudades.",
            "Da +10 % de impuestos y +3 de humor.", new() { Taxes = 0.1, Mood = 3 }, 0xFF8E5BB5),
        [Institution.Humanism] = new("Humanismo", Era.Renaissance, "Nace en la ciudad más poblada que tenga universidad.",
            "Da +10 % de ciencia y +3 de humor.", new() { Science = 0.1, Mood = 3 }, 0xFF4FA3A5),
        [Institution.Industrialization] = new("Industrialización", Era.Industrial, "Nace en la primera provincia con fábrica y yacimiento de carbón.",
            "Da +15 % de madera y +15 % de yacimientos.", new() { Wood = 0.15, Deposits = 0.15 }, 0xFF6B6B6B, Feminine: true),
    };

    public static InstitutionInfo Info(this Institution institution) => Table[institution];
}
