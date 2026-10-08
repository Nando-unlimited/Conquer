using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Presentation;

/// <summary>Going from one screen to another; the client, which owns the screens, carries it out.</summary>
public interface IMenuNavigator
{
    void ShowMainMenu();
    void ShowNewGame();
    void ShowLoadGame();
    /// <summary>Makes the world and starts a game; the player plays <paramref name="country"/>, or one drawn at random when it is null.</summary>
    void StartNewGame(WorldSettings settings, int players, string? country);
    void LoadSavedGame(SaveFile save);
    void Quit();
}

/// <summary>
/// The title screen: carry on the latest game, start a new one, load a saved one, change the options, see the changelog
/// or the credits, or quit.
/// </summary>
public sealed class MainMenu(IMenuNavigator navigator)
{
    private readonly SaveFile? _latest = SaveFiles.List().FirstOrDefault();

    public bool ChangelogOpen { get; set; }
    public bool CreditsOpen { get; set; }

    /// <summary>The options, the changelog or the credits are open over the menu.</summary>
    private bool WindowOpen => ChangelogOpen || CreditsOpen || Settings.Open;

    /// <summary>Closes whichever window is open (Esc).</summary>
    public void CloseWindows() => ChangelogOpen = CreditsOpen = Settings.Open = false;

    /// <summary>The three big buttons on top: Continuar, Nueva partida and Cargar partida. They do nothing while a window is open.</summary>
    public IReadOnlyList<Button> Main() =>
    [
        new("Continuar", () => { if (!WindowOpen) navigator.LoadSavedGame(_latest!); }, _latest != null, Tooltip: _latest?.Name, Size: TextSize.Large),
        new("Nueva partida", () => { if (!WindowOpen) navigator.ShowNewGame(); }, Size: TextSize.Large),
        new("Cargar partida", () => { if (!WindowOpen) navigator.ShowLoadGame(); }, _latest != null, Size: TextSize.Large),
    ];

    public SettingsMenu Settings { get; } = new();

    /// <summary>The smaller ones below: the options, the changelog, the credits and Salir. They do nothing while a window is open.</summary>
    public IReadOnlyList<Button> Other() =>
    [
        new("Opciones", () => { if (!WindowOpen) Settings.Open = true; }, Tooltip: "Música, sonido, animaciones y aspecto del mapa."),
        new("Historial de versiones", () => { if (!WindowOpen) ChangelogOpen = true; }),
        new("Créditos", () => { if (!WindowOpen) CreditsOpen = true; }, Tooltip: "Quién hizo el juego, y la música, los sonidos y los gráficos que usa."),
        new("Salir", () => { if (!WindowOpen) navigator.Quit(); }),
    ];
}

/// <summary>A labelled line of the new-game screen: buttons, or a value between buttons that change it.</summary>
public sealed record OptionRow(string Label, bool Enabled, IReadOnlyList<Button> Buttons, string? Value = null, IReadOnlyList<Button>? After = null);

/// <summary>The new-game options: map, its size (random maps only), seed, number of players, the player's country and difficulty.</summary>
public sealed class NewGameMenu(IMenuNavigator navigator)
{
    private MapKind _kind = MapKind.Random;
    private int _seed = Random.Shared.Next(1, 100000);
    private int _players = 4;
    private Difficulty _difficulty = Difficulty.Normal;
    private MapSize _size = MapSize.Large;

    public OptionRow MapRow() => new("Mapa", true,
    [
        new Button("Aleatorio", () => _kind = MapKind.Random, Active: _kind == MapKind.Random),
        new Button("Tierra real", () => _kind = MapKind.Earth, Active: _kind == MapKind.Earth),
    ]);

    /// <summary>The Earth has one size; random worlds come smaller, with less land and bigger provinces.</summary>
    public OptionRow SizeRow()
    {
        bool randomMap = _kind == MapKind.Random;
        return new("Tamaño", randomMap, MapSizes.All.Select(size => new Button(size.Info().Name, () => _size = size, randomMap,
            randomMap ? _size == size : size == MapSize.Large, randomMap ? size.Info().Description : "La Tierra real tiene un solo tamaño.", TextSize.Small)).ToList());
    }

    public OptionRow SeedRow()
    {
        bool randomMap = _kind == MapKind.Random;
        return new("Semilla", randomMap, [new Button("-", () => _seed = Math.Max(1, _seed - 1), randomMap)], randomMap ? _seed.ToString() : "-",
            [new Button("+", () => _seed++, randomMap), new Button("Azar", () => _seed = Random.Shared.Next(1, 100000), randomMap)]);
    }

    /// <summary>Most nations the chosen map takes (the Earth is always large).</summary>
    private int MaxPlayers => (_kind == MapKind.Earth ? MapSize.Large : _size).Info().MaxNations;

    /// <summary>The nations chosen, fewer if the map chosen afterwards takes fewer.</summary>
    private int Players => Math.Min(_players, MaxPlayers);

    public OptionRow PlayersRow() => new("Jugadores", true, [new Button("-", () => _players = Players - 1, Players > 1)], Players.ToString(),
        [new Button("+", () => _players = Players + 1, Players < MaxPlayers, Tooltip: $"Hasta {MaxPlayers} naciones en este mapa.")]);

    /// <summary>The countries in alphabetical order; the player's is one of them, or none for one drawn at random.</summary>
    private static readonly IReadOnlyList<string> CountryList = [.. GameSession.CountryNames.OrderBy(TextFormat.SpanishSortKey, StringComparer.Ordinal)];

    /// <summary>Where the player's country is in <see cref="CountryList"/>; -1 draws it at random.</summary>
    private int _country = -1;

    /// <summary>The country chosen, or null for one drawn at random.</summary>
    public string? Country => _country >= 0 ? CountryList[_country] : null;

    /// <summary>The player's country, one step back or forward through the list (with «Al azar» before the first), or drawn at random.</summary>
    public OptionRow CountryRow() => new("País", true,
        [new Button("-", () => _country = _country < 0 ? CountryList.Count - 1 : _country - 1)], Country ?? "Al azar",
        [
            new Button("+", () => _country = _country == CountryList.Count - 1 ? -1 : _country + 1),
            new Button("Azar", () => _country = -1, _country >= 0, Tooltip: "Te toca un país al azar, como a tus rivales."),
        ]);

    public OptionRow DifficultyRow() => new("Dificultad", true, [new Button("-", () => _difficulty--, _difficulty > Difficulty.VeryEasy)],
        _difficulty.Info().Name, [new Button("+", () => _difficulty++, _difficulty < Difficulty.VeryHard)]);

    /// <summary>What the difficulty means and what the player starts with.</summary>
    public string Description
    {
        get
        {
            var difficulty = _difficulty.Info();
            double start = difficulty.StartingResources;
            return $"{difficulty.Description} Empiezas con {GameRules.StartingCitizens:N0} colonos, {GameRules.StartingFood * start:0} de comida, " +
                   $"{GameRules.StartingGold * start:0} de oro y {GameRules.StartingWood * start:0} de madera. Nadie posee tierra todavía.";
        }
    }

    /// <summary>The Earth map is fixed, but the seed still drives start positions, resources and rivals.</summary>
    public Button Start => new("Comenzar", () => navigator.StartNewGame(WorldSettings.New(_kind, _seed, _difficulty, _size), Players, Country), Size: TextSize.Large);

    public Button Back => new("Volver", navigator.ShowMainMenu);
}

/// <summary>A saved game in the list: its name, when it was saved and whether it is the one chosen.</summary>
public sealed record SaveRow(SaveFile Save, string Name, string When, bool Selected);

/// <summary>The saved games, newest first, to load or delete (deleting asks first).</summary>
public sealed class LoadGameMenu
{
    private readonly IMenuNavigator _navigator;
    private List<SaveFile> _saves = SaveFiles.List();
    private SaveFile? _selected;
    private bool _confirmDelete;

    public LoadGameMenu(IMenuNavigator navigator)
    {
        _navigator = navigator;
        _selected = _saves.FirstOrDefault();
    }

    public string? Empty => _saves.Count == 0 ? "No hay partidas guardadas." : null;

    public IReadOnlyList<SaveRow> Rows => _saves.Select(s => new SaveRow(s, s.Name, s.SavedAt.ToString("dd/MM/yyyy HH:mm"), s == _selected)).ToList();

    public void Select(SaveFile save)
    {
        _selected = save;
        _confirmDelete = false;
    }

    public IReadOnlyList<Button> Buttons() =>
    [
        new("Cargar", () => _navigator.LoadSavedGame(_selected!), _selected != null, Size: TextSize.Large),
        new(_confirmDelete ? "¿Seguro? Borrar" : "Borrar", () =>
        {
            if (!_confirmDelete) _confirmDelete = true;
            else
            {
                SaveFiles.Delete(_selected!);
                _saves = SaveFiles.List();
                _selected = _saves.FirstOrDefault();
                _confirmDelete = false;
            }
        }, _selected != null, Tooltip: "Borra la partida seleccionada"),
        new("Volver", _navigator.ShowMainMenu),
    ];
}

/// <summary>
/// Makes the world on a background thread, for a new game or a saved one, saying what it is doing. The client
/// prepares what it needs to draw the map (<typeparamref name="TPicture"/>) in the same thread.
/// </summary>
public sealed class LoadingJob<TPicture>
{
    private readonly Task<(GameSession Session, TPicture Picture)> _task;
    private readonly bool _loadingSave;
    private string _status = "Preparando...";

    public LoadingJob(WorldSettings settings, int players, string? country, Func<WorldMap, TPicture> prepare)
    {
        _task = Task.Run(() =>
        {
            var map = WorldGenerator.Generate(settings, s => _status = s);
            _status = "Pintando el mapa...";
            var picture = prepare(map);
            _status = "Repartiendo a los pueblos...";
            return (GameSession.Create(map, players, settings.Seed, country: country), picture);
        });
    }

    /// <summary>Makes the saved game's map again and puts the game back on it.</summary>
    public LoadingJob(SaveFile file, Func<WorldMap, TPicture> prepare)
    {
        _loadingSave = true;
        _task = Task.Run(() =>
        {
            _status = "Leyendo la partida...";
            var save = SaveFiles.Read(file.Path);
            var map = WorldGenerator.Generate(save.World, s => _status = s);
            _status = "Pintando el mapa...";
            var picture = prepare(map);
            _status = "Devolviendo a cada pueblo a su sitio...";
            return (GameSession.Load(map, save), picture);
        });
    }

    public bool LoadingSave => _loadingSave;

    /// <summary>The game and its picture once ready; null while it is still being made or if it failed.</summary>
    public (GameSession Session, TPicture Picture)? Result => _task.IsCompletedSuccessfully ? _task.Result : null;

    /// <summary>What went wrong, with its heading, or null.</summary>
    public (string Heading, string Message)? Error => _task.IsFaulted
        ? (_loadingSave ? "No se pudo cargar la partida:" : "Error al generar el mundo:", _task.Exception!.GetBaseException().Message)
        : null;

    /// <summary>What it is doing, with one to three dots that come and go with the seconds gone by.</summary>
    public string Status(double elapsed) => _status.TrimEnd('.') + new string('.', 1 + (int)(elapsed * 2) % 3);
}
