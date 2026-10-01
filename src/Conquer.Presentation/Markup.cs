namespace Conquer.Presentation;

public enum MarkupKind
{
    Heading,
    Subheading,
    Bullet,
    Paragraph,
}

/// <summary>One line of help or changelog text, without its markup.</summary>
public readonly record struct MarkupLine(MarkupKind Kind, string Text);

/// <summary>The little Markdown the help and the changelog use: "## " heading, "### " subheading, "- " bullet; anything else is a paragraph.</summary>
public static class Markup
{
    public static MarkupLine Parse(string line) =>
        line.StartsWith("## ") ? new(MarkupKind.Heading, line[3..])
        : line.StartsWith("### ") ? new(MarkupKind.Subheading, line[4..])
        : line.StartsWith("- ") ? new(MarkupKind.Bullet, line[2..])
        : new(MarkupKind.Paragraph, line);
}
