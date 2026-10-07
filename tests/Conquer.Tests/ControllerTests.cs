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
    public void FleetPanelsSetTheirMission()
    {
        var game = GameWithCapital();
        var sea = game.Session.Map.Provinces.First(p => p.IsWater);
        var fleet = game.Session.AddFleet(0, sea.Id, BattalionType.LineShip);
        game.SelectUnit(fleet.Id);
        Assert.Equal("Sin misión", game.SidePanel()!.Elements.OfType<Info>().Single(i => i.Label == "Misión").Value);
        Button(game.SidePanel()!, "Atacar convoyes").Press();
        Assert.Equal(FleetMission.Raid, fleet.Mission);
        Assert.True(Button(game.SidePanel()!, "Atacar convoyes").Active);
    }

    [Fact]
    public void AnAirMissionIsGivenFromTheAirfieldByClickingItsTarget()
    {
        var game = GameWithCapital();
        int home = game.SelectedProvince;
        var s = game.Session;
        foreach (var t in new[] { Conquer.Game.Science.Tech.Combustion, Conquer.Game.Science.Tech.Electricity, Conquer.Game.Science.Tech.Aviation }) s.Human.Learn(t);
        s.Map.Provinces[home].AddBuilding(BuildingType.Airfield);
        var wing = s.AddAirUnit(0, home, BattalionType.Fighters);
        game.ProvinceTab = ProvinceTab.Army;
        Button(game.SidePanel()!, "Superioridad aérea...").Press();
        Assert.NotNull(game.TargetPrompt);
        game.HoverProvince = s.Map.Provinces[home].Neighbors[0];
        game.ClickProvince();
        Assert.Null(game.TargetPrompt);
        Assert.Equal((AirMission.AirSuperiority, game.HoverProvince), (wing.Mission, wing.TargetProvinceId!.Value));
        Assert.Equal(home, game.SelectedProvince); // the click picked the target, not a province
    }
    [Fact]
    public void UnitPanelsShowAmmunitionShipmentsAndTheHeadquartersPriority()
    {
        var game = GameWithCapital();
        int home = game.SelectedProvince;
        var unit = game.Session.AddRegiment(0, home, BattalionType.LightInfantry);
        unit.AmmoSpent = GameSession.AmmoCapacity(unit);
        game.SelectUnit(unit.Id);
        var ammo = game.SidePanel()!.Elements.OfType<Info>().Single(i => i.Label == "Munición");
        Assert.Equal(Tone.Bad, ammo.Ink.Tone);
        Assert.Contains(game.Alerts(), a => a.Text.StartsWith("Sin munición"));

        game.Session.Human.Arm();
        do game.Session.Step(); while (game.Session.Date.Hour != 0);
        var shipments = game.SidePanel()!.Elements.OfType<Info>().Single(i => i.Label == "Envíos");
        Assert.StartsWith("1 en camino", shipments.Value);
        Assert.Contains("de munición", shipments.Tooltip);
        Assert.Contains("Sin cuartel general", shipments.Tooltip);

        var corps = game.Session.AddHeadquarters(0, home, 1);
        game.SelectUnit(corps.Id);
        Button(game.SidePanel()!, "Alta").Press();
        Assert.Equal(SupplyPriority.High, corps.SupplyPriority);
        Assert.True(Button(game.SidePanel()!, "Alta").Active);
    }

    [Fact]
    public void TheSettlersPanelOffersToFoundTheCity()
    {
        var game = NewGame();
        var doc = game.SidePanel()!;
        Assert.Equal(game.SelectedUnit!.Name, Assert.IsType<Heading>(doc.Elements[0]).Text);
        Button(doc, "Fundar ciudad").Press();
        Assert.NotNull(game.Naming);
    }

    [Fact]
    public void BuiltBuildingsShowAsIconsWithWhatTheyDoInTheTooltip()
    {
        var game = GameWithCapital();
        var capital = game.Map.Provinces[game.SelectedProvince];
        capital.AddBuilding(BuildingType.Barracks);
        capital.AddBuilding(BuildingType.Farm);
        game.ProvinceTab = ProvinceTab.Buildings;
        var grid = game.SidePanel()!.Elements.OfType<IconGrid>().Single();
        Assert.Equal(capital.Buildings.Count, grid.Tiles.Count);
        var farm = grid.Tiles.Single(t => t.Icon == new BuildingIcon(BuildingType.Farm));
        Assert.StartsWith("Granja", farm.Tooltip);
        Assert.Contains(BuildingType.Farm.Info().Description, farm.Tooltip);
        Assert.Equal(0, farm.Damage);
    }

    [Fact]
    public void AWorkshopsChoicesAreTitledByWhatItMakes()
    {
        var game = GameWithCapital();
        var capital = game.Map.Provinces[game.SelectedProvince];
        capital.AddBuilding(BuildingType.Workshop);
        game.ProvinceTab = ProvinceTab.Buildings;
        var doc = game.SidePanel()!;
        Assert.Contains(doc.Elements, e => e is Button { Text: var t } && t.StartsWith("Armas antiguas"));
        Button(doc, "Suministros").Press();
        Assert.Equal(BattalionType.Scouts.First().Key, capital.Production);
        // Another tab is another page, which the client scrolls back to the top.
        game.ProvinceTab = ProvinceTab.General;
        Assert.NotEqual(doc.Key, game.SidePanel()!.Key);
    }

    [Fact]
    public void TheCapitalsTabsSwitchAndItsCloseButtonDeselects()
    {
        var game = GameWithCapital();
        var doc = game.SidePanel()!;
        Assert.Contains(doc.Elements, e => e is Button { Text: var t } && t.StartsWith("Celebrar fiestas"));
        Button(doc, "Edificios").Press();
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
        for (int i = 0; i < 1000; i++) stepper.More[^1].Press();
        int keep = GameRules.MinCityPopulation;
        Assert.Equal((int)game.Map.Provinces[game.SelectedProvince].Population - keep, game.MigrationAmount);
        for (int i = 0; i < 1000; i++) stepper.Less[0].Press();
        Assert.Equal(1, game.MigrationAmount);
    }

    [Fact]
    public void ForcedMigrantsShowInTheirDestinationsPopulationOnTheWay()
    {
        var game = GameWithCapital();
        var s = game.Session;
        int from = game.SelectedProvince;
        var to = _map.Provinces[from].Neighbors.Select(n => _map.Provinces[n]).First(p => p.IsClaimable && !p.IsOwned);
        Assert.True(s.Claim(0, s.AddRegiment(0, to.Id, BattalionType.Scouts).Id).Ok);
        s.Human.Stockpile[ResourceType.Gold] = 1000;
        Assert.True(s.ForceMigration(0, from, to.Id, 50).Ok);

        game.SelectProvince(to.Id);
        var population = game.SidePanel()!.Elements.OfType<Info>().Single(i => i.Label == "Población");
        Assert.Contains("(+50 en camino)", population.Value);
        game.HoverProvince = to.Id;
        Assert.Contains("(+50 en camino)", game.MapTooltip());
    }

    [Fact]
    public void TheResourcesMapShowsEveryDepositOfExploredProvincesCloseIn()
    {
        var game = NewGame();
        game.Camera.Screen = new System.Numerics.Vector2(1600, 900);
        var rich = _map.Provinces.First(p => p.HasDeposit(ResourceType.Copper) && p.HasDeposit(ResourceType.Gold) && !game.IsExplored(p.Id));
        game.Mode = MapMode.Resources;
        game.Camera.LookAt(game.Center(rich.Id), 6);
        Assert.True(game.DepositIconsShown);
        Assert.DoesNotContain(game.Markers().Deposits, d => d.ProvinceId == rich.Id); // never seen

        game.Human.Explored.Add(rich.Id);
        var mark = Assert.Single(game.Markers().Deposits, d => d.ProvinceId == rich.Id);
        Assert.Contains(ResourceType.Copper, mark.Resources);
        Assert.Contains(ResourceType.Gold, mark.Resources);
        Assert.All(mark.Resources, r => Assert.True(game.Human.Knows(r)));

        game.ResourceFilter = ResourceType.Gold;
        Assert.Equal([ResourceType.Gold], Assert.Single(game.Markers().Deposits, d => d.ProvinceId == rich.Id).Resources);

        // Far out, the provinces are coloured instead.
        game.Camera.LookAt(game.Center(rich.Id), 1);
        Assert.False(game.DepositIconsShown);
        Assert.Empty(game.Markers().Deposits);
    }

    [Fact]
    public void UnitsInAProvinceMakeOneStackThatClicksGoThrough()
    {
        var game = GameWithCapital();
        game.Camera.Screen = new System.Numerics.Vector2(1600, 900);
        int home = game.SelectedProvince;
        var ids = Enumerable.Range(0, 3).Select(_ => game.Session.AddRegiment(0, home, BattalionType.LightInfantry).Id).ToList();
        game.SelectUnit(ids[1]);
        UnitCounter Stack() => Assert.Single(game.Markers().Units, c => c.Stack!.Contains(ids[0]));

        game.Camera.LookAt(game.Center(home), 2);
        var stack = Stack();
        Assert.Equal(ids[1], stack.UnitId); // the selected one on top
        Assert.True(stack.Selected);
        Assert.Equal(ids, stack.Stack!.Where(ids.Contains));
        Assert.Equal(stack.Stack!.Count, stack.Count);
        Assert.Equal(game.Camera.MapToScreen(game.Center(home)), stack.Screen);
        Assert.NotNull(stack.Flag);

        // Clicking it again selects the next unit in it, and after the last, the first.
        var order = stack.Stack!.ToList();
        for (int i = 1; i <= order.Count; i++)
        {
            game.SelectInStack(Stack().Stack!);
            Assert.Equal(order[(order.IndexOf(ids[1]) + i) % order.Count], game.SelectedUnitId);
        }

        // Close in, the stack stands above the city, clear of it and its name.
        game.Camera.LookAt(game.Center(home), 8);
        Assert.True(Stack().Screen.Y < game.Camera.MapToScreen(game.Center(home)).Y - 30);
    }

    [Fact]
    public void UnitNamesAreCutShortForTheirCounters()
    {
        Assert.Equal("3.er Rgto.", UnitLabels.Short("3.er Regimiento"));
        Assert.Equal("II Cpo.", UnitLabels.Short("II Cuerpo"));
        Assert.Equal("1.er G. Ej.", UnitLabels.Short("1.er Grupo de ejércitos"));
        Assert.Equal("Los Tercios.", UnitLabels.Short("Los Tercios de Flandes"));
        Assert.True(Flags.IsKnown("España"));
        Assert.NotEmpty(Flags.Of("Atlántida", 0xFF336699).Shapes);
    }

    [Fact]
    public void TheTopBarSpeedButtonsSetTheClock()
    {
        var game = NewGame();
        var bar = game.TopBar();
        Assert.Equal(6, bar.Speeds.Count);
        bar.Speeds[4].Press();
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
        s.Train(game.Human.Id, p.Id, BattalionType.LightInfantry);
        s.Train(game.Human.Id, p.Id, BattalionType.LightInfantry);
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
        Button(game.SidePanel()!, "Editar unidad").Press();
        var editor = game.UnitEditor()!;
        Assert.Equal(2, editor.Battalions.Count);
        Assert.False(editor.Split!.Enabled);
        editor.Battalions[1].Press();
        game.UnitEditor()!.Split!.Press();
        Assert.Single(regiment.Battalions);
        Assert.Equal(2, game.Session.Units.Count(u => u.OwnerId == game.Human.Id && u.IsMilitary));
    }

    [Fact]
    public void TheUnitEditorFormsABrigadeAndLetsARegimentGo()
    {
        var game = GameWithRegiment(out var regiment);
        var other = game.Session.AddRegiment(game.Human.Id, regiment.ProvinceId, BattalionType.LightInfantry);
        string otherName = other.Name;
        game.OpenUnitEditor(regiment);
        var editor = game.UnitEditor()!;
        Assert.Contains(editor.Merges, b => b.Text.StartsWith("Unir ") && b.Enabled);
        Assert.Empty(editor.Parts);

        editor.Merges.First(b => b.Text.StartsWith("Incorporar ")).Press();
        Assert.Equal(Echelon.Brigade, regiment.Size);
        editor = game.UnitEditor()!;
        Assert.Equal(2, editor.Parts.Count);
        editor.Parts.First(b => b.Text.Contains(otherName)).Press();
        Assert.Contains(game.Session.Units, u => u.Name == otherName && u.Size == Echelon.Regiment);
        Assert.Single(regiment.Regiments);
        Assert.All(game.UnitEditor()!.Parts, b => Assert.False(b.Enabled)); // the last part stays
    }

    [Fact]
    public void TheUnitEditorRenamesAndTheAutomaticNameComesBack()
    {
        var game = GameWithRegiment(out var regiment);
        game.OpenUnitEditor(regiment);
        game.UnitName = "Los Valientes";
        game.UnitEditor()!.Rename.Press();
        Assert.Equal("Los Valientes", regiment.Name);
        game.UnitEditor()!.AutomaticName!.Press();
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
        window.Cancel.Press();
        Assert.False(game.RoadWindowOpen);
    }

    [Fact]
    public void TheBattleWindowShowsBothSidesAndGoesToTheProvince()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        var (a, b) = _map.Provinces
            .Where(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3)
            .SelectMany(p => p.Neighbors.Select(n => (A: p, B: _map.Provinces[n])))
            .First(t => t.B.Biome == Biome.Grassland && t.B.Neighbors.Length > 3);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        var claimer = s.AddRegiment(1, b.Id, BattalionType.Scouts);
        s.Claim(1, claimer.Id);
        s.AddRegiment(1, b.Id, BattalionType.LightInfantry, BattalionType.LightInfantry);
        var attacker = s.AddRegiment(0, a.Id, BattalionType.LightInfantry, BattalionType.LightInfantry);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, attacker.Id, b.Id);
        for (int h = 0; h < 24 * 5 && (s.BattleIn(b.Id) is null || s.BattleIn(b.Id)!.History.Count < 3); h++) s.Step();

        var game = new GameController(s);
        game.OpenFirstBattle();
        var window = game.BattleWindow()!;
        Assert.Equal(2, window.Sides.Count);
        Assert.Equal("Atacante", Assert.IsType<Banner>(window.Sides[0].Elements[0]).Right);
        Assert.Contains(window.Sides[0].Elements, e => e is UnitEntry entry && entry.Name == attacker.Name);
        Assert.NotNull(window.Fire);
        Assert.NotNull(window.Chart);
        Assert.Contains("Ahora", window.Chart!.Describe(window.Chart.Points.Count - 1));
        window.GoTo.Press();
        Assert.False(game.BattleWindowOpen);
        Assert.Equal(game.Center(b.Id), game.Camera.Center);
    }

    [Fact]
    public void TheMapShowsTheSelectedSettlersAndThenTheirCity()
    {
        var game = NewGame();
        game.Camera.Screen = new System.Numerics.Vector2(1600, 900);
        var counter = Assert.Single(game.Markers().Units, c => c.UnitId == game.SelectedUnitId);
        Assert.Equal(CounterKind.Settlers, counter.Kind);
        Assert.True(counter.Selected);
        Assert.Equal(1, counter.Scale);

        var settlers = game.SelectedUnit!;
        game.OpenCityNaming(settlers.Id, settlers.ProvinceId);
        game.CityName = "Villanueva";
        game.ConfirmCityName();
        var city = Assert.Single(game.Markers().Cities);
        Assert.True(city.Capital);
        Assert.Equal("Villanueva", city.Name);
        Assert.DoesNotContain(game.Markers().Units, c => c.UnitId == settlers.Id);
    }

    [Fact]
    public void AMarchingCounterSaysWhereItIsGoingSoItCanHop()
    {
        var game = NewGame();
        game.Camera.Screen = new System.Numerics.Vector2(1600, 900);
        var settlers = game.SelectedUnit!;
        Assert.False(Assert.Single(game.Markers().Units).Moving);
        var target = game.Map.Provinces[settlers.ProvinceId].Neighbors.First(n => !game.Map.Provinces[n].IsWater);
        Assert.True(game.Session.MoveUnit(0, settlers.Id, target).Ok);
        var counter = Assert.Single(game.Markers().Units);
        Assert.True(counter.Moving);
        Assert.False(counter.Fighting);
        Assert.Equal(1, counter.Heading.Length(), 3);
    }

    [Fact]
    public void ThePauseMenuOpensTheOptionsAndStopsTime()
    {
        var game = NewGame();
        game.MenuOpen = true;
        game.PauseMenu(new NoNavigator(), "test").Single(b => b.Text == "Opciones").Press();
        Assert.True(game.Settings.Open);
        Assert.False(game.MenuOpen);
        Assert.True(game.TimeStopped);
        game.Escape();
        Assert.False(game.Settings.Open);
    }

    [Fact]
    public void TheMapModeCyclesBackToTerrain()
    {
        var game = NewGame();
        foreach (var _ in Enum.GetValues<MapMode>()) game.CycleMode();
        Assert.Equal(MapMode.Terrain, game.Mode);
    }

    /// <summary>A game with its capital founded from the starting settlers.</summary>
    private (GameController Game, Province Capital) WithCapital()
    {
        var game = NewGame();
        var settlers = game.SelectedUnit!;
        game.OpenCityNaming(settlers.Id, settlers.ProvinceId);
        game.ConfirmCityName();
        return (game, _map.Provinces[settlers.ProvinceId]);
    }

    private static Alert? AlertStarting(GameController game, string text) => game.Alerts().FirstOrDefault(a => a.Text.StartsWith(text));

    [Fact]
    public void AFinishedBuildingRaisesAnAlertForAFewDays()
    {
        var (game, capital) = WithCapital();
        game.Human.Stockpile[Game.Economy.ResourceType.Wood] = game.Human.Stockpile[Game.Economy.ResourceType.Gold] = 1000;
        capital.Population = 1000;
        Assert.True(game.Session.Build(0, capital.Id, Game.Buildings.BuildingType.Farm).Ok);
        for (int h = 0; h < 24 * 40 && AlertStarting(game, "Obras terminadas") is null; h++) game.Session.Step();
        var alert = AlertStarting(game, "Obras terminadas")!;
        Assert.Equal("Obras terminadas (1)", alert.Text);
        alert.OnClick();
        Assert.Equal(capital.Id, game.SelectedProvince);
        for (int h = 0; h < 24 * (GameSession.RecentWorkDays + 1); h++) game.Session.Step();
        Assert.Null(AlertStarting(game, "Obras terminadas"));
    }

    [Fact]
    public void ANewGameHasNoAlerts() => Assert.Empty(NewGame().Alerts());

    [Fact]
    public void IdleResearchAndHungerRaiseAlertsThatOpenTheNationScreen()
    {
        var (game, _) = WithCapital();
        var science = AlertStarting(game, "Ciencia sin elegir")!;
        Assert.IsType<ScienceIcon>(science.Icon);
        science.OnClick();
        Assert.True(game.Nation.Visible);
        Assert.Equal(NationTab.Science, game.Nation.Tab);

        game.Human.IsStarving = true;
        var hunger = Assert.IsType<Alert>(AlertStarting(game, "Hambre"));
        Assert.Equal(Tone.Bad, hunger.Tone);
        Assert.Same(hunger.Text, game.Alerts()[0].Text); // the most urgent first
    }

    [Fact]
    public void EachClickOnAnAlertShowsTheNextUnit()
    {
        var (game, capital) = WithCapital();
        var far = _map.Provinces.Where(p => p.IsClaimable && _map.DistanceKm(p, capital) > 6000).Take(2).ToList();
        var first = game.Session.AddRegiment(0, far[0].Id, BattalionType.LightInfantry);
        var second = game.Session.AddRegiment(0, far[1].Id, BattalionType.LightInfantry);

        var alert = AlertStarting(game, "Sin suministro")!;
        Assert.Equal("Sin suministro (2)", alert.Text);
        Assert.Contains(first.Name, alert.Tooltip);
        alert.OnClick();
        Assert.Equal(first.Id, game.SelectedUnitId);
        AlertStarting(game, "Sin suministro")!.OnClick();
        Assert.Equal(second.Id, game.SelectedUnitId);
    }

    [Fact]
    public void RestlessProvincesRaiseAnAlertThatLeadsToThem()
    {
        var (game, capital) = WithCapital();
        capital.Mood = 10;
        capital.RevoltProgress = GameRules.RevoltDays / 2;
        var alert = AlertStarting(game, "Descontento")!;
        Assert.Equal("Descontento (1)", alert.Text);
        Assert.Contains("rebelión 50", alert.Tooltip);
        alert.OnClick();
        Assert.Equal(capital.Id, game.SelectedProvince);
    }

    [Fact]
    public void WarPeaceAndOrdersSoundAndTheMusicFollowsTheWar()
    {
        var game = new GameController(GameSession.Create(_map, 2, seed: 7, computerRivals: false));
        Assert.Equal(Soundtrack.For(Game.Science.Era.Ancient, atWar: false), game.Playlist);
        game.TakeSounds();

        game.Show(game.Session.DeclareWar(0, 1));
        Assert.Equal([SoundCue.War, SoundCue.Confirm], game.TakeSounds());
        Assert.Equal(Soundtrack.For(Game.Science.Era.Ancient, atWar: true), game.Playlist);

        game.Session.MakePeace(0, 1);
        Assert.Equal([SoundCue.Peace], game.TakeSounds());
        Assert.Empty(game.TakeSounds());

        // A failed order makes no sound.
        game.Show(game.Session.ProposePeace(0, 1));
        Assert.Empty(game.TakeSounds());
    }

    [Fact]
    public void TheMusicTurnsOrchestralInTheAgeOfDiscoveries()
    {
        Assert.NotEqual(Soundtrack.For(Game.Science.Era.Medieval, false), Soundtrack.For(Game.Science.Era.Renaissance, false));
        Assert.Equal(Soundtrack.For(Game.Science.Era.Renaissance, true), Soundtrack.For(Game.Science.Era.Modern, true));
    }
}
