using System.IO.Compression;

namespace Conquer.Game.World;

/// <summary>
/// Real-Earth elevation and surface flags on a 0.1° equirectangular grid, built offline by
/// tools/Conquer.EarthData and embedded in this assembly as Assets/earth.gz.
/// </summary>
public sealed class EarthData
{
    public const int Width = 3600;
    public const int Height = 1800;
    public const byte FlagLand = 1;
    public const byte FlagLake = 2;
    public const byte FlagGlacier = 4;
    private const string Magic = "CQEARTH1";

    /// <summary>Metres above (positive) or below (negative) sea level.</summary>
    public short[] Elevation { get; }
    public byte[] Flags { get; }

    private EarthData(short[] elevation, byte[] flags)
    {
        Elevation = elevation;
        Flags = flags;
    }

    public static EarthData LoadEmbedded()
    {
        using var stream = typeof(EarthData).Assembly.GetManifestResourceStream("Conquer.Game.Assets.earth.gz")
            ?? throw new InvalidOperationException("Embedded Earth map not found.");
        return Read(stream);
    }

    public static EarthData Read(Stream stream)
    {
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new BinaryReader(gzip);
        if (new string(reader.ReadChars(Magic.Length)) != Magic)
            throw new InvalidDataException("Not an Earth map file.");
        int w = reader.ReadInt32(), h = reader.ReadInt32();
        if (w != Width || h != Height)
            throw new InvalidDataException($"Unexpected Earth map size {w}x{h}.");
        var elevation = new short[w * h];
        reader.BaseStream.ReadExactly(System.Runtime.InteropServices.MemoryMarshal.AsBytes(elevation.AsSpan()));
        var flags = reader.ReadBytes(w * h);
        return new EarthData(elevation, flags);
    }

    public static void Write(string path, short[] elevation, byte[] flags)
    {
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
        using var writer = new BinaryWriter(gzip);
        writer.Write(Magic.ToCharArray());
        writer.Write(Width);
        writer.Write(Height);
        writer.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(elevation.AsSpan()));
        writer.Write(flags);
    }
}
