using Conquer.Game.Simulation;
using Conquer.Presentation;

namespace Conquer.Tests;

public class InterfaceTests
{
    [Fact]
    public void NamesSortInSpanishAlphabeticalOrder()
    {
        string[] names = ["Zaragoza", "ñandú", "Ávila", "Nápoles", "oca", "Écija", "avena", "Nzé"];
        var sorted = names.OrderBy(TextFormat.SpanishSortKey, StringComparer.Ordinal).ToArray();
        Assert.Equal(["avena", "Ávila", "Écija", "Nápoles", "Nzé", "ñandú", "oca", "Zaragoza"], sorted);
    }

    [Fact]
    public void TheHelpUsesOnlyCharactersTheFontCanDraw()
    {
        // The font covers Latin-1: no typographic ellipsis, minus sign or curly quotes.
        Assert.All(HelpTopics.AllText, line => Assert.DoesNotContain(line, c => c > 255));
        Assert.NotEmpty(HelpTopics.AllText);
    }

    [Fact]
    public void GameAndPresentationDoNotDependOnAnyEngine()
    {
        // Everything but drawing lives in these two, so the client's engine can be swapped without touching them.
        foreach (var assembly in new[] { typeof(GameSession).Assembly, typeof(GameClock).Assembly })
        {
            var references = assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
            Assert.DoesNotContain(references, name => name.StartsWith("Silk.NET") || name.StartsWith("Stb") || name.StartsWith("Godot") || name.StartsWith("Conquer.Client") || name == "Conquer");
        }
    }

    [Fact]
    public void TheClockResumesAtTheSpeedItWasPausedAt()
    {
        var clock = new GameClock();
        clock.SetSpeed(3);
        clock.TogglePause();
        Assert.Equal(0, clock.Speed);
        Assert.Equal(0, clock.Advance(10));
        clock.TogglePause();
        Assert.Equal(3, clock.Speed);
        Assert.Equal(12, clock.Advance(1));
    }

    [Fact]
    public void MessagesShowNewestFirstAndExpire()
    {
        var log = new MessageLog();
        log.Add("first", 0);
        log.Add("", 1);
        log.Add("second", 2, ok: false);
        Assert.Equal(["second", "first"], log.Current(3).Select(m => m.Text));
        Assert.Equal(["second"], log.Current(MessageLog.Lifetime + 1).Select(m => m.Text));
        Assert.Equal(1, log.Current(3)[0].Opacity(3));
        Assert.Empty(log.Current(100));
    }

    [Fact]
    public void TheChangelogStartsWithTheLatestVersionWithoutMarkdown()
    {
        var first = Changelog.Lines.First(l => l.Kind == MarkupKind.Heading);
        Assert.StartsWith("1.", first.Text);
        Assert.DoesNotContain("[", first.Text);
        Assert.All(Changelog.Lines.Where(l => l.Kind == MarkupKind.Bullet), l => Assert.DoesNotContain("**", l.Text));
    }
}
