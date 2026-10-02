using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>Events with decisions: two answers, each with its price; the first is taken if nobody answers in time.</summary>
[Collection("World")]
public class DecisionTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private static void RunHours(GameSession s, int hours)
    {
        for (int i = 0; i < hours; i++) s.Step();
    }

    private (GameSession S, Province Capital) Capital()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        var a = _map.Provinces.First(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        a.Population = 20000;
        return (s, a);
    }

    [Fact]
    public void SharingTheGrainCostsFoodAndCheersTheProvince()
    {
        var (s, a) = Capital();
        var d = s.StartDecision(0, DecisionKind.Drought, a.Id);
        Assert.Single(s.PendingDecisions(0));
        double mood = s.TargetMood(a);
        s.Human.Stockpile[ResourceType.Food] = 0;
        Assert.False(s.CanChoose(0, d.Id, 1).Ok);
        s.Human.Stockpile[ResourceType.Food] = 1000;
        Assert.True(s.Choose(0, d.Id, 1).Ok);
        Assert.Equal(1000 - 100 * d.Scale, s.Human.Stockpile[ResourceType.Food], 3);
        Assert.Equal(mood + 15, s.TargetMood(a), 3);
        Assert.Empty(s.PendingDecisions(0));
        Assert.False(s.Choose(0, d.Id, 1).Ok);
    }

    [Fact]
    public void KeepingTheGrainCostsPeopleAndMood()
    {
        var (s, a) = Capital();
        double mood = s.TargetMood(a);
        var d = s.StartDecision(0, DecisionKind.Drought, a.Id);
        Assert.True(s.Choose(0, d.Id, 0).Ok);
        Assert.Equal(20000 * 0.97, a.Population, 3);
        Assert.True(s.TargetMood(a) < mood);
        Assert.Contains(s.MoodFactors(a), f => f.Reason == "Sequía" && f.Points == -20);
    }

    [Fact]
    public void ItsMoodLastsAYear()
    {
        var (s, a) = Capital();
        var d = s.StartDecision(0, DecisionKind.GoldVein, a.Id);
        double gold = s.Human.Stockpile[ResourceType.Gold];
        s.Choose(0, d.Id, 1);
        Assert.Equal(gold + 50, s.Human.Stockpile[ResourceType.Gold], 3);
        Assert.Contains(s.MoodFactors(a), f => f.Reason == "Un filón de oro" && f.Points == 10);
        RunHours(s, 24 * (GameRules.DecisionMoodDays + 1));
        Assert.DoesNotContain(s.MoodFactors(a), f => f.Reason == "Un filón de oro");
    }

    [Fact]
    public void UnansweredDecisionsTakeTheirDefault()
    {
        var (s, a) = Capital();
        var d = s.StartDecision(0, DecisionKind.Refugees, a.Id);
        RunHours(s, 24 * GameRules.DecisionDays + 1);
        Assert.Null(s.DecisionById(d.Id));
        Assert.Contains(s.Notifications, n => n.Text.Contains("sin respuesta"));
    }

    [Fact]
    public void WelcomingRefugeesBringsPeople()
    {
        var (s, a) = Capital();
        var d = s.StartDecision(0, DecisionKind.Refugees, a.Id);
        s.Choose(0, d.Id, 1);
        Assert.Equal(20000 * 1.05, a.Population, 3);
    }

    [Fact]
    public void ItGrowsWithTheNation()
    {
        var (s, a) = Capital();
        Assert.Equal(1, s.DecisionScale(0));
        a.Population = GameRules.DecisionPeople * 4;
        Assert.Equal(2, s.DecisionScale(0), 3);
    }

    [Fact]
    public void ItIsSaved()
    {
        var (s, a) = Capital();
        var d = s.StartDecision(0, DecisionKind.Bandits, a.Id);
        var other = s.StartDecision(0, DecisionKind.Drought, a.Id);
        s.Choose(0, other.Id, 1);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(d, Assert.Single(loaded.PendingDecisions(0)));
        Assert.Contains(loaded.MoodFactors(a), f => f.Reason == "Sequía");
        Assert.True(loaded.StartDecision(0, DecisionKind.Harvest, a.Id).Id > other.Id);
    }

    [Fact]
    public void TheWindowOpensAndStopsTime()
    {
        var game = new GameController(GameSession.Create(_map, 2, seed: 7, computerRivals: false));
        var s = game.Session;
        var a = _map.Provinces.First(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        Assert.False(game.DecisionOpen);
        Assert.Null(game.DecisionWindow());
        s.StartDecision(0, DecisionKind.Harvest, a.Id);
        Assert.True(game.DecisionOpen);
        Assert.True(game.TimeStopped);
        var window = game.DecisionWindow()!;
        Assert.Equal("Una gran cosecha", window.Title);
        Assert.Equal(2, window.Options.Count);
        window.Options[1].OnClick!();
        Assert.False(game.DecisionOpen);
        Assert.True(s.CityIn(a)!.HasFestival(s.Date.Hours));
    }
}
