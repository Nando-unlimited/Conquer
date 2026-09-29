using System.Numerics;
using Conquer.Game.World;
using Silk.NET.OpenGL;

namespace Conquer.Client.Graphics;

/// <summary>
/// The menus' backdrop: the real Earth at half resolution, coloured by height (sea blues by depth, green
/// lowlands, brown hills, pale peaks and ice) with relief, drifting slowly westwards behind a dark veil.
/// It is built once in the background and shared by every menu screen; until it is ready they show a plain colour.
/// </summary>
public static class MenuBackground
{
    private const int Width = EarthData.Width / 2, Height = EarthData.Height / 2;
    /// <summary>Map widths per second it drifts.</summary>
    private const float Drift = 1f / 400;

    private static Task<byte[]>? _pixels;
    private static Texture? _texture;

    /// <summary>Whether the backdrop is on screen yet (screenshots of the menus wait for it).</summary>
    public static bool IsReady => _texture != null;
    private static double _time;

    public static void Draw(GL gl, Batch2D batch, Vector2 screen, double dt)
    {
        _pixels ??= Task.Run(Build);
        _time += dt;
        batch.Rect(0, 0, screen.X, screen.Y, new Rgba(0xFF0A0F16));
        if (_texture == null)
        {
            if (!_pixels.IsCompletedSuccessfully) return;
            _texture = new Texture(gl, Width, Height, _pixels.Result, smooth: true, repeatX: true);
        }

        // The band between the polar circles, as tall as the screen; as wide as that makes it.
        const float Top = 0.12f, Bottom = 0.88f;
        float across = screen.X / screen.Y * (Bottom - Top) * Height / Width;
        float start = (float)(_time * Drift % 1);
        batch.Quad(_texture, Vector2.Zero, screen, new Vector2(start, Top), new Vector2(start + across, Bottom), Rgba.White);
        // A veil, darker at the top and bottom, so the title and buttons stand out.
        batch.Gradient(0, 0, screen.X, screen.Y * 0.5f, new Rgba(0xD0070A10), new Rgba(0x80070A10));
        batch.Gradient(0, screen.Y * 0.5f, screen.X, screen.Y * 0.5f, new Rgba(0x80070A10), new Rgba(0xD0070A10));
    }

    private static byte[] Build()
    {
        var earth = EarthData.LoadEmbedded();
        var data = new byte[Width * Height * 4];
        short At(int x, int y) => earth.Elevation[Math.Clamp(y * 2, 0, EarthData.Height - 1) * EarthData.Width + ((x * 2) % EarthData.Width + EarthData.Width) % EarthData.Width];

        Parallel.For(0, Height, y =>
        {
            for (int x = 0; x < Width; x++)
            {
                int source = y * 2 * EarthData.Width + x * 2;
                byte flags = earth.Flags[source];
                short e = At(x, y);
                Vector3 c;
                if ((flags & EarthData.FlagGlacier) != 0) c = new(230, 236, 242);
                else if ((flags & EarthData.FlagLand) == 0 || (flags & EarthData.FlagLake) != 0)
                    c = Vector3.Lerp(new(48, 110, 160), new(12, 36, 70), Math.Clamp(-e / 5000f, 0, 1));
                else
                {
                    c = e < 300 ? Vector3.Lerp(new(88, 138, 72), new(140, 150, 86), e / 300f)
                        : e < 1500 ? Vector3.Lerp(new(140, 150, 86), new(140, 110, 76), (e - 300) / 1200f)
                        : Vector3.Lerp(new(140, 110, 76), new(215, 208, 198), Math.Clamp((e - 1500) / 2500f, 0, 1));
                    // Relief, lit from the north-west.
                    float slope = ((e - At(x - 1, y)) + (e - At(x, y - 1))) / 600f;
                    c *= Math.Clamp(1f + slope * 0.6f, 0.6f, 1.4f);
                }
                int i = (y * Width + x) * 4;
                data[i] = (byte)Math.Clamp(c.X, 0, 255);
                data[i + 1] = (byte)Math.Clamp(c.Y, 0, 255);
                data[i + 2] = (byte)Math.Clamp(c.Z, 0, 255);
                data[i + 3] = 255;
            }
        });
        return data;
    }
}
