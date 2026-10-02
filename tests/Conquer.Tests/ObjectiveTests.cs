using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>The guided first objectives: met in any order, paid once, saved, and shown on a card.</summary>
[Collection("World")]
public class ObjectiveTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    [Fact]
    public void FoundingTheCapitalMeetsTheFirstObjectiveAndPaysForIt()
    {
        var game = new GameController(GameSession.Create(_map, 2, seed: 7, computerRivals: false));
        var s = game.Session;
        Assert.Equal(Objective.Capital, s.CurrentObjective);
        Assert.Equal("Funda tu capital", game.ObjectiveCard()!.Title);
        game.ObjectiveCard()!.Go!.OnClick!();
        Assert.Equal(UnitType.Settlers, game.SelectedUnit!.Type);

        s.FoundCity(0, game.SelectedUnit.Id);
        double gold = s.Human.Stockpile[ResourceType.Gold];
        s.Step();
        Assert.True(s.ObjectiveDone(Objective.Capital));
        Assert.Equal(gold + GameRules.ObjectiveGold, s.Human.Stockpile[ResourceType.Gold], 3);
        Assert.Equal(Objective.Research, s.CurrentObjective);
        Assert.Contains(s.Notifications, n => n.Text.StartsWith("Objetivo cumplido: funda tu capital"));
        Assert.Equal("Objetivo 2 de 9", game.ObjectiveCard()!.Header);

        // Met once, it stays met and is not paid again.
        s.Step();
        Assert.Equal(gold + GameRules.ObjectiveGold, s.Human.Stockpile[ResourceType.Gold], 3);
    }

    [Fact]
    public void ObjectivesCanBeMetInAnyOrder()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        var settlers = s.Units.First(u => u.OwnerId == 0 && u.Type == UnitType.Settlers);
        s.AddRegiment(0, settlers.ProvinceId, BattalionType.Scouts);
        s.Step();
        Assert.True(s.ObjectiveDone(Objective.Regiment));
        Assert.Equal(Objective.Capital, s.CurrentObjective);
    }

    [Fact]
    public void ObjectivesAreSavedAndOldSavesCountWhatIsAlreadyMet()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.FoundCity(0, s.Units.First(u => u.OwnerId == 0 && u.Type == UnitType.Settlers).Id);
        s.Step();
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.True(loaded.ObjectiveDone(Objective.Capital));

        var old = GameSession.Load(_map, s.ToSave("test") with { ObjectivesDone = null });
        Assert.True(old.ObjectiveDone(Objective.Capital));
        Assert.False(old.ObjectiveDone(Objective.SecondCity));
    }

    [Fact]
    public void TheCardFolds()
    {
        var game = new GameController(GameSession.Create(_map, 2, seed: 7, computerRivals: false));
        var card = game.ObjectiveCard()!;
        Assert.False(card.Folded);
        card.Toggle.OnClick!();
        Assert.True(game.ObjectiveCard()!.Folded);
    }
}
