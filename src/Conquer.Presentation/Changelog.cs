namespace Conquer.Presentation;

/// <summary>CHANGELOG.md (embedded in this assembly), ready to show: one heading per version, its sections and bullets.</summary>
public static class Changelog
{
    public static IReadOnlyList<MarkupLine> Lines { get; } = Read();

    private static List<MarkupLine> Read()
    {
        using var stream = typeof(Changelog).Assembly.GetManifestResourceStream("CHANGELOG.md");
        using var reader = new StreamReader(stream ?? new MemoryStream());
        return reader.ReadToEnd().Replace("\r", "").Split('\n')
            .Where(raw => !raw.StartsWith("# ") && raw.Trim().Length > 0)
            .Select(Markup.Parse)
            .Select(line => line.Kind switch
            {
                MarkupKind.Heading => line with { Text = line.Text.Replace("[", "").Replace("]", "") },
                MarkupKind.Bullet => line with { Text = line.Text.Replace("**", "") },
                _ => line,
            })
            .ToList();
    }
}
