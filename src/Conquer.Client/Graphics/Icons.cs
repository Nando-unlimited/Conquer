using System.Numerics;
using Conquer.Game.Economy;
using Conquer.Presentation;

namespace Conquer.Client.Graphics;

/// <summary>
/// Small icons drawn from shapes, so they scale and need no image files: an ear of wheat for food, a log
/// for wood, ingots for the metals, coins for gold and silver, a crystal for silicon, a drop for oil and a
/// tyre for rubber. Deposit resources use their map colour.
/// </summary>
public static class Icons
{
    private static readonly Rgba Outline = new(0xC0000000);

    /// <summary>The resources' own pictures, from Assets/ResourceIcons (named by <see cref="ResourceIcon.Slug"/>); they replace the drawn ones.</summary>
    private static SpriteAtlas? _pictures;

    public static void Load(Silk.NET.OpenGL.GL gl) => _pictures ??= new SpriteAtlas(gl, "ResourceIcons", maxSize: 64);

    /// <summary>Whether there is a picture of that name beside the resources' (such as <see cref="ScienceIcon.Name"/>).</summary>
    public static bool HasPicture(string name) => _pictures?.Has(name) == true;

    /// <summary>The picture of that name, <paramref name="size"/> pixels across, centred on <paramref name="c"/>, if there is one.</summary>
    public static void Picture(Batch2D b, string name, Vector2 c, float size)
    {
        if (HasPicture(name)) _pictures!.DrawCover(b, name, c - new Vector2(size / 2), c + new Vector2(size / 2));
    }

    /// <summary>The icon of a resource, <paramref name="size"/> pixels across, centred on <paramref name="c"/>: its picture if there is one, or drawn.</summary>
    public static void Resource(Batch2D b, ResourceType type, Vector2 c, float size)
    {
        float s = size / 2;
        if (_pictures is { } pictures && pictures.Has(ResourceIcon.Slug(type)))
        {
            pictures.DrawCover(b, ResourceIcon.Slug(type), c - new Vector2(s), c + new Vector2(s));
            return;
        }
        var color = MapRenderer.ResourceColor(type);
        switch (type)
        {
            case ResourceType.Food: Wheat(b, c, s); break;
            case ResourceType.Wood: Log(b, c, s); break;
            case ResourceType.Coal:
                Lump(b, c + new Vector2(-0.35f, 0.25f) * s, 0.45f * s, color);
                Lump(b, c + new Vector2(0.35f, 0.3f) * s, 0.4f * s, color);
                Lump(b, c + new Vector2(0, -0.25f) * s, 0.5f * s, color.Scale(1.3f));
                break;
            case ResourceType.Gold or ResourceType.Silver: Coin(b, c, s, color); break;
            case ResourceType.Silicon: Crystal(b, c, s, color); break;
            case ResourceType.Oil: Drop(b, c, s, color); break;
            case ResourceType.Rubber:
                b.Circle(c, 0.9f * s, Outline, 0.35f * s);
                b.Circle(c, 0.8f * s, new Rgba(0xFF2C2C2C), 0.45f * s);
                b.Circle(c, 0.45f * s, color, 0.3f * s);
                break;
            default: Ingot(b, c, s, color); break;
        }
    }

    private static void Wheat(Batch2D b, Vector2 c, float s)
    {
        var gold = new Rgba(0xFFE3C15A);
        b.Line(c + new Vector2(0, s), c + new Vector2(0, -0.5f * s), new Rgba(0xFFB08A3A), MathF.Max(1.5f, s * 0.15f));
        for (int i = 0; i < 3; i++)
        {
            float y = -0.45f * s + i * 0.38f * s;
            b.Circle(c + new Vector2(-0.3f * s, y), 0.22f * s, gold, segments: 8);
            b.Circle(c + new Vector2(0.3f * s, y), 0.22f * s, gold, segments: 8);
        }
        b.Circle(c + new Vector2(0, -0.8f * s), 0.22f * s, gold, segments: 8);
    }

    private static void Log(Batch2D b, Vector2 c, float s)
    {
        b.RoundedRect(c.X - 0.95f * s, c.Y - 0.45f * s, 1.6f * s, 0.9f * s, 0.2f * s, new Rgba(0xFFA06A34), new Rgba(0xFF6E4520));
        b.Circle(c + new Vector2(0.6f * s, 0), 0.47f * s, new Rgba(0xFF6E4520), segments: 12);
        b.Circle(c + new Vector2(0.6f * s, 0), 0.4f * s, new Rgba(0xFFD8AE72), segments: 12);
        b.Circle(c + new Vector2(0.6f * s, 0), 0.2f * s, new Rgba(0xFFA87C44), 0.12f * s, 10);
    }

    private static void Lump(Batch2D b, Vector2 c, float r, Rgba color)
    {
        b.Circle(c, r + 1, Outline, segments: 8);
        b.Circle(c, r, color, segments: 8);
    }

    /// <summary>A bar of metal seen from the front: a trapezoid with a lighter top face.</summary>
    private static void Ingot(Batch2D b, Vector2 c, float s, Rgba color)
    {
        var bl = c + new Vector2(-0.95f, 0.55f) * s;
        var br = c + new Vector2(0.95f, 0.55f) * s;
        var tl = c + new Vector2(-0.6f, -0.1f) * s;
        var tr = c + new Vector2(0.6f, -0.1f) * s;
        var dark = color.Scale(0.7f);
        b.Triangle(bl, br, tr, color, dark, color);
        b.Triangle(bl, tr, tl, color, color, color);
        var top = color.Scale(1.25f);
        b.Triangle(tl, tr, c + new Vector2(0.45f, -0.45f) * s, top);
        b.Triangle(tl, c + new Vector2(0.45f, -0.45f) * s, c + new Vector2(-0.45f, -0.45f) * s, top);
        b.Line(bl, br, Outline, 1);
    }

    private static void Coin(Batch2D b, Vector2 c, float s, Rgba color)
    {
        b.Circle(c, 0.9f * s, Outline);
        b.Circle(c, 0.8f * s, color.Scale(0.8f));
        b.Circle(c, 0.62f * s, color);
        b.Circle(c + new Vector2(-0.2f, -0.2f) * s, 0.18f * s, Rgba.White.WithAlpha(0.6f), segments: 8);
    }

    private static void Crystal(Batch2D b, Vector2 c, float s, Rgba color)
    {
        var top = c + new Vector2(0, -0.95f * s);
        var bottom = c + new Vector2(0, 0.95f * s);
        var left = c + new Vector2(-0.6f * s, -0.1f * s);
        var right = c + new Vector2(0.6f * s, -0.1f * s);
        b.Triangle(top, left, c, color.Scale(1.2f));
        b.Triangle(top, c, right, color);
        b.Triangle(left, bottom, c, color.Scale(0.85f));
        b.Triangle(c, bottom, right, color.Scale(0.65f));
    }

    private static void Drop(Batch2D b, Vector2 c, float s, Rgba color)
    {
        var round = c + new Vector2(0, 0.3f * s);
        b.Circle(round, 0.62f * s, color);
        b.Triangle(c + new Vector2(0, -0.95f * s), round + new Vector2(-0.6f * s, -0.1f * s), round + new Vector2(0.6f * s, -0.1f * s), color);
        b.Circle(round + new Vector2(-0.22f, -0.15f) * s, 0.15f * s, Rgba.White.WithAlpha(0.5f), segments: 8);
    }
}
