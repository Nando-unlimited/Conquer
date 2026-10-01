using System.Numerics;

namespace Conquer.Client.Graphics;

/// <summary>
/// Markers drawn on the map from shapes: a city as a cluster of houses roofed in its nation's colour, more of them
/// the bigger it is, and a capital with a tower flying a golden flag.
/// </summary>
public static class MapIcons
{
    private static readonly Rgba Wall = new(0xFFEDE3CC);
    private static readonly Rgba WallShade = new(0xFFC9BC9E);
    private static readonly Rgba Ink = new(0xFF1A1612);
    private static readonly Rgba Flag = new(0xFFE0B656);

    /// <summary>How many houses a city shows: one for a village, up to four for a great city.</summary>
    public static int Houses(double population) => population < 2000 ? 1 : population < 10_000 ? 2 : population < 50_000 ? 3 : 4;

    /// <summary>A city whose houses stand on <paramref name="foot"/> (the middle of their bases); <paramref name="scale"/> 1 is about 20 pixels across.</summary>
    /// <returns>How far below <paramref name="foot"/> its name can go.</returns>
    public static float City(Batch2D b, Vector2 foot, Rgba roof, int houses, bool capital, float scale)
    {
        // The houses side by side, the middle one in front; the capital's tower in the middle, behind them.
        var spots = houses switch
        {
            1 => new[] { 0f },
            2 => [-4.5f, 4.5f],
            3 => [-7f, 7f, 0f],
            _ => [-10f, 10f, -3.5f, 3.5f],
        };
        float width = spots.Max(Math.Abs) * 2 + 10;
        b.Shadow(foot.X - width / 2 * scale, foot.Y - 2 * scale, width * scale, 5 * scale, 3 * scale, spread: 4 * scale, strength: 0.45f);
        if (capital) Tower(b, foot + new Vector2(0, -3) * scale, roof, scale);
        for (int i = 0; i < spots.Length; i++)
        {
            bool front = i == spots.Length - 1 && houses != 2;
            House(b, foot + new Vector2(spots[i], front ? 1 : -1) * scale, roof, scale * (front ? 1.1f : 1));
        }
        return 4 * scale;
    }

    /// <summary>A house 10×12 at <paramref name="scale"/> 1: a wall with a door and a pitched roof, outlined in dark ink.</summary>
    private static void House(Batch2D b, Vector2 foot, Rgba roof, float scale)
    {
        float w = 5 * scale, wall = 5.5f * scale, top = 6 * scale, eave = 1.2f * scale;
        float x0 = foot.X - w, x1 = foot.X + w, y0 = foot.Y - wall, y1 = foot.Y;
        // Outline: the same shapes a pixel larger, in ink.
        b.Rect(x0 - 1, y0 - 1, x1 - x0 + 2, y1 - y0 + 2, Ink);
        b.Triangle(new(x0 - eave - 1.5f, y0 + 0.5f), new(x1 + eave + 1.5f, y0 + 0.5f), new(foot.X, y0 - top - 1.5f), Ink);
        b.Rect(x0, y0, w, y1 - y0, Wall);
        b.Rect(foot.X, y0, w, y1 - y0, WallShade);
        b.Rect(foot.X - 1.2f * scale, y1 - 3 * scale, 2.4f * scale, 3 * scale, Ink.WithAlpha(0.8f));
        b.Triangle(new(x0 - eave, y0), new(foot.X, y0), new(foot.X, y0 - top), roof.Scale(1.15f).WithAlpha(1));
        b.Triangle(new(foot.X, y0), new(x1 + eave, y0), new(foot.X, y0 - top), roof.Scale(0.8f).WithAlpha(1));
    }

    /// <summary>The capital's tower: a slim keep with battlements and a golden pennant above the houses.</summary>
    private static void Tower(Batch2D b, Vector2 foot, Rgba roof, float scale)
    {
        float w = 3.2f * scale, h = 15 * scale;
        float x0 = foot.X - w, y0 = foot.Y - h;
        b.Rect(x0 - 1, y0 - 1, 2 * w + 2, h + 1, Ink);
        b.Rect(x0, y0, w, h, Wall);
        b.Rect(foot.X, y0, w, h, WallShade);
        // Battlements: three teeth on top.
        for (int i = 0; i < 3; i++)
        {
            float tx = x0 + i * (2 * w - 1.6f * scale) / 2;
            b.Rect(tx - 0.5f, y0 - 2.2f * scale - 0.5f, 1.6f * scale + 1, 2.2f * scale + 1, Ink);
            b.Rect(tx, y0 - 2.2f * scale, 1.6f * scale, 2.2f * scale, Wall);
        }
        // Flagpole and pennant.
        var pole = new Vector2(foot.X, y0 - 2.2f * scale);
        b.Line(pole, pole - new Vector2(0, 7 * scale), Ink, 1.2f);
        var tip = pole - new Vector2(0, 7 * scale);
        b.Triangle(tip, tip + new Vector2(6 * scale, 1.7f * scale), tip + new Vector2(0, 3.4f * scale), Flag);
        b.Rect(x0 + w - 1, y0 + 4 * scale, 2, 3 * scale, roof.WithAlpha(1));
    }
}
