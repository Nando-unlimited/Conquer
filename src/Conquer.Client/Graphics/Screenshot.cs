using System.IO.Compression;
using Silk.NET.OpenGL;

namespace Conquer.Client.Graphics;

/// <summary>Saves the current framebuffer as a PNG (used by the --screenshot test option).</summary>
public static class Screenshot
{
    public static void Save(GL gl, int width, int height, string path)
    {
        var pixels = new byte[width * height * 4];
        gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
        gl.ReadPixels(0, 0, (uint)width, (uint)height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels.AsSpan());

        // PNG rows go top to bottom, GL rows bottom to top; each row starts with filter byte 0.
        var raw = new byte[height * (width * 3 + 1)];
        for (int y = 0; y < height; y++)
        {
            int src = (height - 1 - y) * width * 4, dst = y * (width * 3 + 1);
            raw[dst++] = 0;
            for (int x = 0; x < width; x++, src += 4)
            {
                raw[dst++] = pixels[src];
                raw[dst++] = pixels[src + 1];
                raw[dst++] = pixels[src + 2];
            }
        }

        using var file = File.Create(path);
        file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8; // bit depth
        header[9] = 2; // colour type: RGB
        WriteChunk(file, "IHDR", header);
        using (var compressed = new MemoryStream())
        {
            using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true)) z.Write(raw);
            WriteChunk(file, "IDAT", compressed.ToArray());
        }
        WriteChunk(file, "IEND", []);
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var buffer = new byte[4];
        WriteBigEndian(buffer, 0, data.Length);
        s.Write(buffer);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        uint crc = Crc(Crc(0xFFFFFFFF, typeBytes), data) ^ 0xFFFFFFFF;
        WriteBigEndian(buffer, 0, (int)crc);
        s.Write(buffer);
    }

    private static void WriteBigEndian(byte[] b, int offset, int value)
    {
        b[offset] = (byte)(value >> 24);
        b[offset + 1] = (byte)(value >> 16);
        b[offset + 2] = (byte)(value >> 8);
        b[offset + 3] = (byte)value;
    }

    private static uint Crc(uint crc, byte[] data)
    {
        foreach (byte b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
        }
        return crc;
    }
}
