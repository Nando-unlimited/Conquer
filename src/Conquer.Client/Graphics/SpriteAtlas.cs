using System.Numerics;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace Conquer.Client.Graphics;

/// <summary>
/// The colour pictures of the map (the isometric models of cities, buildings, units, ships and planes, in
/// Assets/Sprites) packed into one mipmapped texture, so they stay smooth when drawn smaller than they are. A model
/// may have a second picture, name-team, of the parts painted in its nation's colour (uniforms, flags, sails), drawn
/// over it tinted.
/// </summary>
public sealed class SpriteAtlas : IDisposable
{
    /// <summary>Empty pixels around each picture, so the smaller mipmaps don't blend neighbours together.</summary>
    private const int Padding = 8;

    private readonly Dictionary<string, (Vector2 UvMin, Vector2 UvMax, Vector2 Size)> _sprites = [];

    public Texture Texture { get; }

    /// <summary>Loads every embedded picture whose resource name starts with "Sprites/".</summary>
    public SpriteAtlas(GL gl)
    {
        var assembly = typeof(SpriteAtlas).Assembly;
        var images = assembly.GetManifestResourceNames().Where(n => n.StartsWith("Sprites/", StringComparison.Ordinal) && n.EndsWith(".png", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                return (Name: n["Sprites/".Length..^".png".Length], Image: ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha));
            }).ToList();

        // Shelf packing in rows of a fixed width, tallest first.
        const int width = 2048;
        var places = new Dictionary<string, (int X, int Y)>();
        int x = 0, y = 0, rowHeight = 0;
        foreach (var (name, image) in images.OrderByDescending(i => i.Image.Height))
        {
            int w = image.Width + 2 * Padding, h = image.Height + 2 * Padding;
            if (x + w > width)
            {
                x = 0;
                y += rowHeight;
                rowHeight = 0;
            }
            places[name] = (x + Padding, y + Padding);
            x += w;
            rowHeight = Math.Max(rowHeight, h);
        }
        int height = Math.Max(1, y + rowHeight);

        var pixels = new byte[width * height * 4];
        foreach (var (name, image) in images)
        {
            var (left, top) = places[name];
            for (int row = 0; row < image.Height; row++)
                Array.Copy(image.Data, row * image.Width * 4, pixels, ((top + row) * width + left) * 4, image.Width * 4);
            _sprites[name] = (new Vector2(left, top) / new Vector2(width, height),
                new Vector2(left + image.Width, top + image.Height) / new Vector2(width, height), new Vector2(image.Width, image.Height));
        }
        Bleed(pixels, width, height);
        Texture = new Texture(gl, width, height, pixels, smooth: true, mipmaps: true);
    }

    /// <summary>
    /// Gives the see-through pixels next to the pictures the colour of their neighbours, a few pixels out, so blending
    /// towards them at the edges (and in the smaller mipmaps) doesn't darken the outlines.
    /// </summary>
    private static void Bleed(byte[] pixels, int width, int height)
    {
        var filled = new bool[width * height];
        for (int i = 0; i < filled.Length; i++) filled[i] = pixels[i * 4 + 3] > 0;
        for (int pass = 0; pass < Padding; pass++)
        {
            var next = (bool[])filled.Clone();
            for (int py = 0; py < height; py++)
                for (int px = 0; px < width; px++)
                {
                    int i = py * width + px;
                    if (filled[i]) continue;
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int qx = px + dx, qy = py + dy;
                            if (qx < 0 || qy < 0 || qx >= width || qy >= height || !filled[qy * width + qx]) continue;
                            int j = (qy * width + qx) * 4;
                            r += pixels[j];
                            g += pixels[j + 1];
                            b += pixels[j + 2];
                            n++;
                        }
                    if (n == 0) continue;
                    pixels[i * 4] = (byte)(r / n);
                    pixels[i * 4 + 1] = (byte)(g / n);
                    pixels[i * 4 + 2] = (byte)(b / n);
                    next[i] = true;
                }
            filled = next;
        }
    }

    public bool Has(string name) => _sprites.ContainsKey(name);

    /// <summary>A picture's size in its own pixels, or zero if there is none of that name.</summary>
    public Vector2 SizeOf(string name) => _sprites.TryGetValue(name, out var s) ? s.Size : Vector2.Zero;

    /// <summary>
    /// Draws model <paramref name="name"/> as big as fits in <paramref name="box"/> with its bottom centre at
    /// <paramref name="foot"/>, its team parts in <paramref name="team"/>, <paramref name="mirrored"/> to face left.
    /// Returns its size on screen (zero if there is no such model).
    /// </summary>
    public Vector2 Draw(Batch2D batch, string name, Vector2 foot, Vector2 box, Rgba team, bool mirrored = false, float alpha = 1)
    {
        if (!_sprites.TryGetValue(name, out var sprite)) return Vector2.Zero;
        float scale = MathF.Min(box.X / sprite.Size.X, box.Y / sprite.Size.Y);
        var size = sprite.Size * scale;
        var p0 = new Vector2(foot.X - size.X / 2, foot.Y - size.Y);
        var p1 = p0 + size;
        Quad(batch, sprite, p0, p1, Rgba.White.WithAlpha(alpha), mirrored);
        if (_sprites.TryGetValue(name + "-team", out var teamSprite)) Quad(batch, teamSprite, p0, p1, team.WithAlpha(alpha), mirrored);
        return size;
    }

    private void Quad(Batch2D batch, (Vector2 UvMin, Vector2 UvMax, Vector2 Size) s, Vector2 p0, Vector2 p1, Rgba tint, bool mirrored)
    {
        var (uv0, uv1) = mirrored ? (new Vector2(s.UvMax.X, s.UvMin.Y), new Vector2(s.UvMin.X, s.UvMax.Y)) : (s.UvMin, s.UvMax);
        batch.Quad(Texture, p0, p1, uv0, uv1, tint);
    }

    public void Dispose() => Texture.Dispose();
}
