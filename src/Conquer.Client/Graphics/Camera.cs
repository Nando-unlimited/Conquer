using System.Numerics;

namespace Conquer.Client.Graphics;

/// <summary>
/// View onto the map. Positions are in map pixels; X wraps around the planet, Y is clamped.
/// </summary>
public sealed class Camera
{
    public const float MinZoom = 0.3f;
    public const float MaxZoom = 40f;

    private readonly int _mapWidth, _mapHeight;

    public Vector2 Center { get; set; }
    /// <summary>Screen pixels per map pixel.</summary>
    public float Zoom { get; private set; } = 4;
    public Vector2 Screen { get; set; }

    public Camera(int mapWidth, int mapHeight)
    {
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        Center = new(mapWidth / 2f, mapHeight / 2f);
    }

    public Vector2 ScreenToMap(Vector2 screen) => Center + (screen - Screen / 2) / Zoom;

    /// <summary>Screen position of a map point, using the copy of the planet nearest the view centre.</summary>
    public Vector2 MapToScreen(Vector2 map)
    {
        float dx = map.X - Center.X;
        dx -= _mapWidth * MathF.Round(dx / _mapWidth);
        return new Vector2(dx, map.Y - Center.Y) * Zoom + Screen / 2;
    }

    public void Pan(Vector2 screenDelta)
    {
        Center -= screenDelta / Zoom;
        Clamp();
    }

    /// <summary>Zooms keeping the map point under <paramref name="anchor"/> fixed on screen.</summary>
    public void ZoomAt(Vector2 anchor, float factor)
    {
        var before = ScreenToMap(anchor);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        var after = ScreenToMap(anchor);
        Center += before - after;
        Clamp();
    }

    public void LookAt(Vector2 map, float? zoom = null)
    {
        if (zoom.HasValue) Zoom = Math.Clamp(zoom.Value, MinZoom, MaxZoom);
        Center = map;
        Clamp();
    }

    private void Clamp()
    {
        float x = Center.X % _mapWidth;
        if (x < 0) x += _mapWidth;
        float halfView = Screen.Y / 2 / Zoom;
        float y = _mapHeight < 2 * halfView ? _mapHeight / 2f : Math.Clamp(Center.Y, halfView, _mapHeight - halfView);
        Center = new(x, y);
    }
}
