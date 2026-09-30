using System.Numerics;

namespace Conquer.Client.Graphics;

/// <summary>Anti-aliased bands along a path of screen points, for rivers and route arrows.</summary>
public static class Strokes
{
    /// <summary>
    /// A band along the path: solid out to <paramref name="halfWidth"/> of each point, then fading to nothing over
    /// <paramref name="feather"/> pixels (the anti-aliasing), or with <paramref name="fadeToEdge"/> fading all the way
    /// from the middle. Each point's sideways direction averages its two segments, so the joins stay clean.
    /// </summary>
    public static void Along(Batch2D batch, List<Vector2> points, Vector2 offset, Func<int, float> halfWidth, Rgba color,
                             float feather, bool fadeToEdge = false)
    {
        var clear = color.WithAlpha(0);
        Vector2 prevNormal = default, prevP = default;
        float prevHalf = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var before = points[Math.Max(i - 1, 0)];
            var after = points[Math.Min(i + 1, points.Count - 1)];
            var d = after - before;
            float len = d.Length();
            var normal = len > 1e-4f ? new Vector2(-d.Y, d.X) / len : prevNormal;
            var p = points[i] + offset;
            float h = MathF.Max(halfWidth(i), 0);
            if (i > 0 && (h > 0 || prevHalf > 0))
            {
                if (fadeToEdge)
                {
                    float fa = prevHalf + feather, fb = h + feather;
                    Band(batch, prevP, p, prevNormal, normal, 0, 0, fa, fb, color, clear);
                    Band(batch, prevP, p, -prevNormal, -normal, 0, 0, fa, fb, color, clear);
                }
                else
                {
                    Band(batch, prevP, p, prevNormal, normal, -prevHalf, -h, prevHalf, h, color, color);
                    Band(batch, prevP, p, prevNormal, normal, prevHalf, h, prevHalf + feather, h + feather, color, clear);
                    Band(batch, prevP, p, -prevNormal, -normal, prevHalf, h, prevHalf + feather, h + feather, color, clear);
                }
            }
            prevNormal = normal;
            prevP = p;
            prevHalf = h;
        }
    }

    /// <summary>The quad between offsets <paramref name="innerA"/>..<paramref name="outerA"/> at a and innerB..outerB at b.</summary>
    public static void Band(Batch2D batch, Vector2 a, Vector2 b, Vector2 na, Vector2 nb,
                             float innerA, float innerB, float outerA, float outerB, Rgba inner, Rgba outer)
    {
        var a0 = a + na * innerA;
        var a1 = a + na * outerA;
        var b0 = b + nb * innerB;
        var b1 = b + nb * outerB;
        batch.Triangle(a0, a1, b1, inner, outer, outer);
        batch.Triangle(a0, b1, b0, inner, outer, inner);
    }
}
