using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Client.Screens;

/// <summary>Title screen with the new-game options.</summary>
public sealed class MainMenuScreen : IScreen
{
    private readonly ConquerApp _app;
    private readonly ChangelogView _changelog = new();
    private MapKind _kind = MapKind.Random;
    private int _seed = Random.Shared.Next(1, 100000);
    private int _players = 4;

    public MainMenuScreen(ConquerApp app) => _app = app;

    public void Frame(double dt)
    {
        var ui = _app.Ui;
        var s = _app.ScreenSize;
        float cx = s.X / 2;

        ui.TextCentered(new Rect(0, s.Y * 0.1f, s.X, 80), "CONQUER", Theme.Accent, FontSize.Title, bold: true);
        ui.TextCentered(new Rect(0, s.Y * 0.1f + 80, s.X, 30), $"Versión {ConquerApp.Version}", Theme.TextDim);

        var panel = new Rect(cx - 230, s.Y * 0.1f + 130, 460, 320);
        ui.Panel(panel);
        float x = panel.X + 24, y = panel.Y + 20, w = panel.W - 48;
        ui.Text(x, y, "Nueva partida", Theme.Text, FontSize.Large, bold: true);
        y += 46;

        ui.Text(x, y + 6, "Mapa", Theme.TextDim);
        if (ui.Button(new Rect(x + 120, y, 150, 32), "Aleatorio", active: _kind == MapKind.Random)) _kind = MapKind.Random;
        if (ui.Button(new Rect(x + 276, y, 136, 32), "Tierra real", active: _kind == MapKind.Earth)) _kind = MapKind.Earth;
        y += 46;

        bool randomMap = _kind == MapKind.Random;
        ui.Text(x, y + 6, "Semilla", randomMap ? Theme.TextDim : Theme.TextDisabled);
        if (ui.Button(new Rect(x + 120, y, 36, 32), "-", randomMap)) _seed = Math.Max(1, _seed - 1);
        ui.TextCentered(new Rect(x + 160, y, 110, 32), randomMap ? _seed.ToString() : "-", randomMap ? Theme.Text : Theme.TextDisabled);
        if (ui.Button(new Rect(x + 274, y, 36, 32), "+", randomMap)) _seed++;
        if (ui.Button(new Rect(x + 318, y, 94, 32), "Azar", randomMap)) _seed = Random.Shared.Next(1, 100000);
        y += 46;

        ui.Text(x, y + 6, "Jugadores", Theme.TextDim);
        if (ui.Button(new Rect(x + 120, y, 36, 32), "-", _players > 1)) _players--;
        ui.TextCentered(new Rect(x + 160, y, 110, 32), _players.ToString());
        if (ui.Button(new Rect(x + 274, y, 36, 32), "+", _players < 8)) _players++;
        y += 40;
        foreach (var line in ui.Font.Wrap("Tú y cada rival empezáis con 300 colonos, 600 de comida, 50 de oro y 100 de madera. Nadie posee tierra todavía.", w, FontSize.Small))
        {
            ui.Text(x, y, line, Theme.TextDim, FontSize.Small);
            y += ui.Font.LineHeight(FontSize.Small);
        }

        if (ui.Button(new Rect(x, panel.Bottom - 64, w, 44), "Comenzar", size: FontSize.Large) && !_changelog.Visible)
        {
            // The Earth map is fixed, but the seed still drives start positions, resources and rivals.
            _app.Show(new LoadingScreen(_app, new WorldSettings(_kind, _seed), _players));
        }

        float by = panel.Bottom + 20;
        if (ui.Button(new Rect(cx - 230, by, 225, 40), "Historial de versiones")) _changelog.Visible = true;
        if (ui.Button(new Rect(cx + 5, by, 225, 40), "Salir")) _app.Quit();

        _changelog.Frame(ui, new Rect(cx - 380, 60, 760, s.Y - 120));
    }

    public void Dispose() { }
}

/// <summary>Generates the world on a background thread while showing progress.</summary>
public sealed class LoadingScreen : IScreen
{
    private readonly ConquerApp _app;
    private readonly Task<(GameSession Session, MapRenderer.Prepared Pixels)> _task;
    private string _status = "Preparando...";
    private double _elapsed;

    public LoadingScreen(ConquerApp app, WorldSettings settings, int players)
    {
        _app = app;
        _task = Task.Run(() =>
        {
            var map = WorldGenerator.Generate(settings, s => _status = s);
            _status = "Pintando el mapa...";
            var pixels = MapRenderer.Prepare(map);
            _status = "Repartiendo a los pueblos...";
            return (GameSession.Create(map, players, settings.Seed), pixels);
        });
    }

    public void Frame(double dt)
    {
        _elapsed += dt;
        var ui = _app.Ui;
        var s = _app.ScreenSize;
        ui.TextCentered(new Rect(0, s.Y / 2 - 80, s.X, 80), "CONQUER", Theme.Accent, FontSize.Title, bold: true);

        if (_task.IsFaulted)
        {
            ui.TextCentered(new Rect(0, s.Y / 2 + 10, s.X, 30), "Error al generar el mundo:", Theme.Bad);
            ui.TextCentered(new Rect(0, s.Y / 2 + 40, s.X, 30), _task.Exception!.GetBaseException().Message, Theme.TextDim);
            if (ui.Button(new Rect(s.X / 2 - 100, s.Y / 2 + 90, 200, 40), "Volver")) _app.Show(new MainMenuScreen(_app));
            return;
        }
        if (_task.IsCompletedSuccessfully)
        {
            var (session, pixels) = _task.Result;
            _app.Show(new GameScreen(_app, session, pixels));
            return;
        }

        string dots = new('.', 1 + (int)(_elapsed * 2) % 3);
        ui.TextCentered(new Rect(0, s.Y / 2 + 10, s.X, 30), _status.TrimEnd('.') + dots, Theme.Text, FontSize.Large);
    }

    public void Dispose() { }
}
