using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Client.Screens;

/// <summary>Carries out the menus' moves from one screen to another.</summary>
public sealed class MenuNavigator(ConquerApp app) : IMenuNavigator
{
    public void ShowMainMenu() => app.Show(new MainMenuScreen(app));
    public void ShowNewGame() => app.Show(new NewGameScreen(app));
    public void ShowLoadGame() => app.Show(new LoadGameScreen(app));
    public void StartNewGame(WorldSettings settings, int players) => app.Show(new LoadingScreen(app, settings, players));
    public void LoadSavedGame(SaveFile save) => app.Show(new LoadingScreen(app, save));
    public void Quit() => app.Quit();
}

/// <summary>Draws the title screen (<see cref="MainMenu"/>) over the moving map.</summary>
public sealed class MainMenuScreen(ConquerApp app) : IScreen
{
    private readonly ChangelogView _changelog = new();
    private readonly MainMenu _menu = new(new MenuNavigator(app));

    public void Frame(double dt)
    {
        var ui = app.Ui;
        var s = app.ScreenSize;
        MenuBackground.Draw(app.Gl, ui.Batch, s, dt);
        float cx = s.X / 2;

        ui.TextCentered(new Rect(3, s.Y * 0.12f + 3, s.X, 80), "CONQUER", Rgba.Black.WithAlpha(0.7f), FontSize.Title, bold: true);
        ui.TextCentered(new Rect(0, s.Y * 0.12f, s.X, 80), "CONQUER", Theme.Accent, FontSize.Title, bold: true);
        ui.TextCentered(new Rect(0, s.Y * 0.12f + 80, s.X, 30), $"Versión {ConquerApp.Version}", Theme.TextDim);

        float w = 320, x = cx - w / 2, y = s.Y * 0.12f + 150;
        ui.Panel(new Rect(x - 24, y - 24, w + 48, 60 * 3 + 16 + 50 + 40 + 48));
        foreach (var button in _menu.Main())
        {
            DocumentView.Press(ui, button, new Rect(x, y, w, 48));
            y += 60;
        }
        y += 16;
        foreach (var button in _menu.Other())
        {
            DocumentView.Press(ui, button, new Rect(x, y, w, 40));
            y += 50;
        }

        if (ui.Input.KeysPressed.Contains(Silk.NET.Input.Key.Escape)) _menu.ChangelogOpen = false;
        if (_menu.ChangelogOpen && _changelog.Frame(ui, new Rect(cx - 380, 60, 760, s.Y - 120))) _menu.ChangelogOpen = false;
    }

    public void Dispose() { }
}

/// <summary>Draws the new-game options (<see cref="NewGameMenu"/>).</summary>
public sealed class NewGameScreen(ConquerApp app) : IScreen
{
    private readonly NewGameMenu _menu = new(new MenuNavigator(app));

    public void Frame(double dt)
    {
        var ui = app.Ui;
        var s = app.ScreenSize;
        MenuBackground.Draw(app.Gl, ui.Batch, s, dt);
        float cx = s.X / 2;

        ui.TextCentered(new Rect(0, s.Y * 0.1f, s.X, 80), "CONQUER", Theme.Accent, FontSize.Title, bold: true);

        var panel = new Rect(cx - 230, s.Y * 0.1f + 110, 460, 446);
        ui.Panel(panel);
        float x = panel.X + 24, y = panel.Y + 20, w = panel.W - 48;
        ui.Text(x, y, "Nueva partida", Theme.Text, FontSize.Large, bold: true);
        y += 46;

        // The map kind: two buttons of their own widths.
        var map = _menu.MapRow();
        Label(ui, map, x, y);
        DocumentView.Press(ui, map.Buttons[0], new Rect(x + 120, y, 150, 32));
        DocumentView.Press(ui, map.Buttons[1], new Rect(x + 276, y, 136, 32));
        y += 46;

        var size = _menu.SizeRow();
        Label(ui, size, x, y);
        float sizeWidth = (w - 120 - 12) / size.Buttons.Count;
        for (int i = 0; i < size.Buttons.Count; i++) DocumentView.Press(ui, size.Buttons[i], new Rect(x + 120 + i * (sizeWidth + 6), y, sizeWidth, 32));
        y += 46;

        foreach (var row in new[] { _menu.SeedRow(), _menu.PlayersRow(), _menu.DifficultyRow() })
        {
            // A value between - and +, and for the seed a button for a random one.
            Label(ui, row, x, y);
            DocumentView.Press(ui, row.Buttons[0], new Rect(x + 120, y, 36, 32));
            ui.TextCentered(new Rect(x + 160, y, 110, 32), row.Value!, row.Enabled ? Theme.Text : Theme.TextDisabled);
            DocumentView.Press(ui, row.After![0], new Rect(x + 274, y, 36, 32));
            if (row.After.Count > 1) DocumentView.Press(ui, row.After[1], new Rect(x + 318, y, 94, 32));
            y += 46;
        }
        y -= 6;
        foreach (var line in ui.Font.Wrap(_menu.Description, w, FontSize.Small))
        {
            ui.Text(x, y, line, Theme.TextDim, FontSize.Small);
            y += ui.Font.LineHeight(FontSize.Small);
        }

        DocumentView.Press(ui, _menu.Start, new Rect(x, panel.Bottom - 64, w, 44));
        var back = _menu.Back;
        DocumentView.Press(ui, back, new Rect(cx - 230, panel.Bottom + 20, 460, 40));
        if (ui.Input.KeysPressed.Contains(Silk.NET.Input.Key.Escape)) back.Press();
    }

    private static void Label(Ui ui, OptionRow row, float x, float y) =>
        ui.Text(x, y + 6, row.Label, row.Enabled ? Theme.TextDim : Theme.TextDisabled);

    public void Dispose() { }
}

/// <summary>Draws the saved games (<see cref="LoadGameMenu"/>), scrolling with the wheel.</summary>
public sealed class LoadGameScreen(ConquerApp app) : IScreen
{
    private const float RowHeight = 44;
    private readonly LoadGameMenu _menu = new(new MenuNavigator(app));
    private float _scroll;

    public void Frame(double dt)
    {
        var ui = app.Ui;
        var s = app.ScreenSize;
        MenuBackground.Draw(app.Gl, ui.Batch, s, dt);
        float cx = s.X / 2;

        var panel = new Rect(cx - 340, 60, 680, s.Y - 120);
        ui.Panel(panel);
        ui.Text(panel.X + 24, panel.Y + 20, "Cargar partida", Theme.Text, FontSize.Large, bold: true);

        var list = new Rect(panel.X + 24, panel.Y + 66, panel.W - 48, panel.H - 66 - 80);
        var rows = _menu.Rows;
        if (_menu.Empty != null) ui.Text(list.X, list.Y, _menu.Empty, Theme.TextDim);
        if (ui.Hover(list)) _scroll -= ui.Input.Scroll * RowHeight * 2;
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, rows.Count * RowHeight - list.H));

        int first = (int)(_scroll / RowHeight);
        for (int i = first; i < rows.Count; i++)
        {
            float y = list.Y + i * RowHeight - _scroll;
            if (y + RowHeight > list.Bottom + 1) break;
            var save = rows[i];
            var row = new Rect(list.X, y, list.W, RowHeight - 4);
            if (save.Selected || ui.Hover(row)) ui.Batch.Rect(row.X, row.Y, row.W, row.H, save.Selected ? Theme.ButtonActive.WithAlpha(0.5f) : Theme.ButtonHover);
            ui.Text(row.X + 12, row.Y + 10, save.Name, Theme.Text);
            ui.Text(row.Right - 12 - ui.Font.Measure(save.When, FontSize.Small), row.Y + 12, save.When, Theme.TextDim, FontSize.Small);
            if (ui.Hover(row) && ui.Input.LeftPressed) _menu.Select(save.Save);
        }

        float by = panel.Bottom - 64, bw = (panel.W - 48 - 24) / 3;
        var buttons = _menu.Buttons();
        for (int i = 0; i < buttons.Count; i++) DocumentView.Press(ui, buttons[i], new Rect(panel.X + 24 + i * (bw + 12), by, bw, 44));
        if (ui.Input.KeysPressed.Contains(Silk.NET.Input.Key.Escape)) buttons[^1].Press();
    }

    public void Dispose() { }
}

/// <summary>Shows the progress of a <see cref="LoadingJob{TPicture}"/> and opens the game when it is ready.</summary>
public sealed class LoadingScreen : IScreen
{
    private readonly ConquerApp _app;
    private readonly LoadingJob<MapRenderer.Prepared> _job;
    private double _elapsed;

    public LoadingScreen(ConquerApp app, WorldSettings settings, int players)
    {
        _app = app;
        _job = new LoadingJob<MapRenderer.Prepared>(settings, players, MapRenderer.Prepare);
    }

    public LoadingScreen(ConquerApp app, SaveFile file)
    {
        _app = app;
        _job = new LoadingJob<MapRenderer.Prepared>(file, MapRenderer.Prepare);
    }

    public void Frame(double dt)
    {
        _elapsed += dt;
        var ui = _app.Ui;
        var s = _app.ScreenSize;
        MenuBackground.Draw(_app.Gl, ui.Batch, s, dt);
        ui.TextCentered(new Rect(0, s.Y / 2 - 80, s.X, 80), "CONQUER", Theme.Accent, FontSize.Title, bold: true);

        if (_job.Error is var (heading, message))
        {
            ui.TextCentered(new Rect(0, s.Y / 2 + 10, s.X, 30), heading, Theme.Bad);
            ui.TextCentered(new Rect(0, s.Y / 2 + 40, s.X, 30), message, Theme.TextDim);
            if (ui.Button(new Rect(s.X / 2 - 100, s.Y / 2 + 90, 200, 40), "Volver")) _app.Show(new MainMenuScreen(_app));
            return;
        }
        if (_job.Result is var (session, pixels))
        {
            _app.Show(new GameScreen(_app, session, pixels, loaded: _job.LoadingSave));
            return;
        }
        ui.TextCentered(new Rect(0, s.Y / 2 + 10, s.X, 30), _job.Status(_elapsed), Theme.Text, FontSize.Large);
    }

    public void Dispose() { }
}
