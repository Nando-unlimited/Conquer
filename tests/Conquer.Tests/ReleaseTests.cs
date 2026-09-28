using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Conquer.Tests;

/// <summary>Every change bumps the csproj version and adds a matching CHANGELOG.md entry.</summary>
public class ReleaseTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Conquer.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact]
    public void ChangelogTopEntryMatchesProjectVersion()
    {
        string root = RepoRoot();
        var csproj = XDocument.Load(Path.Combine(root, "src", "Conquer.Client", "Conquer.Client.csproj"));
        string version = csproj.Descendants("Version").Single().Value;

        string changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));
        var top = Regex.Match(changelog, @"^## \[(?<v>[^\]]+)\]", RegexOptions.Multiline);

        Assert.True(top.Success, "CHANGELOG.md has no '## [x.y.z]' entry.");
        Assert.Equal(version, top.Groups["v"].Value);
    }
}
