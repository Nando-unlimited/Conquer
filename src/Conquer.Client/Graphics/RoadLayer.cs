using System.Numerics;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Client.Graphics;

/// <summary>
/// Roads and railways as lines between province centres, in the manner of Hearts of Iron: a road is a pale track
/// with a dark edge, a railway a dark line with sleepers across it. The player's works under way show as dashes
/// along their route, and a planned route (while choosing where a new one goes) as a bright dashed line.
/// Zoomed far out they are hidden so they do not clutter the map.
/// </summary>
public sealed class RoadLayer
{
    private static readonly Rgba RoadEdge = new(0xFF4A3A22);
    private static readonly Rgba RoadFill = new(0xFFD8B878);
    private static readonly Rgba Rail = new(0xFF26221E);
    private static readonly Rgba Work = new(0xFFE0B656);

    /// <summary>Roads show from this zoom on.</summary>
    public const float MinZoom = 1.2f;

    private readonly WorldMap _map;

    public RoadLayer(WorldMap map) => _map = map;

    private Vector2 Center(int id) => new(_map.Provinces[id].CenterX + 0.5f, _map.Provinces[id].CenterY + 0.5f);

    /// <summary>A stretch on screen, from <paramref name="a"/>'s nearest copy to <paramref name="b"/> the short way across the date line.</summary>
    private (Vector2 A, Vector2 B) OnScreen(Camera camera, int a, int b)
    {
        var from = camera.MapToScreen(Center(a));
        var d = Center(b) - Center(a);
        d.X -= _map.Width * MathF.Round(d.X / _map.Width);
        return (from, from + d * camera.Zoom);
    }

    private static bool Visible(Camera camera, Vector2 a, Vector2 b) =>
        MathF.Max(a.X, b.X) > -20 && MathF.Min(a.X, b.X) < camera.Screen.X + 20 && MathF.Max(a.Y, b.Y) > -20 && MathF.Min(a.Y, b.Y) < camera.Screen.Y + 20;

    public void Draw(Batch2D batch, Camera camera, RoadNetwork roads, IEnumerable<RoadProject> works, IReadOnlyList<int>? planned)
    {
        float zoom = camera.Zoom;
        if (zoom >= MinZoom)
        {
            float scale = Math.Clamp(zoom / 4, 0.5f, 2.5f);
            float alpha = Math.Clamp((zoom - MinZoom) / 0.8f, 0, 1);
            // Roads under railways, so a railway laid over a road stays on top.
            foreach (var kind in RoadKinds.All)
                foreach (var (a, b, k) in roads.Links)
                {
                    if (k != kind) continue;
                    var (p, q) = OnScreen(camera, a, b);
                    if (!Visible(camera, p, q)) continue;
                    if (k == RoadKind.Road) DrawRoad(batch, p, q, scale, alpha);
                    else DrawRailway(batch, p, q, scale, alpha);
                }
            foreach (var work in works)
                for (int i = work.Next; i + 1 < work.Route.Count; i++)
                {
                    if (roads.Has(work.Route[i], work.Route[i + 1], work.Kind)) continue;
                    var (p, q) = OnScreen(camera, work.Route[i], work.Route[i + 1]);
                    if (Visible(camera, p, q)) Dashed(batch, p, q, Work.WithAlpha(0.8f * alpha), 1.5f * scale, 5 * scale);
                }
        }
        if (planned == null) return;
        for (int i = 0; i + 1 < planned.Count; i++)
        {
            var (p, q) = OnScreen(camera, planned[i], planned[i + 1]);
            Dashed(batch, p, q, Rgba.Black.WithAlpha(0.6f), 5, 8);
            Dashed(batch, p, q, Work, 3, 8);
        }
    }

    private static void DrawRoad(Batch2D batch, Vector2 a, Vector2 b, float scale, float alpha)
    {
        float edge = 2.2f * scale, fill = 1.2f * scale;
        batch.Line(a, b, RoadEdge.WithAlpha(0.85f * alpha), edge * 2);
        batch.Circle(a, edge, RoadEdge.WithAlpha(0.85f * alpha), segments: 8);
        batch.Circle(b, edge, RoadEdge.WithAlpha(0.85f * alpha), segments: 8);
        batch.Line(a, b, RoadFill.WithAlpha(alpha), fill * 2);
        batch.Circle(a, fill, RoadFill.WithAlpha(alpha), segments: 8);
        batch.Circle(b, fill, RoadFill.WithAlpha(alpha), segments: 8);
    }

    /// <summary>A dark line with sleepers across it every few pixels.</summary>
    private static void DrawRailway(Batch2D batch, Vector2 a, Vector2 b, float scale, float alpha)
    {
        var color = Rail.WithAlpha(0.9f * alpha);
        batch.Line(a, b, color, 2f * scale);
        batch.Circle(a, scale, color, segments: 8);
        batch.Circle(b, scale, color, segments: 8);
        var d = b - a;
        float length = d.Length();
        if (length < 1 || scale < 0.7f) return;
        var along = d / length;
        var across = new Vector2(-along.Y, along.X) * 3.2f * scale;
        float step = 6 * scale;
        for (float t = step / 2; t < length; t += step)
        {
            var c = a + along * t;
            batch.Line(c - across, c + across, color, 1.2f * scale);
        }
    }

    private static void Dashed(Batch2D batch, Vector2 a, Vector2 b, Rgba color, float width, float dash)
    {
        var d = b - a;
        float length = d.Length();
        if (length < 0.5f) return;
        var along = d / length;
        for (float t = 0; t < length; t += 2 * dash)
            batch.Line(a + along * t, a + along * MathF.Min(t + dash, length), color, width);
    }
}
