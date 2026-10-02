using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>Victory by domination, science or score; defeat when another wins or the human's nation falls.</summary>
[Collection("World")]
public class VictoryTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private static void RunDays(GameSession s, int days)
    {
        for (int i = 0; i < days * 24; i++) s.Step();
    }

    /// <summary>A game of three with every nation's capital founded.</summary>
    private GameSession ThreeNations()
    {
        var s = GameSession.Create(_map, 3, seed: 7, computerRivals: false);
        foreach (var settlers in s.Units.Where(u => u.Type == UnitType.Settlers).ToList()) s.FoundCity(settlers.OwnerId, settlers.Id);
        return s;
    }

    [Fact]
    public void KnowingEveryAdvanceWinsByScience()
    {
        var s = ThreeNations();
        foreach (var tech in Techs.All.Skip(1)) s.Human.Learn(tech);
        RunDays(s, 1);
        Assert.Null(s.Outcome);
        s.Human.Learn(Techs.All[0]);
        RunDays(s, 1);
        Assert.Equal(new GameOutcome(0, VictoryKind.Science, s.Outcome!.Hours), s.Outcome);
        Assert.False(s.HumanDefeated);
        Assert.Contains(s.Notifications, n => n.Text.StartsWith("¡Victoria!"));
    }

    [Fact]
    public void EveryRivalGoneOrVassalWinsByDomination()
    {
        var s = ThreeNations();
        Assert.Equal(2, s.RivalsLeft(0));
        s.Eliminate(1);
        s.MakeVassal(2, 0);
        Assert.Equal(0, s.RivalsLeft(0));
        RunDays(s, 1);
        Assert.Equal(VictoryKind.Domination, s.Outcome!.Kind);
        Assert.Equal(0, s.Outcome.WinnerId);
    }

    [Fact]
    public void AnotherNationWinningIsADefeat()
    {
        var s = ThreeNations();
        foreach (var tech in Techs.All) s.Players[1].Learn(tech);
        RunDays(s, 1);
        Assert.Equal(1, s.Outcome!.WinnerId);
        Assert.True(s.HumanDefeated);
        // The game goes on, and nobody else can win it again.
        foreach (var tech in Techs.All) s.Human.Learn(tech);
        RunDays(s, 1);
        Assert.Equal(1, s.Outcome.WinnerId);
    }

    [Fact]
    public void TheHighestScoreWinsWhenTheYearComes()
    {
        var s = ThreeNations();
        var capital = s.Map.Provinces[s.CityById(s.Human.CapitalCityId!.Value)!.ProvinceId];
        capital.Population = 100000;
        Assert.Equal(s.Human, s.Ranking[0]);
        Assert.True(s.Score(s.Human) > s.Score(s.Players[1]));
        RunDays(s, 1);
        Assert.Null(s.Outcome);
        var save = s.ToSave("test");
        var late = GameSession.Load(_map, save with { Hours = (GameRules.ScoreVictoryYear - 1) * 365L * 24 - 1 });
        late.Map.Provinces[capital.Id].Population = 100000;
        late.Step();
        Assert.Equal(new GameOutcome(0, VictoryKind.Score, late.Date.Hours), late.Outcome);
    }

    [Fact]
    public void TheOutcomeIsSaved()
    {
        var s = ThreeNations();
        s.Win(2, VictoryKind.Domination);
        Assert.Equal(s.Outcome, GameSession.Load(_map, s.ToSave("test")).Outcome);
    }

    [Fact]
    public void TheWindowOpensOnceAndStopsTime()
    {
        var game = new GameController(ThreeNations(), loaded: true);
        Assert.False(game.GameOverOpen);
        game.Session.Win(1, VictoryKind.Science);
        Assert.True(game.GameOverOpen);
        Assert.True(game.TimeStopped);
        var window = game.GameOver(new NoNavigator())!;
        Assert.Equal("Derrota", window.Title);
        Assert.Equal(3, window.Ranking.Count);
        window.Buttons[0].OnClick!();
        Assert.False(game.GameOverOpen);
        Assert.Null(game.GameOver(new NoNavigator()));
    }

    [Fact]
    public void TheHumanFallingIsADefeat()
    {
        var game = new GameController(ThreeNations(), loaded: true);
        game.Session.Eliminate(0);
        Assert.True(game.Session.HumanDefeated);
        Assert.True(game.GameOverOpen);
        Assert.Equal("Seguir mirando", game.GameOver(new NoNavigator())!.Buttons[0].Text);
    }
}
