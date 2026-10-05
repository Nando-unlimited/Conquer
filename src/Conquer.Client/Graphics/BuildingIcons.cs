using Conquer.Game.Buildings;
using Conquer.Presentation;

namespace Conquer.Client.Graphics;

/// <summary>
/// The buildings' own pictures, from Assets/BuildingIcons (named by <see cref="BuildingIcon.Slug"/>: granja.png,
/// central-electrica.png...), and the cities' (<see cref="Models.CityIcons"/>), loaded once. A building without one
/// shows no icon.
/// </summary>
public static class BuildingIcons
{
    private static SpriteAtlas? _icons;

    public static void Load(Silk.NET.OpenGL.GL gl) => _icons ??= new SpriteAtlas(gl, "BuildingIcons", maxSize: 128);

    public static bool Has(BuildingType building) => Has(BuildingIcon.Slug(building));

    public static bool Has(string name) => _icons?.Has(name) == true;

    /// <summary>The building's picture filling the square at (<paramref name="x"/>, <paramref name="y"/>), if it has one.</summary>
    public static void Draw(Batch2D b, BuildingType building, float x, float y, float size) => Draw(b, BuildingIcon.Slug(building), x, y, size);

    /// <summary>The picture of that name (a building's or a city's) filling the square at (<paramref name="x"/>, <paramref name="y"/>), if there is one.</summary>
    public static void Draw(Batch2D b, string name, float x, float y, float size)
    {
        if (Has(name)) _icons!.DrawCover(b, name, new(x, y), new(x + size, y + size));
    }
}
