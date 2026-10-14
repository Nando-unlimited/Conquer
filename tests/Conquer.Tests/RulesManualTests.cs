using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>The rules manual: drawable in the game, and the same in docs/REGLAS.md.</summary>
public class RulesManualTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conquer.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void TheRulesUseOnlyCharactersTheFontCanDraw()
    {
        // The font covers Latin-1: no minus sign (−), typographic ellipsis or curly quotes.
        Assert.All(RulesManual.AllText, line => Assert.DoesNotContain(line, c => c > 255));
        Assert.NotEmpty(RulesManual.AllText);
    }

    [Fact]
    public void TheRulesWriteNumbersTheSpanishWay()
    {
        // Whatever the culture of the thread: decimal commas and spaced percentages.
        Assert.Contains(RulesManual.AllText, line => line.Contains("0,13 de comida"));
        Assert.Contains(RulesManual.AllText, line => line.Contains("10 %"));
    }

    /// <summary>
    /// docs/REGLAS.md is the manual as Markdown. When the rules change, regenerate it by running the tests with
    /// CONQUER_WRITE_DOCS=1.
    /// </summary>
    [Fact]
    public void TheRulesDocumentIsUpToDate()
    {
        string path = Path.Combine(RepoRoot(), "docs", "REGLAS.md");
        string expected = RulesManual.ToMarkdown();
        if (Environment.GetEnvironmentVariable("CONQUER_WRITE_DOCS") == "1") File.WriteAllText(path, expected);
        Assert.True(File.Exists(path), "Falta docs/REGLAS.md: ejecuta los tests con CONQUER_WRITE_DOCS=1.");
        Assert.True(File.ReadAllText(path).ReplaceLineEndings("\n") == expected,
            "docs/REGLAS.md no coincide con RulesManual: ejecuta los tests con CONQUER_WRITE_DOCS=1 para regenerarlo.");
    }
}
