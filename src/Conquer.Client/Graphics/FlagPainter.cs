using System.Numerics;
using Conquer.Presentation;

namespace Conquer.Client.Graphics;

/// <summary>Draws a nation's flag (<see cref="Flags"/>) from its shapes into a box, in a thin dark frame.</summary>
public static class FlagPainter
{
    public static void Draw(Batch2D b, NationFlag flag, float x, float y, float w, float h)
    {
        b.Rect(x - 1, y - 1, w + 2, h + 2, new Rgba(0xFF1A1612));
        Vector2 At(Vector2 p) => new(x + p.X * w, y + p.Y * h);
        foreach (var shape in flag.Shapes)
        {
            var color = new Rgba(shape.Color);
            switch (shape)
            {
                case FlagBand r:
                    b.Rect(x + r.X0 * w, y + r.Y0 * h, (r.X1 - r.X0) * w, (r.Y1 - r.Y0) * h, color);
                    break;
                case FlagDisc d when d.LowerHalf:
                {
                    var centre = At(new(d.X, d.Y));
                    float radius = d.Radius * h;
                    const int segments = 12;
                    for (int i = 0; i < segments; i++)
                    {
                        float a0 = MathF.PI * i / segments, a1 = MathF.PI * (i + 1) / segments;
                        b.Triangle(centre, centre + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius, centre + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius, color);
                    }
                    break;
                }
                case FlagDisc d:
                    if (d.Radius > 0) b.Circle(At(new(d.X, d.Y)), d.Radius * h, color, segments: 16);
                    break;
                case FlagStar s:
                    Star(b, At(new(s.X, s.Y)), s.Radius * h, color);
                    break;
                case FlagTriangle t:
                    b.Triangle(At(t.A), At(t.B), At(t.C), color);
                    break;
                case FlagLine l:
                    b.Line(At(l.A), At(l.B), color, Math.Max(1, l.Thickness * h));
                    break;
            }
        }
    }

    /// <summary>A filled five-pointed star pointing up: ten triangles from its centre to its points and notches.</summary>
    private static void Star(Batch2D b, Vector2 centre, float radius, Rgba color)
    {
        Vector2 Point(int i)
        {
            float a = -MathF.PI / 2 + i * MathF.PI / 5, r = i % 2 == 0 ? radius : radius * 0.4f;
            return centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
        }
        for (int i = 0; i < 10; i++) b.Triangle(centre, Point(i), Point(i + 1), color);
    }
}
