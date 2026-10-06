using System.Numerics;
using Conquer.Game.Military;

namespace Conquer.Client.Graphics;

/// <summary>
/// Markers drawn from shapes: NATO counters for units and battalions, and on the map a city as a cluster of houses roofed in its nation's colour, more of them
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

    /// <summary>The NATO symbol of a unit's arm, in black inside the frame at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void NatoSymbol(Batch2D b, float x, float y, float w, float h, UnitFunction function)
    {
        var ink = Rgba.Black;
        const float t = 1.5f;
        float right = x + w, bottom = y + h;
        var centre = new Vector2(x + w / 2, y + h / 2);
        void Cross()
        {
            b.Line(new(x, bottom), new(right, y), ink, t);
            b.Line(new(x, y), new(right, bottom), ink, t);
        }
        switch (function)
        {
            case UnitFunction.Infantry:
                Cross();
                break;
            case UnitFunction.Mountain:
                // The cross with a filled peak at its foot.
                Cross();
                b.Triangle(new(centre.X - w * 0.18f, bottom), new(centre.X + w * 0.18f, bottom), new(centre.X, bottom - h * 0.32f), ink);
                break;
            case UnitFunction.Airborne:
                // The cross with a parachute's canopy over it.
                Cross();
                Arc(b, new(centre.X, y + h * 0.42f), w * 0.22f, h * 0.3f, ink, t);
                break;
            case UnitFunction.AntiAir:
                // The artillery dot under an arch.
                b.Circle(centre + new Vector2(0, h * 0.12f), Math.Min(w, h) * 0.16f, ink);
                Arc(b, new(centre.X, bottom - h * 0.1f), w * 0.36f, h * 0.62f, ink, t);
                break;
            case UnitFunction.Medical:
                // A cross of two bars.
                b.Line(new(centre.X, y + h * 0.15f), new(centre.X, bottom - h * 0.15f), ink, t * 1.6f);
                b.Line(new(centre.X - w * 0.18f, centre.Y), new(centre.X + w * 0.18f, centre.Y), ink, t * 1.6f);
                break;
            case UnitFunction.Mechanised:
                Cross();
                Ellipse(b, centre, w * 0.32f, h * 0.3f, ink, t);
                break;
            case UnitFunction.Cavalry:
                b.Line(new(x, bottom), new(right, y), ink, t);
                break;
            case UnitFunction.Armour:
                Ellipse(b, centre, w * 0.32f, h * 0.3f, ink, t);
                break;
            case UnitFunction.Artillery:
                b.Circle(centre, Math.Min(w, h) * 0.22f, ink);
                break;
            case UnitFunction.Engineers:
            {
                float left = x + w * 0.25f, end = right - w * 0.25f, top = y + h * 0.35f, foot = y + h * 0.7f;
                b.Line(new(left, top), new(end, top), ink, t);
                foreach (float px in new[] { left, centre.X, end }) b.Line(new(px, top), new(px, foot), ink, t);
                break;
            }
            case UnitFunction.Air:
                // Fixed wing: two loops meeting in the middle.
                Ellipse(b, centre - new Vector2(w * 0.16f, 0), w * 0.16f, h * 0.22f, ink, t);
                Ellipse(b, centre + new Vector2(w * 0.16f, 0), w * 0.16f, h * 0.22f, ink, t);
                break;
        }
    }

    /// <summary>Settlers: an outlined triangle pointing up, in black inside the frame at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void Settlers(Batch2D b, float x, float y, float w, float h)
    {
        var ink = Rgba.Black;
        const float t = 1.5f;
        float half = Math.Min(w * 0.3f, h * 0.45f), cx = x + w / 2, top = y + h / 2 - half, bottom = y + h / 2 + half;
        Vector2 apex = new(cx, top), left = new(cx - half * 1.15f, bottom), right = new(cx + half * 1.15f, bottom);
        b.Line(left, right, ink, t);
        b.Line(left, apex, ink, t);
        b.Line(right, apex, ink, t);
    }

    /// <summary>A small NATO counter (20×14) for a battalion, as the lists of troops show it: its arm's symbol on a pale field.</summary>
    public static void Battalion(Batch2D b, float x, float y, BattalionType type)
    {
        const float W = 20, H = 14;
        b.Rect(x - 1, y - 1, W + 2, H + 2, Ink);
        b.Rect(x, y, W, H, Wall);
        if (type.First().Naval)
        {
            // A hull for ships.
            b.Line(new(x + 3, y + H - 4), new(x + W - 3, y + H - 4), Rgba.Black, 2);
            b.Line(new(x + 3, y + H - 4), new(x + 7, y + H - 1), Rgba.Black, 1.5f);
            b.Line(new(x + W - 3, y + H - 4), new(x + W - 7, y + H - 1), Rgba.Black, 1.5f);
        }
        else NatoSymbol(b, x + 1, y + 1, W - 2, H - 2, Formations.FunctionOf(type));
    }

    /// <summary>The upper half of an ellipse standing on <paramref name="foot"/>: a canopy or an arch.</summary>
    private static void Arc(Batch2D b, Vector2 foot, float rx, float ry, Rgba color, float thickness, int segments = 10)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = MathF.PI + MathF.PI * i / segments, a1 = MathF.PI + MathF.PI * (i + 1) / segments;
            b.Line(foot + new Vector2(MathF.Cos(a0) * rx, MathF.Sin(a0) * ry), foot + new Vector2(MathF.Cos(a1) * rx, MathF.Sin(a1) * ry), color, thickness);
        }
    }

    private static void Ellipse(Batch2D b, Vector2 centre, float rx, float ry, Rgba color, float thickness, int segments = 16)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = MathF.Tau * i / segments, a1 = MathF.Tau * (i + 1) / segments;
            b.Line(centre + new Vector2(MathF.Cos(a0) * rx, MathF.Sin(a0) * ry),
                   centre + new Vector2(MathF.Cos(a1) * rx, MathF.Sin(a1) * ry), color, thickness);
        }
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
