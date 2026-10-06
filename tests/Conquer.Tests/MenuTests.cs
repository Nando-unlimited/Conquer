using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>The menus as data: what their buttons allow and where they lead.</summary>
[Collection("World")]
public class MenuTests(WorldFixture world)
{
    /// <summary>Remembers where the menus asked to go.</summary>
    private sealed class FakeNavigator : IMenuNavigator
    {
        public string Last = "";
        public WorldSettings? Settings;
        public int Players;
        public SaveFile? Loaded;

        public void ShowMainMenu() => Last = "main";
        public void ShowNewGame() => Last = "new";
        public void ShowLoadGame() => Last = "load";
        public void StartNewGame(WorldSettings settings, int players) => (Last, Settings, Players) = ("start", settings, players);
        public void LoadSavedGame(SaveFile save) => (Last, Loaded) = ("loaded", save);
        public void Quit() => Last = "quit";
    }


    [Fact]
    public void TheTitleScreenOpensTheOptions()
    {
        var nav = new FakeNavigator();
        var menu = new MainMenu(nav);
        menu.Other().Single(b => b.Text == "Opciones").Press();
        Assert.True(menu.Settings.Open);
        // While the options are open, the other buttons do nothing.
        menu.Main()[1].Press();
        Assert.Equal("", nav.Last);
        menu.Settings.Close.Press();
        Assert.False(menu.Settings.Open);
        menu.Main()[1].Press();
        Assert.Equal("new", nav.Last);
    }

    [Fact]
    public void TheOptionsChangeTheVolumesAndTheMap()
    {
        var settings = new SettingsMenu();
        double music = AudioSettings.Current.Music;
        bool models = DisplaySettings.Current.UnitModels;
        var volume = settings.Rows().Single(r => r.Label == "Música");
        Assert.Equal(AudioSettings.Label(music), volume.Value);
        (music > 0 ? volume.Buttons[0] : volume.After![0]).Press();
        Assert.NotEqual(music, AudioSettings.Current.Music);
        settings.Rows().Single(r => r.Label == "Mapa").Buttons[models ? 1 : 0].Press();
        Assert.Equal(!models, DisplaySettings.Current.UnitModels);

        // Back as they were.
        AudioSettings.Current.ChangeMusic((int)Math.Round((music - AudioSettings.Current.Music) * 4));
        DisplaySettings.Current.SetUnitModels(models);
        Assert.Equal(music, AudioSettings.Current.Music);
    }
    [Fact]
    public void EachMapSizeTakesUpToItsNumberOfNations()
    {
        var nav = new FakeNavigator();
        var menu = new NewGameMenu(nav);
        for (int i = 0; i < 60; i++) menu.PlayersRow().After![0].Press();
        Assert.Equal(MapSize.Large.Info().MaxNations.ToString(), menu.PlayersRow().Value);
        Assert.False(menu.PlayersRow().After![0].Enabled);
        menu.SizeRow().Buttons[(int)MapSize.Tiny].Press();
        Assert.Equal(MapSize.Tiny.Info().MaxNations.ToString(), menu.PlayersRow().Value);
        menu.Start.Press();
        Assert.Equal(MapSize.Tiny.Info().MaxNations, nav.Players);
        Assert.All(MapSizes.All, size => Assert.InRange(size.Info().MaxNations, 2, Countries.MaxNations));
    }

    [Fact]
    public void TheNewGameMenuStartsTheChosenGame()
    {
        var nav = new FakeNavigator();
        var menu = new NewGameMenu(nav);
        menu.MapRow().Buttons[1].Press();
        Assert.False(menu.SeedRow().Enabled);
        Assert.All(menu.SizeRow().Buttons, b => Assert.False(b.Enabled));
        for (int i = 0; i < 10; i++) menu.PlayersRow().Buttons[0].Press();
        Assert.False(menu.PlayersRow().Buttons[0].Enabled);
        menu.DifficultyRow().After![0].Press();
        menu.Start.Press();
        Assert.Equal("start", nav.Last);
        Assert.Equal(1, nav.Players);
        Assert.Equal(MapKind.Earth, nav.Settings!.Kind);
        Assert.Equal(Difficulty.Hard, nav.Settings.Difficulty);
        menu.Back.Press();
        Assert.Equal("main", nav.Last);
    }

    [Fact]
    public void TheMainMenuDoesNothingElseWhileTheChangelogIsOpen()
    {
        var nav = new FakeNavigator();
        var menu = new MainMenu(nav);
        menu.Other().Single(b => b.Text == "Historial de versiones").Press();
        Assert.True(menu.ChangelogOpen);
        menu.Main()[1].Press();
        menu.Other().Single(b => b.Text == "Salir").Press();
        menu.Other().Single(b => b.Text == "Opciones").Press();
        Assert.False(menu.Settings.Open);
        Assert.Equal("", nav.Last);
        menu.ChangelogOpen = false;
        menu.Main()[1].Press();
        Assert.Equal("new", nav.Last);
    }

    [Fact]
    public void TheTitleScreenShowsTheCredits()
    {
        var nav = new FakeNavigator();
        var menu = new MainMenu(nav);
        menu.Other().Single(b => b.Text == "Créditos").Press();
        Assert.True(menu.CreditsOpen);
        menu.Main()[1].Press();
        Assert.Equal("", nav.Last);
        menu.CloseWindows();
        Assert.False(menu.CreditsOpen);
        // «Lord of the Land» asks to be credited.
        Assert.Contains(Credits.Lines, l => l.Text.Contains("Lord of the Land") && l.Text.Contains("Attribution 4.0"));
        Assert.Contains("©", Credits.Copyright);
    }

    [Fact]
    public void ASavedGameCanBeLoadedAndDeletedAfterConfirming()
    {
        var session = GameSession.Create(world.Map, 1, seed: 7);
        string name = SaveFiles.Save(session, "test");
        try
        {
            var nav = new FakeNavigator();
            var menu = new LoadGameMenu(nav);
            var row = menu.Rows.First(r => r.Name == name);
            menu.Select(row.Save);
            Assert.True(menu.Rows.First(r => r.Name == name).Selected);
            menu.Buttons()[0].Press();
            Assert.Equal(name, nav.Loaded!.Name);

            menu.Buttons()[1].Press();
            Assert.Equal("¿Seguro? Borrar", menu.Buttons()[1].Text);
            menu.Buttons()[1].Press();
            Assert.DoesNotContain(menu.Rows, r => r.Name == name);
        }
        finally
        {
            foreach (var save in SaveFiles.List().Where(s => s.Name == name)) SaveFiles.Delete(save);
        }
    }

    [Fact]
    public void EscapeClosesTheTopmostThingAndThenOpensThePauseMenu()
    {
        var game = new GameController(GameSession.Create(world.Map, 1, seed: 7));
        game.HelpOpen = true;
        game.Escape();
        Assert.False(game.HelpOpen);
        Assert.True(game.HasSelection);
        game.Escape();
        Assert.False(game.HasSelection);
        Assert.False(game.TimeStopped);
        game.Escape();
        Assert.True(game.MenuOpen);
        Assert.True(game.TimeStopped);

        var nav = new FakeNavigator();
        var pause = game.PauseMenu(nav, "test");
        pause.First(b => b.Text == "Ayuda").Press();
        Assert.True(game.HelpOpen);
        Assert.False(game.MenuOpen);
        game.PauseMenu(nav, "test").First(b => b.Text == "Salir del juego").Press();
        Assert.Equal("quit", nav.Last);
    }
}
