using System.Numerics;
using Conquer.Client.UI;
using Conquer.Game.Science;
using Conquer.Presentation;

namespace Conquer.Client.Graphics;

/// <summary>
/// Paints an officer's portrait from shapes (<see cref="Portrait"/>): head and shoulders with their face, in the uniform
/// and headgear of their era (crested bronze helmet, nasal helm, tricorne or bicorne, kepi, peaked cap) in their
/// nation's colour, with bars (a colonel) or stars (generals) on the shoulders and a ribbon for each star beyond the
/// first. Brought from the other version of the game. Officers with a painted portrait in Assets/Portraits take that instead.
/// </summary>
public static class PortraitPainter
{
    private static Rgba Hex(uint rgb) => new(0xFF000000 | rgb);

    private static readonly Rgba[] Skin = [Hex(0xf2d6c1), Hex(0xe8b99a), Hex(0xd19a74), Hex(0xb07a52), Hex(0x8a5a3a), Hex(0x5e3b25)];
    private static readonly Rgba[] Hair = [Hex(0x1e1a18), Hex(0x4a3222), Hex(0x7a5232), Hex(0xc9a15a), Hex(0x9a4a26)];
    private static readonly Rgba[] Ribbons = [Hex(0xb0302a), Hex(0x2a5aa8), Hex(0xe0c040), Hex(0x2e8a4a), Hex(0x6a3a8a), Hex(0xe0e0e0)];
    private static readonly Rgba Grey = Hex(0xcfcfcf), Gold = Hex(0xd9b44a), DarkGold = Hex(0x9c7a24), Ink = Hex(0x1a1410),
        Lips = Hex(0x7a3a30), FemaleLips = Hex(0xa8453a), Bronze = Hex(0x8f6a28), Steel = Hex(0x9aa0a8), Crest = Hex(0xa8281e),
        HatBlack = Hex(0x161616), NavyBlue = Hex(0x1c2740), White = Hex(0xe8e4d8), Backdrop = Hex(0x20252e);

    private static SpriteAtlas? _photos;
    private static Dictionary<string, string[]> _groups = [];

    /// <summary>
    /// The painted portraits of Assets/Portraits, loaded once: "renacimiento-hombre-03" belongs to the group
    /// "renacimiento-hombre". Officers of an era and sex (or sailors) with pictures take one of them; the rest are drawn.
    /// </summary>
    public static void LoadPhotos(Silk.NET.OpenGL.GL gl)
    {
        if (_photos != null) return;
        _photos = new SpriteAtlas(gl, "Portraits", maxSize: 256);
        _groups = _photos.Names.GroupBy(GroupOf).ToDictionary(g => g.Key, g => g.Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>"renacimiento-hombre-03" is in "renacimiento-hombre": the name without its last part if that is a number.</summary>
    public static string GroupOf(string name)
    {
        int dash = name.LastIndexOf('-');
        return dash > 0 && int.TryParse(name[(dash + 1)..], out _) ? name[..dash] : name;
    }

    /// <summary>The officer's portrait: a painted one of their group if there is any, or one drawn from shapes.</summary>
    public static void Draw(Batch2D b, Rect rect, Portrait face)
    {
        if (_photos != null && face.PhotoGroups.Select(g => _groups.GetValueOrDefault(g)).FirstOrDefault(g => g is { Length: > 0 }) is { } group)
            DrawPhoto(b, rect, face, group[face.Pick % group.Length]);
        else
            DrawShapes(b, rect, face);
    }

    /// <summary>
    /// A painted portrait in a frame of the nation's colour, with the rank on a band along the bottom: three bars for a
    /// colonel, a star for each grade of general.
    /// </summary>
    private static void DrawPhoto(Batch2D b, Rect rect, Portrait face, string name)
    {
        var country = new Rgba(face.Color | 0xFF000000);
        _photos!.DrawCover(b, name, new Vector2(rect.X, rect.Y), new Vector2(rect.Right, rect.Bottom));
        float frame = Math.Max(2, rect.W / 28f);
        b.Rect(rect.X, rect.Y, rect.W, frame, country);
        b.Rect(rect.X, rect.Bottom - frame, rect.W, frame, country);
        b.Rect(rect.X, rect.Y, frame, rect.H, country);
        b.Rect(rect.Right - frame, rect.Y, frame, rect.H, country);
        if (rect.W < 40) return;
        float band = rect.H * 0.16f, top = rect.Bottom - frame - band;
        b.Rect(rect.X + frame, top, rect.W - 2 * frame, band, Rgba.Black.WithAlpha(0.6f));
        float size = band * 0.32f, cy = top + band / 2;
        int marks = face.Rank >= 1 ? face.Rank : 3;
        float step = size * 2.6f, x = rect.X + rect.W / 2 - (marks - 1) * step / 2;
        for (int i = 0; i < marks; i++, x += step)
        {
            if (face.Rank >= 1)
            {
                b.Triangle(new(x, cy - size * 1.3f), new(x + size * 0.45f, cy), new(x, cy + size * 1.3f), Gold);
                b.Triangle(new(x, cy - size * 1.3f), new(x - size * 0.45f, cy), new(x, cy + size * 1.3f), Gold);
                b.Triangle(new(x - size * 1.3f, cy), new(x, cy - size * 0.45f), new(x + size * 1.3f, cy), Gold);
                b.Triangle(new(x - size * 1.3f, cy), new(x, cy + size * 0.45f), new(x + size * 1.3f, cy), Gold);
            }
            else b.Rect(x - size * 0.4f, cy - size * 1.2f, size * 0.8f, size * 2.4f, Gold);
        }
    }

    private static void DrawShapes(Batch2D b, Rect rect, Portrait face)
    {
        Vector2 P(float u, float v) => new(rect.X + u * rect.W, rect.Y + v * rect.H);
        Vector2 R(float u, float v) => new(u * rect.W, v * rect.H);
        void Ellipse(Vector2 c, Vector2 r, Rgba color) => b.Ellipse(c, r.X, r.Y, color, segments: 20);
        void Box(Vector2 p, Vector2 s, Rgba color) => b.Rect(p.X, p.Y, s.X, s.Y, color);
        void Polygon(Rgba color, params Vector2[] points)
        {
            for (int i = 1; i < points.Length - 1; i++) b.Triangle(points[0], points[i], points[i + 1], color);
        }

        var country = new Rgba(face.Color | 0xFF000000);
        var skin = Skin[face.SkinTone % Skin.Length];
        var shade = Darken(skin, 0.18f);
        var hair = Batch2D.Mix(Hair[face.HairColor % Hair.Length], Grey, face.Greying);
        var uniform = UniformColor(face, country);
        float faceW = 0.19f + 0.035f * face.FaceWidth;
        float jawW = faceW * (0.5f + 0.3f * face.JawWidth);

        // Background
        var tint = Batch2D.Mix(Darken(country, 0.55f), Backdrop, 0.35f + 0.1f * face.Accent);
        b.Rect(rect.X, rect.Y, rect.W, rect.H, tint);
        Ellipse(P(0.5f, 0.42f), R(0.42f, 0.36f), Batch2D.Mix(tint, White, 0.08f));

        // Long hair falls behind the shoulders.
        if (face.Hair == HairStyle.Long) Ellipse(P(0.5f, 0.48f), R(faceW + 0.05f, 0.28f), hair);

        // Shoulders, neck and collar
        Ellipse(P(0.5f, 0.9f), R(0.47f, 0.1f), uniform);
        Box(P(0.03f, 0.9f), R(0.94f, 0.1f), uniform);
        Box(P(0.43f, 0.6f), R(0.14f, 0.2f), shade);
        b.Triangle(P(0.36f, 0.8f), P(0.5f, 0.8f), P(0.43f, 0.9f), Darken(uniform, 0.25f));
        b.Triangle(P(0.5f, 0.8f), P(0.64f, 0.8f), P(0.57f, 0.9f), Darken(uniform, 0.25f));
        b.Triangle(P(0.43f, 0.79f), P(0.57f, 0.79f), P(0.5f, 0.87f), Lighten(country, 0.1f));
        Insignia(face.Rank, uniform);
        for (int i = 0; i < face.Skill - 1; i++)
        {
            float x = 0.6f + i % 2 * 0.1f, y = 0.93f - i / 2 * 0.035f;
            Box(P(x, y), R(0.09f, 0.028f), Ribbons[i % Ribbons.Length]);
            Box(P(x + 0.035f, y), R(0.02f, 0.028f), Ribbons[(i + 2) % Ribbons.Length]);
        }

        // Head
        Ellipse(P(0.5f - faceW, 0.47f), R(0.035f, 0.055f), shade);
        Ellipse(P(0.5f + faceW, 0.47f), R(0.035f, 0.055f), shade);
        Ellipse(P(0.5f, 0.44f), R(faceW, 0.2f), skin);
        Polygon(skin, P(0.5f - faceW, 0.44f), P(0.5f + faceW, 0.44f), P(0.5f + jawW, 0.6f), P(0.5f, 0.67f), P(0.5f - jawW, 0.6f));
        HairOf(face.Hair);

        // Face
        float tilt = face.BrowTilt * 0.012f, brow = (face.IsFemale ? 1.2f : 2f) * rect.W / 60f;
        b.Line(P(0.37f, 0.41f + tilt), P(0.45f, 0.415f - tilt), Darken(hair, 0.2f), brow);
        b.Line(P(0.55f, 0.415f - tilt), P(0.63f, 0.41f + tilt), Darken(hair, 0.2f), brow);
        Ellipse(P(0.415f, 0.455f), R(0.024f, 0.013f), White);
        Ellipse(P(0.585f, 0.455f), R(0.024f, 0.013f), White);
        Ellipse(P(0.418f, 0.456f), R(0.011f, 0.012f), Ink);
        Ellipse(P(0.588f, 0.456f), R(0.011f, 0.012f), Ink);
        float nose = 0.022f + 0.02f * face.NoseSize;
        b.Triangle(P(0.5f, 0.47f), P(0.5f - nose, 0.545f), P(0.5f + nose * 0.4f, 0.55f), shade);
        if (face.Age > 0.6f)
        {
            var wrinkle = Darken(skin, 0.12f);
            float t = Math.Max(1, rect.W / 90f);
            b.Line(P(0.43f, 0.36f), P(0.57f, 0.36f), wrinkle, t);
            b.Line(P(0.4f, 0.52f), P(0.43f, 0.58f), wrinkle, t);
            b.Line(P(0.6f, 0.52f), P(0.57f, 0.58f), wrinkle, t);
        }
        BeardOf(face.Beard);
        b.Line(P(0.455f, 0.605f), P(0.545f, 0.605f), face.IsFemale ? FemaleLips : Lips, Math.Max(1, rect.W / 45f));

        if (!face.Bareheaded) Headgear();
        b.Outline(rect.X, rect.Y, rect.W, rect.H, Theme.PanelBorder);

        void Insignia(int rank, Rgba coat)
        {
            bool flag = rank >= 1;
            foreach (float side in new[] { -1f, 1f })
            {
                float centre = 0.5f + side * 0.29f;
                Box(P(centre - 0.1f, 0.84f), R(0.2f, 0.06f), flag ? DarkGold : Darken(coat, 0.3f));
                if (flag)
                    for (int i = 0; i < rank; i++) Star(P(centre - 0.075f + i * 0.0375f, 0.87f), R(0.016f, 0.022f));
                else
                    for (int i = 0; i < 3; i++) Box(P(centre - 0.06f + i * 0.045f, 0.848f), R(0.025f, 0.044f), Gold);
            }
        }

        void Star(Vector2 c, Vector2 r)
        {
            // A four-pointed star: two crossed diamonds.
            Polygon(Gold, c + new Vector2(0, -r.Y), c + new Vector2(r.X * 0.35f, 0), c + new Vector2(0, r.Y), c + new Vector2(-r.X * 0.35f, 0));
            Polygon(Gold, c + new Vector2(-r.X, 0), c + new Vector2(0, -r.Y * 0.35f), c + new Vector2(r.X, 0), c + new Vector2(0, r.Y * 0.35f));
        }

        void HairOf(HairStyle style)
        {
            switch (style)
            {
                case HairStyle.Bald:
                    Ellipse(P(0.5f - faceW + 0.01f, 0.4f), R(0.03f, 0.06f), hair);
                    Ellipse(P(0.5f + faceW - 0.01f, 0.4f), R(0.03f, 0.06f), hair);
                    break;
                case HairStyle.Short:
                    Ellipse(P(0.5f, 0.3f), R(faceW + 0.012f, 0.09f), hair);
                    break;
                case HairStyle.SidePart:
                    Ellipse(P(0.5f, 0.3f), R(faceW + 0.015f, 0.095f), hair);
                    b.Triangle(P(0.5f - faceW, 0.33f), P(0.52f, 0.29f), P(0.5f - faceW + 0.02f, 0.39f), hair);
                    break;
                case HairStyle.Swept:
                    Ellipse(P(0.5f, 0.285f), R(faceW + 0.02f, 0.11f), hair);
                    Ellipse(P(0.55f, 0.25f), R(faceW * 0.7f, 0.07f), Lighten(hair, 0.06f));
                    break;
                case HairStyle.Long:
                    Ellipse(P(0.5f, 0.3f), R(faceW + 0.02f, 0.1f), hair);
                    Box(P(0.5f - faceW - 0.02f, 0.3f), R(0.05f, 0.3f), hair);
                    Box(P(0.5f + faceW - 0.03f, 0.3f), R(0.05f, 0.3f), hair);
                    break;
            }
        }

        void BeardOf(FacialHair beard)
        {
            switch (beard)
            {
                case FacialHair.Moustache:
                    Polygon(hair, P(0.44f, 0.575f), P(0.56f, 0.575f), P(0.545f, 0.595f), P(0.455f, 0.595f));
                    break;
                case FacialHair.Handlebar:
                    Polygon(hair, P(0.45f, 0.572f), P(0.55f, 0.572f), P(0.55f, 0.592f), P(0.45f, 0.592f));
                    b.Triangle(P(0.45f, 0.575f), P(0.45f, 0.592f), P(0.39f, 0.56f), hair);
                    b.Triangle(P(0.55f, 0.575f), P(0.55f, 0.592f), P(0.61f, 0.56f), hair);
                    break;
                case FacialHair.Goatee:
                    Polygon(hair, P(0.45f, 0.575f), P(0.55f, 0.575f), P(0.545f, 0.59f), P(0.455f, 0.59f));
                    Polygon(hair, P(0.465f, 0.625f), P(0.535f, 0.625f), P(0.52f, 0.68f), P(0.48f, 0.68f));
                    break;
                case FacialHair.FullBeard:
                    Polygon(hair, P(0.5f - faceW, 0.5f), P(0.5f + faceW, 0.5f), P(0.5f + jawW + 0.02f, 0.62f), P(0.5f, 0.71f), P(0.5f - jawW - 0.02f, 0.62f));
                    Ellipse(P(0.5f, 0.535f), R(faceW * 0.62f, 0.05f), skin);
                    Polygon(hair, P(0.44f, 0.572f), P(0.56f, 0.572f), P(0.545f, 0.592f), P(0.455f, 0.592f));
                    break;
                case FacialHair.Sideburns:
                    Box(P(0.5f - faceW, 0.38f), R(0.035f, 0.15f), hair);
                    Box(P(0.5f + faceW - 0.035f, 0.38f), R(0.035f, 0.15f), hair);
                    break;
            }
        }

        void Headgear()
        {
            var era = face.Era;
            if (!face.Naval && era <= Era.Classical)
            {
                // Crested bronze helmet with cheek guards
                Box(P(0.5f, 0.1f), R(0.012f, 0.1f), Darken(Bronze, 0.3f));
                Ellipse(P(0.5f, 0.12f), R(0.2f, 0.065f), Crest);
                Ellipse(P(0.5f, 0.3f), R(faceW + 0.025f, 0.12f), Bronze);
                Ellipse(P(0.45f, 0.25f), R(faceW * 0.45f, 0.05f), Lighten(Bronze, 0.2f));
                Box(P(0.5f - faceW - 0.03f, 0.36f), R(2 * faceW + 0.06f, 0.025f), Darken(Bronze, 0.25f));
                Polygon(Bronze, P(0.5f - faceW - 0.03f, 0.37f), P(0.5f - faceW + 0.06f, 0.37f), P(0.5f - faceW + 0.05f, 0.53f), P(0.5f - faceW - 0.01f, 0.5f));
                Polygon(Bronze, P(0.5f + faceW - 0.06f, 0.37f), P(0.5f + faceW + 0.03f, 0.37f), P(0.5f + faceW + 0.01f, 0.5f), P(0.5f + faceW - 0.05f, 0.53f));
                return;
            }
            if (!face.Naval && era == Era.Medieval)
            {
                // Nasal helm
                Ellipse(P(0.5f, 0.3f), R(faceW + 0.025f, 0.12f), Steel);
                Box(P(0.5f - faceW - 0.025f, 0.36f), R(2 * faceW + 0.05f, 0.03f), Darken(Steel, 0.2f));
                Box(P(0.49f, 0.37f), R(0.02f, 0.11f), Steel);
                return;
            }
            if (face.Naval && era < Era.Renaissance) return; // sailors of old went bareheaded
            if (era <= Era.Renaissance || (face.Naval && era == Era.Industrial))
            {
                if (face.Naval)
                {
                    // Bicorne
                    Ellipse(P(0.5f, 0.25f), R(faceW + 0.13f, 0.075f), HatBlack);
                    b.Line(P(0.5f - faceW - 0.1f, 0.27f), P(0.5f + faceW + 0.1f, 0.27f), Gold, 1.5f);
                }
                else
                {
                    // Tricorne
                    b.Triangle(P(0.5f - faceW - 0.09f, 0.31f), P(0.5f + faceW + 0.09f, 0.31f), P(0.5f, 0.14f), HatBlack);
                    b.Line(P(0.5f - faceW - 0.09f, 0.31f), P(0.5f, 0.14f), Gold, 1.5f);
                    b.Line(P(0.5f, 0.14f), P(0.5f + faceW + 0.09f, 0.31f), Gold, 1.5f);
                }
                return;
            }
            if (!face.Naval && era == Era.Industrial)
            {
                // Kepi
                Polygon(uniform, P(0.5f - faceW * 0.9f, 0.32f), P(0.5f + faceW * 0.9f, 0.32f), P(0.5f + faceW * 0.75f, 0.17f), P(0.5f - faceW * 0.75f, 0.17f));
                Box(P(0.5f - faceW * 0.9f, 0.28f), R(faceW * 1.8f, 0.03f), Darken(uniform, 0.35f));
                Ellipse(P(0.5f, 0.325f), R(faceW * 0.85f, 0.025f), HatBlack);
                Ellipse(P(0.5f, 0.24f), R(0.018f, 0.022f), Gold);
                return;
            }
            // Peaked cap
            Ellipse(P(0.5f, 0.235f), R(faceW + 0.06f, 0.07f), face.Naval ? White : uniform);
            Box(P(0.5f - faceW - 0.005f, 0.255f), R(2 * faceW + 0.01f, 0.055f), face.Naval ? HatBlack : Darken(uniform, 0.3f));
            Ellipse(P(0.5f, 0.315f), R(faceW * 0.9f, 0.028f), HatBlack);
            Ellipse(P(0.5f, 0.275f), R(0.025f, 0.02f), Gold);
        }
    }

    private static Rgba UniformColor(Portrait face, Rgba country) => face.Naval
        ? face.Era <= Era.Classical ? White : NavyBlue
        : face.Era switch
        {
            Era.Ancient or Era.Classical => Batch2D.Mix(Hex(0x8e2b22), country, 0.3f),
            Era.Medieval => Batch2D.Mix(Hex(0x6b6b6b), country, 0.7f),
            Era.Renaissance => Batch2D.Mix(Hex(0x2a3550), country, 0.75f),
            Era.Industrial => Batch2D.Mix(Hex(0x2c3a5a), country, 0.35f),
            _ => Batch2D.Mix(Hex(0x5a5e3a), country, 0.15f),
        };

    private static Rgba Darken(Rgba c, float amount) => Batch2D.Mix(c, Rgba.Black, amount);

    private static Rgba Lighten(Rgba c, float amount) => Batch2D.Mix(c, Rgba.White, amount);
}
