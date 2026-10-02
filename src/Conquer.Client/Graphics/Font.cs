using System.Numerics;
using Silk.NET.OpenGL;
using StbTrueTypeSharp;
using static StbTrueTypeSharp.StbTrueType;

namespace Conquer.Client.Graphics;

public enum FontSize
{
    Small,
    Normal,
    Large,
    Title,
}

/// <summary>
/// The game's lettering baked at a few pixel sizes into one texture atlas: Lilita One, round and sturdy, for the
/// interface (one weight, so bold is the same); Cinzel Bold, Roman capitals, for window titles and headings (the large
/// size in bold) and the title size (the game's name, nations on the map). Covers Latin-1 (U+0020..U+00FF), enough for
/// Spanish text.
/// </summary>
public sealed unsafe class Font : IDisposable
{
    private const int AtlasSize = 2048;
    private const int FirstChar = 32;
    private const int CharCount = 256 - FirstChar;
    private static readonly (FontSize Size, float Pixels)[] Sizes =
        [(FontSize.Small, 14), (FontSize.Normal, 17), (FontSize.Large, 22), (FontSize.Title, 56)];

    private readonly Dictionary<(FontSize, bool), (stbtt_packedchar[] Chars, float Ascent, float LineHeight)> _faces = [];
    public Texture Atlas { get; }

    public Font(GL gl)
    {
        var face = LoadAsset("LilitaOne-Regular.ttf");
        var title = LoadAsset("Cinzel-Bold.ttf");
        var pixels = new byte[AtlasSize * AtlasSize];

        fixed (byte* atlas = pixels)
        {
            var pack = new stbtt_pack_context();
            stbtt_PackBegin(pack, atlas, AtlasSize, AtlasSize, AtlasSize, 1, null);
            // Lilita One is the interface, regular and bold alike; Cinzel is the bold of the large size (window titles and
            // headings) and the title size, which Face looks for under (Title, bold).
            foreach (var (data, isBold, isTitle) in new[] { (face, false, false), (face, true, false), (title, true, true) })
            {
                fixed (byte* font = data)
                {
                    var info = new stbtt_fontinfo();
                    stbtt_InitFont(info, font, 0);
                    int ascent, descent, lineGap;
                    stbtt_GetFontVMetrics(info, &ascent, &descent, &lineGap);
                    foreach (var (size, px) in Sizes)
                    {
                        bool cinzel = size == FontSize.Title || (size == FontSize.Large && isBold);
                        if (cinzel != isTitle) continue;
                        uint oversample = size == FontSize.Title ? 1u : 2u;
                        stbtt_PackSetOversampling(pack, oversample, oversample);
                        var chars = new stbtt_packedchar[CharCount];
                        fixed (stbtt_packedchar* c = chars)
                            stbtt_PackFontRange(pack, font, 0, px, FirstChar, CharCount, c);
                        float scale = stbtt_ScaleForPixelHeight(info, px);
                        _faces[(size, isBold)] = (chars, ascent * scale, (ascent - descent + lineGap) * scale);
                    }
                }
            }
            stbtt_PackEnd(pack);
        }

        var rgba = new byte[AtlasSize * AtlasSize * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            rgba[i * 4] = rgba[i * 4 + 1] = rgba[i * 4 + 2] = 255;
            rgba[i * 4 + 3] = pixels[i];
        }
        Atlas = new Texture(gl, AtlasSize, AtlasSize, rgba, smooth: true);
    }

    private static byte[] LoadAsset(string name)
    {
        using var stream = typeof(Font).Assembly.GetManifestResourceStream("Conquer.Client.Assets." + name)
            ?? throw new InvalidOperationException($"Missing font {name}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public float LineHeight(FontSize size, bool bold = false) => Face(size, bold).LineHeight;

    // The title size is only baked in bold.
    private (stbtt_packedchar[] Chars, float Ascent, float LineHeight) Face(FontSize size, bool bold) =>
        _faces[(size, bold || size == FontSize.Title)];

    public float Measure(string text, FontSize size, bool bold = false)
    {
        var chars = Face(size, bold).Chars;
        float w = 0;
        foreach (char ch in text) w += chars[Index(ch)].xadvance;
        return w;
    }

    /// <summary>Draws text with its top-left corner at (x, y); returns the width drawn.</summary>
    public float Draw(Batch2D batch, string text, float x, float y, Rgba color, FontSize size = FontSize.Normal, bool bold = false)
    {
        var (chars, ascent, _) = Face(size, bold);
        float penX = MathF.Round(x), penY = MathF.Round(y + ascent);
        float startX = penX;
        fixed (stbtt_packedchar* c = chars)
        {
            foreach (char ch in text)
            {
                stbtt_aligned_quad q;
                stbtt_GetPackedQuad(c, AtlasSize, AtlasSize, Index(ch), &penX, &penY, &q, 0);
                if (ch != ' ')
                    batch.Quad(Atlas, new Vector2(q.x0, q.y0), new Vector2(q.x1, q.y1), new Vector2(q.s0, q.t0), new Vector2(q.s1, q.t1), color);
            }
        }
        return penX - startX;
    }

    /// <summary>Splits text into lines no wider than <paramref name="width"/>.</summary>
    public List<string> Wrap(string text, float width, FontSize size, bool bold = false)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' '))
        {
            string candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && Measure(candidate, size, bold) > width)
            {
                lines.Add(line);
                line = word;
            }
            else line = candidate;
        }
        lines.Add(line);
        return lines;
    }

    private static int Index(char ch) => ch is >= (char)FirstChar and <= (char)255 ? ch - FirstChar : '?' - FirstChar;

    public void Dispose() => Atlas.Dispose();
}
