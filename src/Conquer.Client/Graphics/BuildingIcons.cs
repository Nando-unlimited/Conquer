using Conquer.Game.Buildings;
using Conquer.Presentation;

namespace Conquer.Client.Graphics;

/// <summary>
/// The buildings' own pictures, from Assets/BuildingIcons (named by <see cref="BuildingIcon.Slug"/>: granja.png,
/// central-electrica.png...), loaded once. A building without one shows no icon.
/// </summary>
public static class BuildingIcons
{
    private static SpriteAtlas? _icons;

    public static void Load(Silk.NET.OpenGL.GL gl) => _icons ??= new SpriteAtlas(gl, "BuildingIcons", maxSize: 64);

    public static bool Has(BuildingType building) => _icons?.Has(BuildingIcon.Slug(building)) == true;

    /// <summary>The building's picture filling the square at (<paramref name="x"/>, <paramref name="y"/>), if it has one.</summary>
    public static void Draw(Batch2D b, BuildingType building, float x, float y, float size)
    {
        if (Has(building)) _icons!.DrawCover(b, BuildingIcon.Slug(building), new(x, y), new(x + size, y + size));
    }
}
