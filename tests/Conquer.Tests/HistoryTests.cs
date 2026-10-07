using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>The ledger: each nation's figures written down every month, and the statistics tab that draws them.</summary>
[Collection("World")]
public class HistoryTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private static void RunDays(GameSession s, int days)
    {
        for (int i = 0; i < days * 24; i++) s.Step();
    }

    private GameController Game()
    {
        var game = new GameController(GameSession.Create(_map, 3, seed: 7, computerRivals: false));
        var settlers = game.SelectedUnit!;
        game.OpenCityNaming(settlers.Id, settlers.ProvinceId);
        game.ConfirmCityName();
        game.Nation.Visible = true;
        game.Session.MeetEveryone();
        game.Nation.Tab = NationTab.Statistics;
        return game;
    }

    [Fact]
    public void EveryNationIsWrittenDownEachMonth()
    {
        var s = GameSession.Create(_map, 3, seed: 7, computerRivals: false);
        RunDays(s, GameRules.HistoryDays * 3);
        Assert.Equal(3, s.HistoryOf(0).Count());
        Assert.All(s.Players, p => Assert.Equal(3, s.HistoryOf(p.Id).Count()));
        var last = s.HistoryOf(0).Last();
        Assert.Equal(GameRules.HistoryDays * 3 * 24L, last.Hours);
        Assert.Equal(s.Human.Provinces.Count, last.Provinces);
    }

    [Fact]
    public void TheLedgerIsSaved()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        RunDays(s, GameRules.HistoryDays * 2);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(s.History, loaded.History);
    }

    [Fact]
    public void TheTabDrawsALinePerNationOnceThereAreFigures()
    {
        var game = Game();
        var page = Assert.IsType<StatisticsPage>(game.Nation.Page());
        Assert.NotNull(page.Empty);
        RunDays(game.Session, GameRules.HistoryDays * 2);
        page = Assert.IsType<StatisticsPage>(game.Nation.Page());
        Assert.Null(page.Empty);
        Assert.Equal(3, page.Series.Count);
        Assert.True(page.Series[0].Player);
        Assert.All(page.Series, s => Assert.All(s.Points, p => Assert.InRange(p.X, 0, 1)));
        Assert.All(page.Series, s => Assert.All(s.Points, p => Assert.InRange(p.Y, 0, 1)));
        Assert.Equal(0, page.Series[0].Points[0].X);
        Assert.Equal(1, page.Series[0].Points[^1].X);

        page.Metrics[(int)StatisticsMetric.Provinces].OnClick!();
        Assert.Equal(StatisticsMetric.Provinces, game.Nation.Metric);
        page = Assert.IsType<StatisticsPage>(game.Nation.Page());
        Assert.StartsWith("Provincias", page.Title);
    }
}
