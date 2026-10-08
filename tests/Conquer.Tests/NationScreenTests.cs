using Conquer.Game.Economy;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>The nation screen as data: its tables sort, its buttons act, and the unit designer edits templates.</summary>
[Collection("World")]
public class NationScreenTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>A game with its capital founded and the nation screen open on <paramref name="tab"/>.</summary>
    private GameController Game(NationTab tab, int players = 1)
    {
        var game = new GameController(GameSession.Create(_map, players, seed: 7));
        var settlers = game.SelectedUnit!;
        game.OpenCityNaming(settlers.Id, settlers.ProvinceId);
        game.ConfirmCityName();
        game.Nation.Visible = true;
        game.Nation.Tab = tab;
        return game;
    }

    private static Button FindButton(IEnumerable<Cell> cells, string text) =>
        cells.OfType<ButtonsCell>().SelectMany(c => c.Buttons).First(b => b.Text == text);

    [Fact]
    public void TheCitiesTableListsTheCapitalAndItsViewButtonShowsItOnTheMap()
    {
        var game = Game(NationTab.Cities);
        var table = Assert.IsType<TablePage>(game.Nation.Page()).Table;
        var row = Assert.Single(table.Rows);
        var capital = game.Session.Cities.Single();
        Assert.Equal(capital.Name, Assert.IsType<TextCell>(row[0]).Text);
        FindButton(row, "Ver").Press();
        Assert.False(game.Nation.Visible);
        Assert.Equal(capital.ProvinceId, game.SelectedProvince);
    }

    [Fact]
    public void PressingAColumnTitleSortsByItAndPressingItAgainReverses()
    {
        var game = Game(NationTab.Provinces);
        var table = ((TablePage)game.Nation.Page()).Table;
        Assert.Equal(1, table.SortColumn);
        table.SortBy!(0);
        table = ((TablePage)game.Nation.Page()).Table;
        Assert.Equal(0, table.SortColumn);
        Assert.True(table.SortAscending);
        table.SortBy!(0);
        Assert.False(((TablePage)game.Nation.Page()).Table.SortAscending);
    }

    [Fact]
    public void TheProvincesTableShowsWhatEachProvinceGivesAndWhatItsWorkshopMakes()
    {
        var game = Game(NationTab.Provinces);
        var capital = game.Session.Map.Provinces[game.Session.Cities.Single().ProvinceId];
        var output = game.Session.ProvinceOutput(capital);
        Assert.True(output[(int)ResourceType.Gold] > 0);
        Assert.True(output[(int)ResourceType.Food] > 0);

        var table = ((TablePage)game.Nation.Page()).Table;
        int given = table.Columns.ToList().FindIndex(c => c.Title == "Aporta al día");
        int workshop = table.Columns.ToList().FindIndex(c => c.Title == "Taller");
        var row = table.Rows.Single(r => ((TextCell)r[0]).Text == capital.DisplayName);
        Assert.Contains("oro", ((TextCell)row[given]).Tooltip);
        Assert.Equal("-", ((TextCell)row[workshop]).Text); // no workshop yet
    }

    [Fact]
    public void TheDesignerAddsABattalionToANewTemplate()
    {
        var game = Game(NationTab.Templates);
        var page = Assert.IsType<TemplatesPage>(game.Nation.Page());
        int templates = game.Human.Templates.Count;
        page.Actions.First(b => b.Text == "Nueva plantilla").Press();
        Assert.Equal(templates + 1, game.Human.Templates.Count);
        page = (TemplatesPage)game.Nation.Page();
        Assert.Equal(game.Human.Templates[^1].Name, page.Name);
        int before = game.Human.Templates[^1].Battalions.Count;
        page.Sections.SelectMany(s => s.Add).First(b => b.Enabled).Press();
        Assert.Equal(before + 1, game.Human.Templates[^1].Battalions.Count);
    }

    [Fact]
    public void TheNavyAndAirTabsAppearWithTheAdvancesThatBuildThem()
    {
        var game = Game(NationTab.Navy);
        Assert.DoesNotContain(NationTab.Navy, game.Nation.Tabs);
        Assert.DoesNotContain(NationTab.AirForce, game.Nation.Tabs);
        Assert.Equal(NationTab.Summary, game.Nation.Tab); // a hidden tab reads as the summary

        // Navigation finishes: the tab appears and the player is told.
        var human = game.Human;
        human.Researching[(int)Conquer.Game.Science.TechBranch.Economy] = Conquer.Game.Science.Tech.Navigation;
        human.ResearchProgress[(int)Conquer.Game.Science.Tech.Navigation] = 1e9;
        for (int i = 0; i < 24 && !human.Techs.Contains(Conquer.Game.Science.Tech.Navigation); i++) game.Session.Step();
        Assert.Contains(NationTab.Navy, game.Nation.Tabs);
        Assert.Equal(NationTab.Navy, game.Nation.Tab);
        Assert.Contains(game.Session.Notifications, n => n.Text.Contains("puertos y barcos") && n.Text.Contains("pestaña Marina"));
        Assert.DoesNotContain(NationTab.AirForce, game.Nation.Tabs);

        human.Learn(Conquer.Game.Science.Tech.Aviation);
        Assert.Contains(NationTab.AirForce, game.Nation.Tabs);
        Assert.Equal(NationScreen.TabNames.Length, game.Nation.Tabs.Count);
    }

    [Fact]
    public void ATemplateIsRenamedFromTheDesigner()
    {
        var game = Game(NationTab.Templates);
        var page = Assert.IsType<TemplatesPage>(game.Nation.Page());
        Assert.Null(page.Renaming);
        page.Rename!.Press();
        page = (TemplatesPage)game.Nation.Page();
        Assert.Equal(game.Human.Templates[0].Name, game.Nation.TemplateNameDraft);
        Assert.False(page.Rename!.Enabled);

        game.Nation.TemplateNameDraft = "  Guardia  ";
        page.Renaming!.First(b => b.Text == "Aceptar").Press();
        Assert.Equal("Guardia", game.Human.Templates[0].Name);
        Assert.Null(game.Nation.TemplateNameDraft);
        Assert.Equal("Guardia", ((TemplatesPage)game.Nation.Page()).Name);

        // A name already taken is refused and the field stays; an empty one gives the number back.
        Assert.True(game.Session.CreateTemplate(game.Human.Id).Ok);
        Assert.False(game.Session.RenameTemplate(game.Human.Id, game.Human.Templates[^1].Id, "guardia").Ok);
        Assert.True(game.Session.RenameTemplate(game.Human.Id, game.Human.Templates[0].Id, "").Ok);
        Assert.Equal("Plantilla I", game.Human.Templates[0].Name);
    }

    [Fact]
    public void DeclaringWarFromDiplomacyTurnsTheButtonIntoPeace()
    {
        var game = Game(NationTab.Diplomacy, players: 2);
        game.Session.MeetEveryone();
        var row = Assert.Single(((TablePage)game.Nation.Page()).Table.Rows);
        FindButton(row, "Guerra").Press();
        row = Assert.Single(((TablePage)game.Nation.Page()).Table.Rows);
        Assert.NotNull(FindButton(row, "Paz blanca"));
        // Nothing occupied yet: nothing to demand or hand over.
        Assert.False(FindButton(row, "Exigir (0)").Enabled);
        Assert.False(FindButton(row, "Ceder (0)").Enabled);
    }

    [Fact]
    public void AfterPeaceTheTruceBlocksANewWar()
    {
        var game = Game(NationTab.Diplomacy, players: 2);
        game.Session.MeetEveryone();
        game.Session.DeclareWar(0, 1);
        game.Session.MakePeace(0, 1);
        var row = Assert.Single(((TablePage)game.Nation.Page()).Table.Rows);
        Assert.False(FindButton(row, "Guerra").Enabled);
        Assert.StartsWith("Tregua", Assert.IsType<TextCell>(row[1]).Text);
    }

    [Fact]
    public void ScienceShowsThreeBranchesAndResearchingPicksTheAdvance()
    {
        var game = Game(NationTab.Science);
        var page = Assert.IsType<SciencePage>(game.Nation.Page());
        Assert.Equal(3, page.Branches.Count);
        var card = page.Branches[0].Levels.SelectMany(l => l.Cards).First(c => c.Research is { Enabled: true });
        card.Research!.Press();
        var branch = ((SciencePage)game.Nation.Page()).Branches[0];
        Assert.NotNull(branch.Progress);
        Assert.Contains(card.Name, branch.Status);
    }

    [Fact]
    public void TheSummaryShowsTheCapital()
    {
        var game = Game(NationTab.Summary);
        var page = Assert.IsType<SummaryPage>(game.Nation.Page());
        Assert.Contains(page.Left.Elements, e => e is Pair { Label: "Capital" } pair && pair.Value == game.Session.Cities.Single().Name);
    }

    [Fact]
    public void TheStoreListsEachResourceWithWhatCameInAndWentOutAndThenTheEquipment()
    {
        var game = Game(NationTab.Store);
        for (int h = 0; h < 24; h++) game.Session.Step();
        var page = Assert.IsType<TablesPage>(game.Nation.Page());
        Assert.Equal("Almacén", NationScreen.TabNames[(int)NationTab.Store]);
        var (resources, equipment) = (page.Tables[0].Table, page.Tables[1].Table);
        var food = resources.Rows.Single(r => ((TextCell)r[0]).Text == "Comida");
        Assert.StartsWith("+", ((TextCell)food[2]).Text); // the harvest
        Assert.StartsWith("-", ((TextCell)food[4]).Text); // what the people eat
        // The flows add up to the day's balance.
        var flows = game.Human.LastDayFlows;
        foreach (var r in Resources.All)
            Assert.Equal(game.Human.LastDayNet[(int)r], flows.Sum(f => f[(int)r]), 6);
        Assert.Contains(equipment.Rows, r => ((TextCell)r[0]).Text == "Suministros");
    }

    [Fact]
    public void TheUnitsTabShowsEveryModelWithItsFiguresAndEquipment()
    {
        var game = Game(NationTab.Units);
        var table = Assert.IsType<TablePage>(game.Nation.Page()).Table;
        var catapults = table.Rows.Single(r => ((TextCell)r[0]).Text == "Catapultas");
        Assert.Equal("50", ((TextCell)catapults[1]).Text);
        Assert.Equal("5 catapultas", ((TextCell)catapults[7]).Text);
        var warriors = table.Rows.Single(r => ((TextCell)r[0]).Text == "Guerreros");
        Assert.True(((TextCell)warriors[0]).Bold); // the model the nation trains now
        Assert.Contains(table.Rows, r => ((TextCell)r[0]).Text == "Portaaviones" && ((TextCell)r[7]).Text == "Se construye entero");
        // The light infantry the heavy infantry turns into is listed once.
        Assert.Single(table.Rows, r => ((TextCell)r[0]).Text == "Infantería ligera" && r.Count > 1);
    }
}
