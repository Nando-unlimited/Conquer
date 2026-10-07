using System.Numerics;

namespace Conquer.Presentation;

/// <summary>One piece of a flag, in a box 1 wide and 1 high (drawn 3:2); sizes of discs, stars and lines are shares of its height.</summary>
public abstract record FlagShape(uint Color);

/// <summary>A filled rectangle from (<see cref="X0"/>, <see cref="Y0"/>) to (<see cref="X1"/>, <see cref="Y1"/>).</summary>
public sealed record FlagBand(float X0, float Y0, float X1, float Y1, uint Color) : FlagShape(Color);

/// <summary>A filled disc, or only its lower half.</summary>
public sealed record FlagDisc(float X, float Y, float Radius, uint Color, bool LowerHalf = false) : FlagShape(Color);

/// <summary>A filled five-pointed star, pointing up.</summary>
public sealed record FlagStar(float X, float Y, float Radius, uint Color) : FlagShape(Color);

/// <summary>A filled triangle.</summary>
public sealed record FlagTriangle(Vector2 A, Vector2 B, Vector2 C, uint Color) : FlagShape(Color);

/// <summary>A straight bar from <see cref="A"/> to <see cref="B"/>, <see cref="Thickness"/> of the height thick.</summary>
public sealed record FlagLine(Vector2 A, Vector2 B, float Thickness, uint Color) : FlagShape(Color);

/// <summary>A nation's flag: its shapes, drawn in order over each other.</summary>
public sealed record NationFlag(IReadOnlyList<FlagShape> Shapes);

/// <summary>
/// The flags of the nations, drawn from shapes: each real country (<c>Countries</c>) has its own flag, simplified so it
/// reads at the size of a counter; a nation with any other name flies one of its colour.
/// </summary>
public static class Flags
{
    private const uint White = 0xFFFFFFFF, Black = 0xFF1A1A1A, Red = 0xFFD52B1E,
        Sky = 0xFF74ACDF, Yellow = 0xFFFFCD00, Gold = 0xFFF1BF00, Orange = 0xFFFF883E;

    private static readonly Dictionary<string, NationFlag> Known = new()
    {
        ["España"] = Rows((Red, 1), (Gold, 2), (Red, 1)),
        ["Portugal"] = Flag(new FlagBand(0, 0, 0.4f, 1, 0xFF046A38), new FlagBand(0.4f, 0, 1, 1, 0xFFDA291C), new FlagDisc(0.4f, 0.5f, 0.22f, Yellow),
            new FlagDisc(0.4f, 0.5f, 0.12f, 0xFFDA291C)),
        ["Francia"] = Columns(0xFF0055A4, White, 0xFFEF4135),
        ["Inglaterra"] = Flag(Field(White), new FlagBand(0.42f, 0, 0.58f, 1, 0xFFCE1124), new FlagBand(0, 0.38f, 1, 0.62f, 0xFFCE1124)),
        ["Escocia"] = Flag(Field(0xFF005EB8), new FlagLine(new(0, 0), new(1, 1), 0.2f, White), new FlagLine(new(0, 1), new(1, 0), 0.2f, White)),
        ["Irlanda"] = Columns(0xFF169B62, White, Orange),
        ["Alemania"] = Rows((Black, 1), (0xFFDD0000, 1), (0xFFFFCE00, 1)),
        ["Italia"] = Columns(0xFF009246, White, 0xFFCE2B37),
        ["Países Bajos"] = Rows((0xFFAE1C28, 1), (White, 1), (0xFF21468B, 1)),
        // The lion of Flanders, black on gold.
        ["Flandes"] = Flag(Field(Yellow), new FlagBand(0.3f, 0.35f, 0.62f, 0.62f, Black), new FlagDisc(0.66f, 0.3f, 0.13f, Black),
            new FlagLine(new(0.34f, 0.6f), new(0.32f, 0.85f), 0.08f, Black), new FlagLine(new(0.58f, 0.6f), new(0.6f, 0.85f), 0.08f, Black),
            new FlagLine(new(0.3f, 0.4f), new(0.18f, 0.2f), 0.06f, Black)),
        ["Suiza"] = Flag(Field(0xFFDA291C), new FlagBand(0.43f, 0.18f, 0.57f, 0.82f, White), new FlagBand(0.3f, 0.4f, 0.7f, 0.6f, White)),
        ["Austria"] = Rows((0xFFED2939, 1), (White, 1), (0xFFED2939, 1)),
        ["Polonia"] = Rows((White, 1), (0xFFDC143C, 1)),
        ["Bohemia"] = Flag(new FlagBand(0, 0, 1, 0.5f, White), new FlagBand(0, 0.5f, 1, 1, 0xFFD7141A), new FlagTriangle(new(0, 0), new(0.5f, 0.5f), new(0, 1), 0xFF11457E)),
        ["Hungría"] = Rows((0xFFCD2A3E, 1), (White, 1), (0xFF436F4D, 1)),
        ["Suecia"] = Nordic(0xFF006AA7, 0xFFFECC02),
        ["Noruega"] = Nordic(0xFFBA0C2F, White, 0xFF00205B),
        ["Dinamarca"] = Nordic(0xFFC8102E, White),
        ["Finlandia"] = Nordic(White, 0xFF002F6C),
        ["Rusia"] = Rows((White, 1), (0xFF0039A6, 1), (0xFFD52B1E, 1)),
        ["Ucrania"] = Rows((0xFF0057B7, 1), (0xFFFFD700, 1)),
        ["Grecia"] = Greece(),
        ["Turquía"] = Crescent(0xFFE30A17, White),
        ["Rumanía"] = Columns(0xFF002B7F, 0xFFFCD116, 0xFFCE1126),
        ["Serbia"] = Rows((0xFFC6363C, 1), (0xFF0C4076, 1), (White, 1)),
        ["Bulgaria"] = Rows((White, 1), (0xFF00966E, 1), (0xFFD62612, 1)),
        ["Egipto"] = With(Rows((0xFFCE1126, 1), (White, 1), (Black, 1)), new FlagDisc(0.5f, 0.5f, 0.11f, 0xFFC09300)),
        ["Marruecos"] = With(Flag(Field(0xFFC1272D)), Pentagram(0.5f, 0.5f, 0.28f, 0xFF006233)),
        ["Etiopía"] = With(Rows((0xFF078930, 1), (0xFFFCDD09, 1), (0xFFDA121A, 1)), new FlagDisc(0.5f, 0.5f, 0.24f, 0xFF0F47AF),
            new FlagStar(0.5f, 0.52f, 0.17f, 0xFFFCDD09)),
        ["Malí"] = Columns(0xFF14B53A, 0xFFFCD116, 0xFFCE1126),
        ["Persia"] = With(Rows((0xFF239F40, 1), (White, 1), (0xFFDA0000, 1)), new FlagDisc(0.5f, 0.5f, 0.1f, 0xFFDA0000)),
        ["Arabia"] = Flag(Field(0xFF006C35), new FlagBand(0.25f, 0.3f, 0.75f, 0.42f, White), new FlagLine(new(0.22f, 0.66f), new(0.78f, 0.66f), 0.07f, White)),
        ["India"] = With(Rows((0xFFFF9933, 1), (White, 1), (0xFF138808, 1)), new FlagDisc(0.5f, 0.5f, 0.13f, 0xFF000080), new FlagDisc(0.5f, 0.5f, 0.09f, White)),
        ["China"] = Flag(Field(0xFFEE1C25), new FlagStar(0.17f, 0.27f, 0.17f, 0xFFFFFF00), new FlagStar(0.34f, 0.1f, 0.05f, 0xFFFFFF00),
            new FlagStar(0.4f, 0.2f, 0.05f, 0xFFFFFF00), new FlagStar(0.4f, 0.35f, 0.05f, 0xFFFFFF00), new FlagStar(0.34f, 0.45f, 0.05f, 0xFFFFFF00)),
        ["Japón"] = Flag(Field(White), new FlagDisc(0.5f, 0.5f, 0.3f, 0xFFBC002D)),
        ["Corea"] = Flag(Field(White), new FlagDisc(0.5f, 0.5f, 0.25f, 0xFFCD2E3A), new FlagDisc(0.5f, 0.5f, 0.25f, 0xFF003478, LowerHalf: true),
            new FlagLine(new(0.12f, 0.2f), new(0.22f, 0.08f), 0.1f, Black), new FlagLine(new(0.78f, 0.08f), new(0.88f, 0.2f), 0.1f, Black),
            new FlagLine(new(0.12f, 0.8f), new(0.22f, 0.92f), 0.1f, Black), new FlagLine(new(0.78f, 0.92f), new(0.88f, 0.8f), 0.1f, Black)),
        ["Vietnam"] = Flag(Field(0xFFDA251D), new FlagStar(0.5f, 0.52f, 0.3f, 0xFFFFFF00)),
        ["Siam"] = Rows((0xFFA51931, 1), (White, 1), (0xFF2D2A4A, 2), (White, 1), (0xFFA51931, 1)),
        ["Indonesia"] = Rows((0xFFCE1126, 1), (White, 1)),
        ["Mongolia"] = Flag(new FlagBand(0, 0, 1f / 3, 1, 0xFFC4272F), new FlagBand(1f / 3, 0, 2f / 3, 1, 0xFF015197), new FlagBand(2f / 3, 0, 1, 1, 0xFFC4272F),
            new FlagDisc(1f / 6, 0.32f, 0.07f, 0xFFF9CF02), new FlagBand(0.1f, 0.45f, 0.23f, 0.85f, 0xFFF9CF02)),
        ["Australia"] = Flag(Field(0xFF00008B), new FlagBand(0, 0, 0.5f, 0.5f, 0xFF00008B), new FlagLine(new(0, 0), new(0.5f, 0.5f), 0.1f, White),
            new FlagLine(new(0, 0.5f), new(0.5f, 0), 0.1f, White), new FlagBand(0.21f, 0, 0.29f, 0.5f, White), new FlagBand(0, 0.19f, 0.5f, 0.31f, White),
            new FlagBand(0.225f, 0, 0.275f, 0.5f, 0xFFE4002B), new FlagBand(0, 0.215f, 0.5f, 0.285f, 0xFFE4002B),
            new FlagStar(0.25f, 0.75f, 0.13f, White), new FlagStar(0.75f, 0.2f, 0.06f, White), new FlagStar(0.62f, 0.45f, 0.06f, White),
            new FlagStar(0.86f, 0.4f, 0.06f, White), new FlagStar(0.75f, 0.82f, 0.06f, White)),
        ["México"] = With(Columns(0xFF006847, White, 0xFFCE1126), new FlagDisc(0.5f, 0.5f, 0.14f, 0xFF8C5A2B)),
        ["Perú"] = Columns(0xFFD91023, White, 0xFFD91023),
        ["Argentina"] = With(Rows((Sky, 1), (White, 1), (Sky, 1)), new FlagDisc(0.5f, 0.5f, 0.1f, 0xFFF6B40E)),
        ["Brasil"] = Flag(Field(0xFF009C3B), new FlagTriangle(new(0.08f, 0.5f), new(0.5f, 0.1f), new(0.92f, 0.5f), 0xFFFFDF00),
            new FlagTriangle(new(0.08f, 0.5f), new(0.5f, 0.9f), new(0.92f, 0.5f), 0xFFFFDF00), new FlagDisc(0.5f, 0.5f, 0.24f, 0xFF002776)),
        ["Colombia"] = Rows((0xFFFCD116, 2), (0xFF003893, 1), (0xFFCE1126, 1)),
        ["Chile"] = Flag(new FlagBand(0, 0, 1, 0.5f, White), new FlagBand(0, 0.5f, 1, 1, 0xFFD52B1E), new FlagBand(0, 0, 1f / 3, 0.5f, 0xFF0039A6),
            new FlagStar(1f / 6, 0.26f, 0.14f, White)),
        ["Estados Unidos"] = UnitedStates(),
        ["Canadá"] = Flag(new FlagBand(0, 0, 0.25f, 1, Red), new FlagBand(0.25f, 0, 0.75f, 1, White), new FlagBand(0.75f, 0, 1, 1, Red),
            new FlagStar(0.5f, 0.45f, 0.26f, Red), new FlagBand(0.48f, 0.55f, 0.52f, 0.8f, Red)),
    };

    /// <summary>The flag of the nation called <paramref name="nation"/>; for a name that is not a real country, one in its colour.</summary>
    public static NationFlag Of(string nation, uint color) => Known.TryGetValue(nation, out var flag) ? flag : Plain(nation, color);

    /// <summary>Whether the country has a flag of its own (not one made up from its colour).</summary>
    public static bool IsKnown(string nation) => Known.ContainsKey(nation);

    /// <summary>A made-up flag: the nation's colour with a white band, its pattern chosen by its name.</summary>
    private static NationFlag Plain(string nation, uint color)
    {
        int pattern = nation.Aggregate(0, (h, c) => h * 31 + c) & 3;
        return pattern switch
        {
            0 => Rows((color, 1), (White, 1), (color, 1)),
            1 => Columns(color, White, color),
            2 => Flag(Field(color), new FlagLine(new(0, 1), new(1, 0), 0.22f, White)),
            _ => Flag(Field(color), new FlagDisc(0.5f, 0.5f, 0.26f, White), new FlagStar(0.5f, 0.52f, 0.18f, color)),
        };
    }

    private static NationFlag Flag(params FlagShape[] shapes) => new(shapes);

    private static NationFlag With(NationFlag flag, params FlagShape[] more) => new([.. flag.Shapes, .. more]);

    private static FlagBand Field(uint color) => new(0, 0, 1, 1, color);

    /// <summary>Horizontal stripes, top to bottom, each as tall as its weight.</summary>
    private static NationFlag Rows(params (uint Color, int Weight)[] stripes)
    {
        float total = stripes.Sum(s => s.Weight), y = 0;
        var shapes = new List<FlagShape>();
        foreach (var (color, weight) in stripes)
        {
            shapes.Add(new FlagBand(0, y / total, 1, (y + weight) / total, color));
            y += weight;
        }
        return new(shapes);
    }

    /// <summary>Upright stripes of equal width, from the pole out.</summary>
    private static NationFlag Columns(params uint[] colors) =>
        new([.. colors.Select((c, i) => new FlagBand((float)i / colors.Length, 0, (float)(i + 1) / colors.Length, 1, c))]);

    /// <summary>A Scandinavian cross, off towards the pole, with an <paramref name="inner"/> cross inside it if given.</summary>
    private static NationFlag Nordic(uint field, uint cross, uint? inner = null)
    {
        var shapes = new List<FlagShape> { Field(field), new FlagBand(0.28f, 0, 0.42f, 1, cross), new FlagBand(0, 0.38f, 1, 0.62f, cross) };
        if (inner is uint i) shapes.AddRange([new FlagBand(0.31f, 0, 0.39f, 1, i), new FlagBand(0, 0.44f, 1, 0.56f, i)]);
        return new(shapes);
    }

    private static NationFlag Greece()
    {
        const uint blue = 0xFF0D5EAF;
        var shapes = new List<FlagShape>();
        for (int i = 0; i < 9; i++) shapes.Add(new FlagBand(0, i / 9f, 1, (i + 1) / 9f, i % 2 == 0 ? blue : White));
        shapes.AddRange([new FlagBand(0, 0, 0.37f, 5 / 9f, blue), new FlagBand(0.155f, 0, 0.215f, 5 / 9f, White), new FlagBand(0, 2 / 9f, 0.37f, 3 / 9f, White)]);
        return new(shapes);
    }

    private static NationFlag UnitedStates()
    {
        var shapes = new List<FlagShape>();
        for (int i = 0; i < 13; i++) shapes.Add(new FlagBand(0, i / 13f, 1, (i + 1) / 13f, i % 2 == 0 ? 0xFFB22234 : White));
        shapes.Add(new FlagBand(0, 0, 0.4f, 7 / 13f, 0xFF3C3B6E));
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 5; col++)
                shapes.Add(new FlagDisc(0.05f + col * 0.075f + (row % 2) * 0.037f, 0.07f + row * 0.13f, 0.025f, White));
        return new(shapes);
    }

    /// <summary>A white crescent and star on <paramref name="field"/>, as on the Ottoman flag.</summary>
    private static NationFlag Crescent(uint field, uint emblem) =>
        Flag(Field(field), new FlagDisc(0.4f, 0.5f, 0.25f, emblem), new FlagDisc(0.45f, 0.5f, 0.2f, field), new FlagStar(0.62f, 0.5f, 0.12f, emblem));

    /// <summary>An outlined five-pointed star (Morocco's seal), as one line per stroke.</summary>
    private static FlagShape[] Pentagram(float x, float y, float r, uint color)
    {
        var points = Enumerable.Range(0, 5).Select(i =>
        {
            double a = -Math.PI / 2 + i * 2 * Math.PI / 5;
            return new Vector2(x + (float)Math.Cos(a) * r * 2 / 3, y + (float)Math.Sin(a) * r);
        }).ToArray();
        return [.. Enumerable.Range(0, 5).Select(i => (FlagShape)new FlagLine(points[i], points[(i + 2) % 5], 0.05f, color))];
    }
}
