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
        page.Add.First(b => b.Enabled).Press();
        Assert.Equal(before + 1, game.Human.Templates[^1].Battalions.Count);
    }

    [Fact]
    public void DeclaringWarFromDiplomacyTurnsTheButtonIntoPeace()
    {
        var game = Game(NationTab.Diplomacy, players: 2);
        var row = Assert.Single(((TablePage)game.Nation.Page()).Table.Rows);
        FindButton(row, "Declarar la guerra").Press();
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
        game.Session.DeclareWar(0, 1);
        game.Session.MakePeace(0, 1);
        var row = Assert.Single(((TablePage)game.Nation.Page()).Table.Rows);
        Assert.False(FindButton(row, "Declarar la guerra").Enabled);
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
}
