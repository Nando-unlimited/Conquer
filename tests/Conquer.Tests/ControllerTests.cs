using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
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

    /// <summary>A game whose capital has been founded, with its province selected.</summary>
    private GameController GameWithCapital()
    {
        var game = NewGame();
        var settlers = game.SelectedUnit!;
        game.OpenCityNaming(settlers.Id, settlers.ProvinceId);
        game.ConfirmCityName();
        return game;
    }

    private static Button Button(Document doc, string text) =>
        doc.Elements.OfType<Button>().Concat(doc.Elements.OfType<ButtonRow>().SelectMany(r => r.Buttons)).First(b => b.Text.StartsWith(text));

    [Fact]
    public void TheSettlersPanelOffersToFoundTheCity()
    {
        var game = NewGame();
        var doc = game.SidePanel()!;
        Assert.Equal(game.SelectedUnit!.Name, Assert.IsType<Heading>(doc.Elements[0]).Text);
        Button(doc, "Fundar ciudad").OnClick!();
        Assert.NotNull(game.Naming);
    }

    [Fact]
    public void TheCapitalsTabsSwitchAndItsCloseButtonDeselects()
    {
        var game = GameWithCapital();
        var doc = game.SidePanel()!;
        Assert.Contains(doc.Elements, e => e is Button { Text: var t } && t.StartsWith("Celebrar fiestas"));
        Button(doc, "Edificios").OnClick!();
        Assert.Equal(ProvinceTab.Buildings, game.ProvinceTab);
        Assert.Contains(game.SidePanel()!.Elements, e => e is Heading { Text: "Construir" });
        game.SidePanel()!.OnClose!();
        Assert.Null(game.SidePanel());
    }

    [Fact]
    public void TheForcedMigrationNeverAsksForMoreThanCanLeave()
    {
        var game = GameWithCapital();
        var stepper = game.SidePanel()!.Elements.OfType<Stepper>().Single();
        for (int i = 0; i < 1000; i++) stepper.More[^1].OnClick!();
        int keep = GameRules.MinCityPopulation;
        Assert.Equal((int)game.Map.Provinces[game.SelectedProvince].Population - keep, game.MigrationAmount);
        for (int i = 0; i < 1000; i++) stepper.Less[0].OnClick!();
        Assert.Equal(1, game.MigrationAmount);
    }

    [Fact]
    public void TheTopBarSpeedButtonsSetTheClock()
    {
        var game = NewGame();
        var bar = game.TopBar();
        Assert.Equal(6, bar.Speeds.Count);
        bar.Speeds[4].OnClick!();
        Assert.Equal(4, game.Clock.Speed);
        Assert.True(game.TopBar().Speeds[4].Active);
    }

    /// <summary>A game with a two-battalion regiment of the player's in the capital.</summary>
    private GameController GameWithRegiment(out Unit regiment)
    {
        var game = GameWithCapital();
        var s = game.Session;
        var p = _map.Provinces[game.SelectedProvince];
        p.Population += 1000;
        p.AddBuilding(BuildingType.Barracks);
        foreach (var r in new[] { ResourceType.Wood, ResourceType.Gold }) game.Human.Stockpile[r] += 1000;
        s.Train(game.Human.Id, p.Id, BattalionType.Warriors);
        s.Train(game.Human.Id, p.Id, BattalionType.Warriors);
        for (int h = 0; h < 24 * 25; h++) s.Step();
        var units = s.Units.Where(u => u.OwnerId == game.Human.Id && u.IsMilitary).ToList();
        s.Merge(game.Human.Id, units[0].Id, units[1].Id);
        regiment = units[0];
        return game;
    }

    [Fact]
    public void TheUnitEditorSplitsTheMarkedBattalionsIntoANewUnit()
    {
        var game = GameWithRegiment(out var regiment);
        game.SelectUnit(regiment.Id);
        Button(game.SidePanel()!, "Editar unidad").OnClick!();
        var editor = game.UnitEditor()!;
        Assert.Equal(2, editor.Battalions.Count);
        Assert.False(editor.Split!.Enabled);
        editor.Battalions[1].OnClick!();
        game.UnitEditor()!.Split!.OnClick!();
        Assert.Single(regiment.Battalions);
        Assert.Equal(2, game.Session.Units.Count(u => u.OwnerId == game.Human.Id && u.IsMilitary));
    }

    [Fact]
    public void TheUnitEditorRenamesAndTheAutomaticNameComesBack()
    {
        var game = GameWithRegiment(out var regiment);
        game.OpenUnitEditor(regiment);
        game.UnitName = "Los Valientes";
        game.UnitEditor()!.Rename.OnClick!();
        Assert.Equal("Los Valientes", regiment.Name);
        game.UnitEditor()!.AutomaticName!.OnClick!();
        Assert.Null(regiment.CustomName);
        game.CloseUnitEditor();
        Assert.Null(game.UnitEditor());
    }

    [Fact]
    public void TheRoadWindowWithNowhereToGoSaysSoAndCancels()
    {
        var game = GameWithCapital();
        game.OpenRoadWindow(game.SelectedProvince, RoadKinds.All[0]);
        var window = game.RoadWindow()!;
        Assert.NotNull(window.None);
        Assert.False(window.Build.Enabled);
        window.Cancel.OnClick!();
        Assert.False(game.RoadWindowOpen);
    }

    [Fact]
    public void TheMapModeCyclesBackToTerrain()
    {
        var game = NewGame();
        foreach (var _ in Enum.GetValues<MapMode>()) game.CycleMode();
        Assert.Equal(MapMode.Terrain, game.Mode);
    }
}
