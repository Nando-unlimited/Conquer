using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Client.Screens;

/// <summary>Title screen: carry on the latest game, start a new one, load a saved one, or quit.</summary>
public sealed class MainMenuScreen : IScreen
{
    private readonly ConquerApp _app;
    private readonly ChangelogView _changelog = new();
    private readonly SaveFile? _latest = SaveFiles.List().FirstOrDefault();

    public MainMenuScreen(ConquerApp app) => _app = app;

    public void Frame(double dt)
    {
        var ui = _app.Ui;
        var s = _app.ScreenSize;
        MenuBackground.Draw(_app.Gl, ui.Batch, s, dt);
        float cx = s.X / 2;

        ui.TextCentered(new Rect(3, s.Y * 0.12f + 3, s.X, 80), "CONQUER", Rgba.Black.WithAlpha(0.7f), FontSize.Title, bold: true);
        ui.TextCentered(new Rect(0, s.Y * 0.12f, s.X, 80), "CONQUER", Theme.Accent, FontSize.Title, bold: true);
        ui.TextCentered(new Rect(0, s.Y * 0.12f + 80, s.X, 30), $"Versión {ConquerApp.Version}", Theme.TextDim);

        float w = 320, x = cx - w / 2, y = s.Y * 0.12f + 150;
        ui.Panel(new Rect(x - 24, y - 24, w + 48, 60 * 3 + 16 + 50 + 40 + 48));
        bool free = !_changelog.Visible;
        if (ui.Button(new Rect(x, y, w, 48), "Continuar", _latest != null, tooltip: _latest?.Name, size: FontSize.Large) && free)
            _app.Show(new LoadingScreen(_app, _latest!));
        y += 60;
        if (ui.Button(new Rect(x, y, w, 48), "Nueva partida", size: FontSize.Large) && free) _app.Show(new NewGameScreen(_app));
        y += 60;
        if (ui.Button(new Rect(x, y, w, 48), "Cargar partida", _latest != null, size: FontSize.Large) && free) _app.Show(new LoadGameScreen(_app));
        y += 76;
        if (ui.Button(new Rect(x, y, w, 40), "Historial de versiones")) _changelog.Visible = true;
        y += 50;
        if (ui.Button(new Rect(x, y, w, 40), "Salir") && free) _app.Quit();

        if (ui.Input.KeysPressed.Contains(Silk.NET.Input.Key.Escape)) _changelog.Visible = false;
        _changelog.Frame(ui, new Rect(cx - 380, 60, 760, s.Y - 120));
    }

    public void Dispose() { }
}

/// <summary>The new-game options: map, seed, number of players and difficulty.</summary>
public sealed class NewGameScreen : IScreen
{
    private readonly ConquerApp _app;
    private MapKind _kind = MapKind.Random;
    private int _seed = Random.Shared.Next(1, 100000);
    private int _players = 4;
    private Difficulty _difficulty = Difficulty.Normal;

    public NewGameScreen(ConquerApp app) => _app = app;

    public void Frame(double dt)
    {
        var ui = _app.Ui;
        var s = _app.ScreenSize;
        MenuBackground.Draw(_app.Gl, ui.Batch, s, dt);
        float cx = s.X / 2;

        ui.TextCentered(new Rect(0, s.Y * 0.1f, s.X, 80), "CONQUER", Theme.Accent, FontSize.Title, bold: true);

        var panel = new Rect(cx - 230, s.Y * 0.1f + 110, 460, 400);
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
        y += 46;

        var difficulty = _difficulty.Info();
        ui.Text(x, y + 6, "Dificultad", Theme.TextDim);
        if (ui.Button(new Rect(x + 120, y, 36, 32), "-", _difficulty > Difficulty.VeryEasy)) _difficulty--;
        ui.TextCentered(new Rect(x + 160, y, 110, 32), difficulty.Name);
        if (ui.Button(new Rect(x + 274, y, 36, 32), "+", _difficulty < Difficulty.VeryHard)) _difficulty++;
        y += 40;
        double start = difficulty.StartingResources;
        string text = $"{difficulty.Description} Empiezas con {GameRules.StartingCitizens:N0} colonos, {GameRules.StartingFood * start:0} de comida, " +
                      $"{GameRules.StartingGold * start:0} de oro y {GameRules.StartingWood * start:0} de madera. Nadie posee tierra todavía.";
        foreach (var line in ui.Font.Wrap(text, w, FontSize.Small))
        {
            ui.Text(x, y, line, Theme.TextDim, FontSize.Small);
            y += ui.Font.LineHeight(FontSize.Small);
        }

        // The Earth map is fixed, but the seed still drives start positions, resources and rivals.
        if (ui.Button(new Rect(x, panel.Bottom - 64, w, 44), "Comenzar", size: FontSize.Large))
            _app.Show(new LoadingScreen(_app, new WorldSettings(_kind, _seed, Difficulty: _difficulty, Generator: WorldGenerator.LatestGenerator), _players));

        if (ui.Button(new Rect(cx - 230, panel.Bottom + 20, 460, 40), "Volver") || ui.Input.KeysPressed.Contains(Silk.NET.Input.Key.Escape))
            _app.Show(new MainMenuScreen(_app));
    }

    public void Dispose() { }
}

/// <summary>The saved games, newest first, to load or delete.</summary>
public sealed class LoadGameScreen : IScreen
{
    private const float RowHeight = 44;
    private readonly ConquerApp _app;
    private List<SaveFile> _saves = SaveFiles.List();
    private SaveFile? _selected;
    private bool _confirmDelete;
    private float _scroll;

    public LoadGameScreen(ConquerApp app)
    {
        _app = app;
        _selected = _saves.FirstOrDefault();
    }

    public void Frame(double dt)
    {
        var ui = _app.Ui;
        var s = _app.ScreenSize;
        MenuBackground.Draw(_app.Gl, ui.Batch, s, dt);
        float cx = s.X / 2;

        var panel = new Rect(cx - 340, 60, 680, s.Y - 120);
        ui.Panel(panel);
        ui.Text(panel.X + 24, panel.Y + 20, "Cargar partida", Theme.Text, FontSize.Large, bold: true);

        var list = new Rect(panel.X + 24, panel.Y + 66, panel.W - 48, panel.H - 66 - 80);
        if (_saves.Count == 0) ui.Text(list.X, list.Y, "No hay partidas guardadas.", Theme.TextDim);
        if (ui.Hover(list)) _scroll -= ui.Input.Scroll * RowHeight * 2;
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _saves.Count * RowHeight - list.H));

        int first = (int)(_scroll / RowHeight);
        for (int i = first; i < _saves.Count; i++)
        {
            float y = list.Y + i * RowHeight - _scroll;
            if (y + RowHeight > list.Bottom + 1) break;
            var save = _saves[i];
            var row = new Rect(list.X, y, list.W, RowHeight - 4);
            bool selected = save == _selected;
            if (selected || ui.Hover(row)) ui.Batch.Rect(row.X, row.Y, row.W, row.H, selected ? Theme.ButtonActive.WithAlpha(0.5f) : Theme.ButtonHover);
            ui.Text(row.X + 12, row.Y + 10, save.Name, Theme.Text);
            string when = save.SavedAt.ToString("dd/MM/yyyy HH:mm");
            ui.Text(row.Right - 12 - ui.Font.Measure(when, FontSize.Small), row.Y + 12, when, Theme.TextDim, FontSize.Small);
            if (ui.Hover(row) && ui.Input.LeftPressed)
            {
                _selected = save;
                _confirmDelete = false;
            }
        }

        float by = panel.Bottom - 64, bw = (panel.W - 48 - 24) / 3;
        if (ui.Button(new Rect(panel.X + 24, by, bw, 44), "Cargar", _selected != null, size: FontSize.Large))
            _app.Show(new LoadingScreen(_app, _selected!));
        string deleteLabel = _confirmDelete ? "¿Seguro? Borrar" : "Borrar";
        if (ui.Button(new Rect(panel.X + 24 + bw + 12, by, bw, 44), deleteLabel, _selected != null, tooltip: "Borra la partida seleccionada"))
        {
            if (!_confirmDelete) _confirmDelete = true;
            else
            {
                SaveFiles.Delete(_selected!);
                _saves = SaveFiles.List();
                _selected = _saves.FirstOrDefault();
                _confirmDelete = false;
            }
        }
        if (ui.Button(new Rect(panel.X + 24 + 2 * (bw + 12), by, bw, 44), "Volver") || ui.Input.KeysPressed.Contains(Silk.NET.Input.Key.Escape))
            _app.Show(new MainMenuScreen(_app));
    }

    public void Dispose() { }
}

/// <summary>Generates the world on a background thread while showing progress, for a new game or a saved one.</summary>
public sealed class LoadingScreen : IScreen
{
    private readonly ConquerApp _app;
    private readonly Task<(GameSession Session, MapRenderer.Prepared Pixels)> _task;
    private readonly bool _loadingSave;
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

    /// <summary>Makes the saved game's map again and puts the game back on it.</summary>
    public LoadingScreen(ConquerApp app, SaveFile file)
    {
        _app = app;
        _loadingSave = true;
        _task = Task.Run(() =>
        {
            _status = "Leyendo la partida...";
            var save = SaveFiles.Read(file.Path);
            var map = WorldGenerator.Generate(save.World, s => _status = s);
            _status = "Pintando el mapa...";
            var pixels = MapRenderer.Prepare(map);
            _status = "Devolviendo a cada pueblo a su sitio...";
            return (GameSession.Load(map, save), pixels);
        });
    }

    public void Frame(double dt)
    {
        _elapsed += dt;
        var ui = _app.Ui;
        var s = _app.ScreenSize;
        MenuBackground.Draw(_app.Gl, ui.Batch, s, dt);
        ui.TextCentered(new Rect(0, s.Y / 2 - 80, s.X, 80), "CONQUER", Theme.Accent, FontSize.Title, bold: true);

        if (_task.IsFaulted)
        {
            ui.TextCentered(new Rect(0, s.Y / 2 + 10, s.X, 30), _loadingSave ? "No se pudo cargar la partida:" : "Error al generar el mundo:", Theme.Bad);
            ui.TextCentered(new Rect(0, s.Y / 2 + 40, s.X, 30), _task.Exception!.GetBaseException().Message, Theme.TextDim);
            if (ui.Button(new Rect(s.X / 2 - 100, s.Y / 2 + 90, 200, 40), "Volver")) _app.Show(new MainMenuScreen(_app));
            return;
        }
        if (_task.IsCompletedSuccessfully)
        {
            var (session, pixels) = _task.Result;
            _app.Show(new GameScreen(_app, session, pixels, loaded: _loadingSave));
            return;
        }

        string dots = new('.', 1 + (int)(_elapsed * 2) % 3);
        ui.TextCentered(new Rect(0, s.Y / 2 + 10, s.X, 30), _status.TrimEnd('.') + dots, Theme.Text, FontSize.Large);
    }

    public void Dispose() { }
}
