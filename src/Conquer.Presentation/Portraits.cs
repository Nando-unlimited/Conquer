using Conquer.Game.Military;
using Conquer.Game.Science;

namespace Conquer.Presentation;

public enum HairStyle
{
    Bald,
    Short,
    SidePart,
    Swept,
    Long,
}

public enum FacialHair
{
    None,
    Moustache,
    Handlebar,
    Goatee,
    FullBeard,
    Sideburns,
}

/// <summary>
/// How an officer looks, for the portrait the client paints: the face is made up from their name and id, so it never
/// changes and needs nothing in saved games; the uniform and headgear come from the era, the colour from the nation,
/// the insignia from the rank (bars for a colonel, stars for generals) and a ribbon for each star beyond the first.
/// Hair greys as the officer's skill grows.
/// </summary>
/// <param name="SkinTone">0 (lightest) to <see cref="SkinTones"/> - 1.</param>
/// <param name="HairColor">0 (black) to <see cref="HairColors"/> - 1.</param>
/// <param name="FaceWidth">Narrow (0) to broad (1).</param>
/// <param name="JawWidth">Pointed (0) to square (1).</param>
/// <param name="Age">Young (0) to old (1): wrinkles, and how grey the hair is to begin with.</param>
/// <param name="BrowTilt">Stern (-1) to worried (1).</param>
/// <param name="NoseSize">Small (0) to large (1).</param>
/// <param name="Bareheaded">Painted without headgear.</param>
/// <param name="Accent">Picks the background tint.</param>
/// <param name="Rank">0 for a colonel, up to 5 for a marshal.</param>
/// <param name="Naval">Commands a fleet: a sailor's uniform.</param>
public readonly record struct Portrait(
    bool IsFemale, int SkinTone, int HairColor, HairStyle Hair, FacialHair Beard, float FaceWidth, float JawWidth, float Age,
    float BrowTilt, float NoseSize, bool Bareheaded, int Accent, int Skill, int Rank, Era Era, bool Naval, uint Color)
{
    public const int SkinTones = 6;
    public const int HairColors = 5;

    /// <summary>The portrait of an officer of a nation of this colour, in this era.</summary>
    public static Portrait Of(Officer officer, Era era, uint color, bool naval = false)
    {
        var random = new Random(StableHash(officer.Name) ^ (officer.Id * 7919));
        bool female = officer.IsFemale;
        var hair = female
            ? random.NextSingle() < 0.6f ? HairStyle.Long : HairStyle.Swept
            : (HairStyle)random.Next((int)HairStyle.Long); // men: bald to swept
        var beard = female ? FacialHair.None : (FacialHair)random.Next(Enum.GetValues<FacialHair>().Length);
        if (!female && random.NextSingle() < 0.35f) beard = FacialHair.None; // many are clean-shaven
        return new Portrait(female, random.Next(SkinTones), random.Next(HairColors), hair, beard, random.NextSingle(),
            female ? random.NextSingle() * 0.5f : random.NextSingle(), random.NextSingle(), random.NextSingle() * 2f - 1f,
            random.NextSingle(), random.NextSingle() < 0.25f, random.Next(4), officer.Skill, (int)officer.Rank, era, naval, color);
    }

    /// <summary>How grey the hair is, 0 to 1: some with age, more as skill (and years of service) grows.</summary>
    public float Greying => Math.Clamp(Age * 0.5f - 0.15f + (Skill - 1) * 0.15f, 0f, 1f);

    /// <summary>FNV-1a: unlike <see cref="string.GetHashCode()"/>, the same in every run.</summary>
    private static int StableHash(string text)
    {
        unchecked
        {
            int hash = (int)2166136261;
            foreach (char c in text) hash = (hash ^ c) * 16777619;
            return hash;
        }
    }
}
