using System.Numerics;

namespace Conquer.Client.Graphics;

/// <summary>
/// A route arrow in the style of Hearts of Iron: a thick curve through the waypoints with a dark outline, an
/// arrowhead on the destination and light chevrons that slide along it towards where the unit is going.
/// </summary>
public static class PathArrow
{
    private static readonly Rgba Outline = new(0xFF141414);

    /// <param name="waypoints">Screen points from the unit to its destination.</param>
    /// <param name="time">Seconds of real time, to animate the chevrons.</param>
    public static void Draw(Batch2D batch, IReadOnlyList<Vector2> waypoints, Rgba fill, double time, float width = 6, float alpha = 1)
    {
        if (waypoints.Count < 2) return;
        var curve = Smooth(waypoints);
        var arc = new float[curve.Count];
        for (int i = 1; i < curve.Count; i++) arc[i] = arc[i - 1] + Vector2.Distance(curve[i - 1], curve[i]);
        float length = arc[^1];
        if (length < 4) return;

        float headLength = MathF.Min(width * 2.6f, length * 0.6f);
        float headHalf = width * 1.4f;
        var (baseCentre, _) = At(curve, arc, length - headLength);
        var tip = curve[^1];
        var dir = Vector2.Normalize(tip - baseCentre);
        var side = new Vector2(-dir.Y, dir.X);

        // The body stops a little inside the head, so the two overlap without a seam.
        var body = new List<Vector2>();
        float bodyEnd = length - headLength * 0.8f;
        for (int i = 0; i < curve.Count && arc[i] < bodyEnd; i++) body.Add(curve[i]);
        body.Add(At(curve, arc, bodyEnd).Point);

        var outline = Outline.WithAlpha(0.85f * alpha);
        var colour = fill.WithAlpha(alpha);
        if (body.Count >= 2) Strokes.Along(batch, body, Vector2.Zero, _ => width / 2 + 1.5f, outline, feather: 1);
        batch.Triangle(tip + dir * 2.5f, baseCentre - dir * 1.5f + side * (headHalf + 2.2f), baseCentre - dir * 1.5f - side * (headHalf + 2.2f), outline);
        if (body.Count >= 2) Strokes.Along(batch, body, Vector2.Zero, _ => width / 2, colour, feather: 1);
        batch.Triangle(tip, baseCentre + side * headHalf, baseCentre - side * headHalf, colour);

        // Chevrons sliding towards the destination.
        var chevron = Batch2D.Mix(fill, Rgba.White, 0.55f).WithAlpha(0.9f * alpha);
        const float Spacing = 18, Speed = 24;
        float arm = width * 0.45f;
        for (float s = (float)(time * Speed % Spacing) + 4; s < length - headLength - 2; s += Spacing)
        {
            var (p, t) = At(curve, arc, s);
            var n = new Vector2(-t.Y, t.X);
            var point = p + t * (arm * 0.6f);
            batch.Line(p - t * (arm * 0.6f) + n * arm, point, chevron, 1.6f);
            batch.Line(p - t * (arm * 0.6f) - n * arm, point, chevron, 1.6f);
        }
    }

    /// <summary>A Catmull-Rom curve through the points, so the route bends smoothly at each province.</summary>
    private static List<Vector2> Smooth(IReadOnlyList<Vector2> p)
    {
        var result = new List<Vector2> { p[0] };
        for (int i = 0; i + 1 < p.Count; i++)
        {
            var p0 = p[Math.Max(i - 1, 0)];
            var p1 = p[i];
            var p2 = p[i + 1];
            var p3 = p[Math.Min(i + 2, p.Count - 1)];
            int steps = Math.Clamp((int)(Vector2.Distance(p1, p2) / 6), 1, 16);
            for (int k = 1; k <= steps; k++)
            {
                float t = k / (float)steps, t2 = t * t, t3 = t2 * t;
                result.Add(0.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (3 * p1 - p0 - 3 * p2 + p3) * t3));
            }
        }
        return result;
    }

    /// <summary>The point and direction at a distance along the curve.</summary>
    private static (Vector2 Point, Vector2 Tangent) At(List<Vector2> curve, float[] arc, float distance)
    {
        int i = 1;
        while (i < curve.Count - 1 && arc[i] < distance) i++;
        var a = curve[i - 1];
        var b = curve[i];
        float span = arc[i] - arc[i - 1];
        float t = span > 1e-4f ? Math.Clamp((distance - arc[i - 1]) / span, 0, 1) : 0;
        var d = b - a;
        return (a + d * t, d.LengthSquared() > 1e-8f ? Vector2.Normalize(d) : Vector2.UnitX);
    }
}
