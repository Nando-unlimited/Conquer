using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>The game on screen without a screen: selection, orders, the clock and the city dialog.</summary>
[Collection("World")]
public class ControllerTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private GameController NewGame() => new(GameSession.Create(_map, 1, seed: 7));

    [Fact]
    public void ANewGameStartsWithTheSettlersSelectedAndInView()
    {
        var game = NewGame();
        var settlers = game.SelectedUnit!;
        Assert.Equal(game.Human.Id, settlers.OwnerId);
        Assert.Equal(game.Center(settlers.ProvinceId), game.Camera.Center);
    }

    [Fact]
    public void SelectingAProvinceDropsTheUnitAndEscapeClearsBoth()
    {
        var game = NewGame();
        int province = game.SelectedUnit!.ProvinceId;
        game.HoverProvince = province;
        game.ClickProvince();
        Assert.Null(game.SelectedUnitId);
        Assert.Equal(province, game.SelectedProvince);
        game.ClearSelection();
        Assert.False(game.HasSelection);
    }

    [Fact]
    public void TimeStandsStillWhileFrozenOrPaused()
    {
        var game = NewGame();
        var start = game.Session.Date.Hours;
        game.Tick(10, frozen: true);
        Assert.Equal(start, game.Session.Date.Hours);
        game.Clock.TogglePause();
        game.Tick(10, frozen: false);
        Assert.Equal(start, game.Session.Date.Hours);
        game.Clock.TogglePause();
        game.Tick(1, frozen: false);
        Assert.Equal(start + 1, game.Session.Date.Hours);
        Assert.Equal(21, game.Now);
    }

    [Fact]
    public void NamingACityFoundsItWithThatNameAndSelectsItsProvince()
    {
        var game = NewGame();
        var settlers = game.SelectedUnit!;
        game.OpenCityNaming(settlers.Id, settlers.ProvinceId);
        Assert.NotEqual("", game.CityName);
        game.CityName = "Villanueva";
        game.ConfirmCityName();
        Assert.Null(game.Naming);
        Assert.Equal(settlers.ProvinceId, game.SelectedProvince);
        Assert.Equal("Villanueva", game.Session.CityIn(_map.Provinces[settlers.ProvinceId])!.Name);
        game.Tick(0, frozen: true);
        Assert.Contains(game.Messages.Current(game.Now), m => m.Text.Contains("Villanueva"));
    }

    [Fact]
    public void AnInvalidCityNameKeepsTheDialogOpen()
    {
        var game = NewGame();
        var settlers = game.SelectedUnit!;
        game.OpenCityNaming(settlers.Id, settlers.ProvinceId);
        game.CityName = "";
        game.ConfirmCityName();
        Assert.NotNull(game.Naming);
        Assert.Contains(game.Messages.Current(game.Now), m => !m.Ok);
    }

    [Fact]
    public void TheMapModeCyclesBackToTerrain()
    {
        var game = NewGame();
        foreach (var _ in Enum.GetValues<MapMode>()) game.CycleMode();
        Assert.Equal(MapMode.Terrain, game.Mode);
    }
}
