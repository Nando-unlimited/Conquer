using System.Numerics;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Client.Graphics;

/// <summary>
/// Rivers drawn as continuous strokes: the segments of each stretch are joined into one path whose width
/// follows the water it carries, with anti-aliased edges, darker banks, a light glint down the middle of wide
/// rivers and, on the terrain map, a green floodplain along the great rivers (the ones that make land fertile).
/// Zoomed out only the great rivers show; the smaller ones appear as you zoom in.
/// </summary>
public sealed class RiverLayer
{
    private static readonly Rgba Bank = new(0xFF1F4A78);
    private static readonly Rgba Water = new(0xFF3F7FC8);
    private static readonly Rgba Glint = new(0xFFA8D2F5);
    private static readonly Rgba Floodplain = new(0xFF4E8A34);

    /// <summary>One stretch, with x unwrapped so it runs continuously across the date line.</summary>
    private sealed record Stretch(Vector2[] Points, float[] Flow);

    private readonly List<Stretch> _stretches = [];
    private readonly int _mapWidth;

    public RiverLayer(WorldMap map)
    {
        _mapWidth = map.Width;
        var points = new List<Vector2>();
        var flow = new List<float>();
        foreach (var s in map.Rivers)
        {
            // Segments come stretch by stretch, each starting where the last one ended (modulo the date line).
            bool continues = points.Count > 0 && Math.Abs(s.Y1 - points[^1].Y) < 0.01f
                             && Math.Abs(Wrap(s.X1) - Wrap(points[^1].X)) < 0.01f;
            if (!continues)
            {
                Finish();
                points.Add(new(s.X1, s.Y1));
                flow.Add(s.Flow);
            }
            else flow[^1] = s.Flow;
            points.Add(points[^1] + new Vector2(s.X2 - s.X1, s.Y2 - s.Y1));
            flow.Add(s.Flow);
        }
        Finish();

        void Finish()
        {
            if (points.Count >= 2) _stretches.Add(new([.. points], [.. flow]));
            points.Clear();
            flow.Clear();
        }
    }

    private float Wrap(float x) => ((x % _mapWidth) + _mapWidth) % _mapWidth;

    public void Draw(Batch2D batch, Camera camera, bool terrainMode)
    {
        float zoom = camera.Zoom;
        float minFlow = GameRules.MinRiverFlow * MathF.Max(1, MathF.Pow(6 / zoom, 1.5f));
        float scale = Math.Clamp(zoom / 4, 0.35f, 3f);
        bool floodplain = terrainMode && zoom >= 3;

        var screen = new List<Vector2>();
        var half = new List<float>();
        var flows = new List<float>();
        foreach (var stretch in _stretches)
        {
            if (stretch.Flow[^1] < minFlow) continue;

            // Screen points from the first point's nearest copy of the planet, so the path stays continuous.
            var origin = camera.MapToScreen(stretch.Points[0]);
            screen.Clear();
            half.Clear();
            flows.Clear();
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < stretch.Points.Length; i++)
            {
                if (stretch.Flow[i] < minFlow) continue;
                var p = origin + (stretch.Points[i] - stretch.Points[0]) * zoom;
                screen.Add(p);
                flows.Add(stretch.Flow[i]);
                half.Add(MathF.Max(0.8f, (0.7f + 1.3f * MathF.Log10(stretch.Flow[i] / GameRules.MinRiverFlow)) * scale) / 2);
                minX = MathF.Min(minX, p.X); maxX = MathF.Max(maxX, p.X);
                minY = MathF.Min(minY, p.Y); maxY = MathF.Max(maxY, p.Y);
            }
            if (screen.Count < 2) continue;

            // A long stretch near the date line may show on the other copy of the planet too.
            float worldPx = _mapWidth * zoom;
            for (int copy = -1; copy <= 1; copy++)
            {
                float shift = copy * worldPx;
                if (maxX + shift < -60 || minX + shift > camera.Screen.X + 60 || maxY < -60 || minY > camera.Screen.Y + 60) continue;
                var offset = new Vector2(shift, 0);

                if (floodplain && flows[^1] >= GameRules.GreatRiverFlow)
                    Strokes.Along(batch, screen, offset, i => flows[i] >= GameRules.GreatRiverFlow ? half[i] * 2 + 2 * scale : 0,
                           Floodplain.WithAlpha(0.4f), feather: 6 * scale);
                Strokes.Along(batch, screen, offset, i => half[i] + 0.7f, Bank.WithAlpha(0.75f), feather: 1);
                Strokes.Along(batch, screen, offset, i => half[i] - 0.3f, Water, feather: 1);
                if (zoom >= 4)
                    Strokes.Along(batch, screen, offset, i => half[i] >= 1.6f ? half[i] * 0.3f : 0, Glint.WithAlpha(0.45f), feather: 1, fadeToEdge: true);
            }
        }
    }
}
