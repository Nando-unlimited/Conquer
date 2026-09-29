using System.Numerics;
using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Military;
using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Silk.NET.Input;

namespace Conquer.Client.Screens;

public sealed partial class GameScreen : IScreen
{
    /// <summary>Game hours per real second at each speed; index 0 is paused.</summary>
    private static readonly double[] HoursPerSecond = [0, 1, 4, 12, 48, 168];
    private const float TopBarHeight = 56;
    private const float SidePanelWidth = 340;

    private readonly ConquerApp _app;
    private readonly GameSession _session;
    private readonly MapRenderer _renderer;
    private readonly Camera _camera;
    private readonly ChangelogView _changelog = new();
    private readonly NationView _nation;
    private readonly List<(int UnitId, Rect Bounds)> _unitHitBoxes = [];
    private readonly List<(string Text, double Time, bool Ok)> _messages = [];

    private int _speed = 1, _lastSpeed = 1;
    private double _hourAccumulator, _realTime;
    private bool _mapDirty = true, _menuOpen, _dragging;
    private long _lastRefreshDay = -1;
    private int _seenNotifications;
    /// <summary>The city being named: founded by settlers (UnitId) or built by a province's citizens; null when no dialog is open.</summary>
    private (int? UnitId, int ProvinceId)? _naming;
    private string _cityName = "";
    private int? _selectedUnitId;
    private int _selectedProvince = -1, _hoverProvince = -1;
    private bool _choosingMigrationTarget;
    private int _migrationAmount = 50;
    /// <summary>Which tab the province panel shows.</summary>
    private ProvinceTab _provinceTab;

    private Ui Ui => _app.Ui;
    private Batch2D Batch => _app.Batch;
    private WorldMap Map => _session.Map;
    private Player Human => _session.Human;

    /// <param name="loaded">A saved game being carried on: it opens on the player's capital and does not repeat old messages.</param>
    public GameScreen(ConquerApp app, GameSession session, MapRenderer.Prepared pixels, bool loaded = false)
    {
        _app = app;
        _session = session;
        _renderer = new MapRenderer(app.Gl, session.Map, pixels);
        _camera = new Camera(session.Map.Width, session.Map.Height) { Screen = app.ScreenSize };
        session.OwnershipChanged += _ => _mapDirty = true;
        _nation = new NationView(session, session.Human, ViewProvince, ViewUnit, Show);
        _renderer.IsResourceKnown = session.Human.Knows;

        if (loaded)
        {
            _seenNotifications = session.Notifications.Count;
            _camera.LookAt(_camera.Center, zoom: 8);
            CenterOnHome();
            _messages.Add(("Partida cargada.", 0, true));
            return;
        }
        var settlers = session.Units.First(u => u.OwnerId == GameSession.HumanPlayerId);
        _selectedUnitId = settlers.Id;
        _camera.LookAt(Center(settlers.ProvinceId), zoom: 8);
        ApplyTestOptions(app.Options, settlers);
    }

    /// <summary>Command-line shortcuts for testing: found the capital, fast-forward and set the view.</summary>
    private void ApplyTestOptions(StartOptions options, Unit settlers)
    {
        if (options.Days > 0)
        {
            int capitalProvince = settlers.ProvinceId;
            _session.FoundCity(Human.Id, settlers.Id);
            for (int h = 0; h < options.Days * 24; h++) _session.Step();
            _selectedUnitId = null;
            _selectedProvince = capitalProvince;
        }
        if (options.Zoom is float zoom) _camera.LookAt(_camera.Center, zoom);
        if (Enum.TryParse<MapMode>(options.Mode, ignoreCase: true, out var mode)) _renderer.Mode = mode;
        if (Enum.TryParse<NationTab>(options.Nation, ignoreCase: true, out var tab)) { _nation.Tab = tab; _nation.Visible = true; }
        _provinceTab = options.Panel switch { "buildings" => ProvinceTab.Buildings, "army" => ProvinceTab.Army, _ => ProvinceTab.General };
        if (options.Panel == "regiment" && Human.CapitalCityId is int capital) ShowSampleArmy(capital);
        if (options.Panel == "found" && _session.UnitById(settlers.Id) != null) OpenCityNaming(settlers.Id, settlers.ProvinceId);
    }

    /// <summary>
    /// For <c>--panel regiment</c>: trains three battalions and a corps HQ in the capital, merges the
    /// battalions into one regiment under the corps and selects it.
    /// </summary>
    private void ShowSampleArmy(int capitalId)
    {
        var city = _session.CityById(capitalId)!;
        Map.Provinces[city.ProvinceId].Population += 1000;
        foreach (var r in new[] { ResourceType.Wood, ResourceType.Gold }) Human.Stockpile[r] += 1000;
        Human.Learn(Tech.Archery);
        foreach (var type in new[] { BattalionType.Warriors, BattalionType.Archers, BattalionType.Warriors }) _session.Train(Human.Id, capitalId, type);
        _session.RaiseHeadquarters(Human.Id, capitalId, 1);
        for (int h = 0; h < 24 * 25; h++) _session.Step();
        var regiments = _session.Units.Where(u => u.OwnerId == Human.Id && u.IsMilitary).ToList();
        foreach (var other in regiments.Skip(1)) _session.Merge(Human.Id, regiments[0].Id, other.Id);
        if (_session.Units.FirstOrDefault(u => u.OwnerId == Human.Id && u.IsHeadquarters) is { } corps) _session.Attach(Human.Id, regiments[0].Id, corps.Id);
        _selectedUnitId = regiments[0].Id;
        _selectedProvince = -1;
    }

    public void Frame(double dt)
    {
        _realTime += dt;
        _camera.Screen = _app.ScreenSize;
        HandleKeys(dt);
        AdvanceTime(dt);
        CollectNotifications();

        if (_mapDirty || (_renderer.Mode >= MapMode.Population &&_session.Date.Days != _lastRefreshDay))
        {
            _renderer.Refresh(_session);
            _mapDirty = false;
            _lastRefreshDay = _session.Date.Days;
        }

        var mouseMap = _camera.ScreenToMap(Ui.Input.Mouse);
        _hoverProvince = MapRenderer.ProvinceAt(Map, mouseMap, _camera.Zoom);

        _renderer.Draw(_camera, _selectedProvince, _hoverProvince, _app.PixelScale);
        DrawRivers();
        DrawCities();
        DrawMigrations();
        DrawUnits();
        DrawBattles();

        DrawTopBar();
        if (!_nation.Visible) DrawSidePanel();
        DrawBottomBar();
        _nation.Frame(Ui, NationRect);
        DrawMessages();
        if (_naming.HasValue) DrawCityNaming();
        if (_menuOpen) DrawPauseMenu();
        _changelog.Frame(Ui, new Rect(_app.ScreenSize.X / 2 - 380, 70, 760, _app.ScreenSize.Y - 140));

        bool modal = _menuOpen || _naming.HasValue;
        if (!modal && !_changelog.Visible && !_nation.Visible) HandleMapMouse();
        if (!Ui.MouseOverUi && !modal && !_nation.Visible && _hoverProvince >= 0 && !_dragging) HoverTooltip();
    }

    // ------------------------------------------------------------------ time and input

    private void AdvanceTime(double dt)
    {
        if (_menuOpen || _changelog.Visible || _naming.HasValue || _speed == 0) return;
        _hourAccumulator += dt * HoursPerSecond[_speed];
        int steps = Math.Min((int)_hourAccumulator, 400);
        _hourAccumulator -= steps;
        if (_hourAccumulator > 4) _hourAccumulator = 0; // don't build up a backlog when the machine can't keep up
        for (int i = 0; i < steps; i++) _session.Step();
    }

    private void SetSpeed(int speed)
    {
        if (speed > 0) _lastSpeed = speed;
        _speed = speed;
    }

    private void HandleKeys(double dt)
    {
        var input = Ui.Input;
        if (_naming.HasValue)
        {
            // While the city's name is being typed, keys belong to the text field.
            if (input.KeysPressed.Contains(Key.Escape)) _naming = null;
            else if (input.KeysPressed.Contains(Key.Enter) || input.KeysPressed.Contains(Key.KeypadEnter)) ConfirmCityName();
            return;
        }
        foreach (var key in input.KeysPressed)
        {
            switch (key)
            {
                case Key.Escape:
                    if (_changelog.Visible) _changelog.Visible = false;
                    else if (_nation.Visible) _nation.Visible = false;
                    else if (_choosingMigrationTarget) _choosingMigrationTarget = false;
                    else if (_selectedUnitId.HasValue || _selectedProvince >= 0) { _selectedUnitId = null; _selectedProvince = -1; }
                    else _menuOpen = !_menuOpen;
                    break;
                case Key.Space: SetSpeed(_speed == 0 ? _lastSpeed : 0); break;
                case >= Key.Number1 and <= Key.Number5: SetSpeed(key - Key.Number1 + 1); break;
                case Key.Tab: _renderer.Mode = (MapMode)(((int)_renderer.Mode + 1) % Enum.GetValues<MapMode>().Length); _mapDirty = true; break;
                case Key.Home: CenterOnHome(); break;
                case Key.N when !_menuOpen: _nation.Visible = !_nation.Visible; break;
                case Key.KeypadAdd or Key.Equal: _camera.ZoomAt(_camera.Screen / 2, 1.25f); break;
                case Key.KeypadSubtract or Key.Minus: _camera.ZoomAt(_camera.Screen / 2, 0.8f); break;
            }
        }

        if (_menuOpen) return;
        var pan = Vector2.Zero;
        if (input.KeysDown.Contains(Key.A) || input.KeysDown.Contains(Key.Left)) pan.X += 1;
        if (input.KeysDown.Contains(Key.D) || input.KeysDown.Contains(Key.Right)) pan.X -= 1;
        if (input.KeysDown.Contains(Key.W) || input.KeysDown.Contains(Key.Up)) pan.Y += 1;
        if (input.KeysDown.Contains(Key.S) || input.KeysDown.Contains(Key.Down)) pan.Y -= 1;
        if (pan != Vector2.Zero) _camera.Pan(pan * (float)(900 * dt));
    }

    private void HandleMapMouse()
    {
        var input = Ui.Input;
        bool overUi = Ui.MouseOverUi;

        if (!overUi && input.Scroll != 0) _camera.ZoomAt(input.Mouse, MathF.Pow(1.2f, input.Scroll));
        if (input.LeftPressed) _pressStartedOnUi = overUi;

        if (input.LeftDown && !_pressStartedOnUi && (_dragging || Vector2.Distance(input.Mouse, input.LeftPressPosition) > 5))
        {
            if (_dragging) _camera.Pan(input.Mouse - _lastMouse);
            _dragging = true;
        }
        _lastMouse = input.Mouse;

        if (input.LeftReleased)
        {
            bool wasDrag = _dragging;
            _dragging = false;
            if (!wasDrag && !overUi) LeftClick();
        }
        if (input.RightPressed && !overUi) RightClick();
    }

    private Vector2 _lastMouse;
    private bool _pressStartedOnUi;

    private Rect TopBarRect => new(0, 0, _app.ScreenSize.X, TopBarHeight);

    private void LeftClick()
    {
        if (_choosingMigrationTarget)
        {
            _choosingMigrationTarget = false;
            if (_hoverProvince >= 0 && _selectedProvince >= 0)
                Show(_session.ForceMigration(Human.Id, _selectedProvince, _hoverProvince, _migrationAmount));
            return;
        }

        var hit = _unitHitBoxes.LastOrDefault(h => h.Bounds.Contains(Ui.Input.Mouse));
        if (hit.Bounds.W > 0)
        {
            _selectedUnitId = hit.UnitId;
            _selectedProvince = -1;
            return;
        }
        _selectedUnitId = null;
        _selectedProvince = _hoverProvince;
    }

    private void RightClick()
    {
        if (_selectedUnitId is not int id || _session.UnitById(id) is not { } unit || unit.OwnerId != Human.Id || _hoverProvince < 0) return;
        var result = _session.MoveUnit(Human.Id, unit.Id, _hoverProvince);
        Show(result);
    }

    private Rect NationRect => new(Math.Max(8, _app.ScreenSize.X / 2 - 540), TopBarHeight + 12, Math.Min(1080, _app.ScreenSize.X - 16), _app.ScreenSize.Y - TopBarHeight - 76);

    /// <summary>Selects a province and centres the map on it (from the nation screen).</summary>
    /// <summary>Selects a unit and centres the map on it (from the nation screen).</summary>
    private void ViewUnit(int unitId)
    {
        if (_session.UnitById(unitId) is not { } unit) return;
        _selectedUnitId = unit.Id;
        _selectedProvince = -1;
        _choosingMigrationTarget = false;
        _camera.LookAt(Center(unit.ProvinceId));
    }

    private void ViewProvince(int provinceId)
    {
        _selectedUnitId = null;
        _selectedProvince = provinceId;
        _choosingMigrationTarget = false;
        _camera.LookAt(Center(provinceId));
    }

    private void CenterOnHome()
    {
        if (Human.CapitalCityId is int capital && _session.CityById(capital) is { } city) _camera.LookAt(Center(city.ProvinceId));
        else if (_session.Units.FirstOrDefault(u => u.OwnerId == Human.Id) is { } unit) _camera.LookAt(Center(unit.ProvinceId));
    }

    private void Show(CommandResult result)
    {
        if (!string.IsNullOrEmpty(result.Message)) _messages.Add((result.Message, _realTime, result.Ok));
    }

    private void CollectNotifications()
    {
        for (; _seenNotifications < _session.Notifications.Count; _seenNotifications++)
        {
            var n = _session.Notifications[_seenNotifications];
            if (n.PlayerId == Human.Id || n.PlayerId < 0) _messages.Add((n.Text, _realTime, true));
        }
    }

    // ------------------------------------------------------------------ map markers

    private static readonly Rgba RiverColor = new(0xFF3F7FC8);

    /// <summary>
    /// Rivers as blue lines, wider the more water they carry. Zoomed out only the great rivers show;
    /// the smaller ones appear as you zoom in.
    /// </summary>
    private void DrawRivers()
    {
        float zoom = _camera.Zoom;
        double minFlow = GameRules.MinRiverFlow * Math.Max(1, Math.Pow(6 / zoom, 1.5));
        float scale = Math.Clamp(zoom / 4, 0.35f, 1.6f);
        foreach (var r in Map.Rivers)
        {
            if (r.Flow < minFlow) continue;
            var a = _camera.MapToScreen(new Vector2(r.X1, r.Y1));
            var b = _camera.MapToScreen(new Vector2(r.X2, r.Y2));
            if (!OnScreen(a, 20) && !OnScreen(b, 20)) continue;
            if (Vector2.DistanceSquared(a, b) > 400 * zoom * zoom) continue; // the two ends landed on opposite sides of the date line
            float width = (0.7f + 1.3f * MathF.Log10(r.Flow / GameRules.MinRiverFlow)) * scale;
            Batch.Line(a, b, RiverColor.WithAlpha(0.9f), Math.Max(0.8f, width));
        }
    }

    private Vector2 Center(int provinceId)
    {
        var p = Map.Provinces[provinceId];
        return new Vector2(p.CenterX + 0.5f, p.CenterY + 0.5f);
    }

    /// <summary>Map position between two provinces, crossing the date line the short way.</summary>
    private Vector2 Between(int from, int to, double t)
    {
        var a = Center(from);
        var b = Center(to);
        float dx = b.X - a.X;
        dx -= Map.Width * MathF.Round(dx / Map.Width);
        return new Vector2(a.X + dx * (float)t, a.Y + (b.Y - a.Y) * (float)t);
    }

    private bool OnScreen(Vector2 p, float margin = 40) =>
        p.X > -margin && p.Y > -margin && p.X < _camera.Screen.X + margin && p.Y < _camera.Screen.Y + margin;

    private void DrawCities()
    {
        foreach (var city in _session.Cities)
        {
            var s = _camera.MapToScreen(Center(city.ProvinceId));
            if (!OnScreen(s)) continue;
            bool capital = _session.Players[city.OwnerId].CapitalCityId == city.Id;
            float size = capital ? 12 : 9;
            var color = new Rgba(_session.Players[city.OwnerId].Color);
            Batch.Rect(s.X - size / 2 - 2, s.Y - size / 2 - 2, size + 4, size + 4, Rgba.Black);
            Batch.Rect(s.X - size / 2, s.Y - size / 2, size, size, capital ? Theme.Accent : Rgba.White);
            Batch.Rect(s.X - size / 2 + 2, s.Y - size / 2 + 2, size - 4, size - 4, color);
            if (_camera.Zoom >= 2.5f || (capital && _camera.Zoom >= 1))
            {
                float w = Ui.Font.Measure(city.Name, FontSize.Small, true);
                Ui.Text(s.X - w / 2 + 1, s.Y + size / 2 + 3, city.Name, Rgba.Black, FontSize.Small, bold: true);
                Ui.Text(s.X - w / 2, s.Y + size / 2 + 2, city.Name, Rgba.White, FontSize.Small, bold: true);
            }
        }
    }

    private void DrawMigrations()
    {
        if (_camera.Zoom < 1.5f) return;
        foreach (var m in _session.Migrations)
        {
            var s = _camera.MapToScreen(Between(m.FromProvinceId, m.ToProvinceId, m.Progress(_session.Date.Hours)));
            if (!OnScreen(s)) continue;
            var color = new Rgba(_session.Players[m.OwnerId].Color);
            Batch.Rect(s.X - 3, s.Y - 3, 6, 6, Rgba.Black.WithAlpha(0.7f));
            Batch.Rect(s.X - 2, s.Y - 2, 4, 4, m.Forced ? Theme.Accent : color);
        }
    }

    private void DrawPath(Unit unit, Vector2 start)
    {
        var previous = start;
        foreach (int step in unit.Path)
        {
            var s = _camera.MapToScreen(Center(step));
            Batch.Line(previous, s, Theme.Accent.WithAlpha(0.85f), 2.5f);
            previous = s;
        }
        if (unit.Path.Count > 0) Batch.Rect(previous.X - 4, previous.Y - 4, 8, 8, Theme.Accent);
    }

    // ------------------------------------------------------------------ panels

    private void DrawTopBar()
    {
        var s = _app.ScreenSize;
        Ui.Panel(TopBarRect);
        float x = 12;
        Batch.Rect(x, 16, 24, 24, Rgba.Black);
        Batch.Rect(x + 2, 18, 20, 20, new Rgba(Human.Color));
        x += 32;
        Ui.Text(x, 8, Human.Name, Theme.Text, FontSize.Normal, bold: true);
        var stats = _session.Stats(Human);
        double population = stats.Settled, mood = stats.AverageMood;
        Ui.Text(x, 30, $"{population:N0} hab. · humor {mood:0}", Theme.Mood(mood, Theme.TextDim), FontSize.Small);
        if (Ui.Hover(new Rect(x, 28, 150, 20)))
            Ui.Tooltip($"{population:N0} habitantes\nHumor medio: {mood:0} ({GameRules.MoodName(mood)})");
        x += 150;

        Ui.Text(x, 8, _session.Date.ToString(), Theme.Text);
        string[] labels = ["||", "1", "2", "3", "4", "5"];
        for (int i = 0; i < labels.Length; i++)
        {
            string tip = i == 0 ? "Pausa (Espacio)" : $"Velocidad {i}: {GameSession.FormatHours(HoursPerSecond[i])} por segundo (tecla {i})";
            if (Ui.Button(new Rect(x + i * 29, 30, 26, 20), labels[i], active: _speed == i, tooltip: tip, size: FontSize.Small)) SetSpeed(i);
        }
        x += 200;

        foreach (var r in Resources.All.Where(Human.Knows))
        {
            double amount = Human.Stockpile[r];
            double net = Human.LastDayNet[(int)r];
            var rect = new Rect(x, 4, 88, 48);
            Ui.Text(x, 6, r.Name(), Theme.TextDim, FontSize.Small);
            Ui.Text(x, 24, Compact(amount), r == ResourceType.Food && Human.IsStarving ? Theme.Bad : Theme.Text, FontSize.Normal, bold: true);
            if (Math.Abs(net) >= 0.05)
            {
                float vw = Ui.Font.Measure(Compact(amount), FontSize.Normal, true);
                Ui.Text(x + vw + 4, 28, (net > 0 ? "+" : "") + Compact(net, decimals: true), net > 0 ? Theme.Good : Theme.Bad, FontSize.Small);
            }
            if (Ui.Hover(rect)) Ui.Tooltip($"{r.Name()}: {amount:N1}\nCambio en el último día: {net:+0.##;-0.##;0}");
            x += 90;
            if (x > s.X - 290) break;
        }

        // Science that no branch can take is going to waste: every branch is finished or waiting.
        bool idleScience = Human.SpareScience >= 1;
        string nationTip = idleScience ? "Gestionar el país (N)\nNinguna rama de la ciencia puede avanzar." : "Gestionar el país (N)";
        if (Ui.Button(new Rect(s.X - 190, 12, 92, 32), idleScience ? "Nación !" : "Nación", active: _nation.Visible, tooltip: nationTip))
            _nation.Visible = !_nation.Visible;
        if (Ui.Button(new Rect(s.X - 90, 12, 78, 32), "Menú")) _menuOpen = true;
    }

    /// <summary>Short form for the top bar: 950, 12,3k, 2,9M.</summary>
    private static string Compact(double value, bool decimals = false)
    {
        double abs = Math.Abs(value);
        if (abs >= 1_000_000) return $"{value / 1_000_000:0.#}M";
        if (abs >= 10_000) return $"{value / 1000:0.#}k";
        return decimals && abs < 100 ? $"{value:0.#}" : $"{value:N0}";
    }

    private void DrawSidePanel()
    {
        Unit? unit = _selectedUnitId is int uid ? _session.UnitById(uid) : null;
        if (unit == null) _selectedUnitId = null;
        if (unit == null && _selectedProvince < 0) return;

        var s = _app.ScreenSize;
        var panel = new Rect(s.X - SidePanelWidth - 8, TopBarHeight + 8, SidePanelWidth, s.Y - TopBarHeight - 70);
        Ui.Panel(panel);
        var y = panel.Y + 14;
        float x = panel.X + 16, w = panel.W - 32;
        if (Ui.Button(new Rect(panel.Right - 34, panel.Y + 8, 26, 24), "x", size: FontSize.Small))
        {
            _selectedUnitId = null;
            _selectedProvince = -1;
            return;
        }

        if (unit != null) UnitPanel(unit, x, ref y, w);
        else ProvincePanel(Map.Provinces[_selectedProvince], x, ref y, w);
    }

    private void Line(float x, ref float y, string label, string value, Rgba? valueColor = null)
    {
        Ui.Text(x, y, label, Theme.TextDim);
        Ui.Text(x + 130, y, value, valueColor ?? Theme.Text);
        y += 24;
    }

    /// <summary>Current mood, where it is heading and why, and what it does to the province.</summary>
    private string MoodTooltip(Province p)
    {
        var factors = _session.MoodFactors(p).Select(f => $"{f.Points:+0;-0;0}  {f.Reason}");
        string text = $"Humor {p.Mood:0}; tiende a {_session.TargetMood(p):0}.\n" + string.Join("\n", factors) +
                      $"\nProducción ×{GameRules.MoodProductivity(p.Mood):0.00}";
        if (p.Mood < GameRules.UnrestMood) text += "\nDescontento: no paga impuestos.";
        return text;
    }

    private void Paragraph(float x, ref float y, float w, string text, Rgba color, FontSize size = FontSize.Small)
    {
        foreach (var l in Ui.Font.Wrap(text, w, size))
        {
            Ui.Text(x, y, l, color, size);
            y += Ui.Font.LineHeight(size);
        }
    }

    private void ProvincePanel(Province p, float x, ref float y, float w)
    {
        var city = _session.CityIn(p);
        Ui.Text(x, y, city?.Name ?? p.DisplayName, Theme.Accent, FontSize.Large, bold: true);
        y += 36;
        if (p.IsOwned)
        {
            string buildings = p.Constructing.HasValue || p.PlannedCityName != null ? $"Edificios ({p.Buildings.Count}+1)" : $"Edificios ({p.Buildings.Count})";
            bool army = city != null && p.OwnerId == Human.Id;
            if (!army && _provinceTab == ProvinceTab.Army) _provinceTab = ProvinceTab.General;
            string[] tabs = army ? ["General", buildings, city!.Training.Count > 0 ? $"Ejército ({city.Training.Count})" : "Ejército"] : ["General", buildings];
            float tw = (w - 6 * (tabs.Length - 1)) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
                if (Ui.Button(new Rect(x + i * (tw + 6), y, tw, 28), tabs[i], active: (int)_provinceTab == i, size: FontSize.Small)) _provinceTab = (ProvinceTab)i;
            y += 38;
            if (_provinceTab == ProvinceTab.Buildings)
            {
                BuildingsPanel(p, x, ref y, w);
                return;
            }
            if (_provinceTab == ProvinceTab.Army)
            {
                ArmyPanel(city!, x, ref y, w);
                return;
            }
        }
        if (city != null) Line(x, ref y, "Provincia", p.DisplayName);
        if (p.PlannedCityName != null) Line(x, ref y, "Ciudad en obras", p.PlannedCityName, Theme.Accent);
        if (p.Name.Length > 0 || city != null) Line(x, ref y, "Terreno", p.Info.Name);
        Line(x, ref y, "Superficie", $"{p.AreaKm2:N0} km²");
        Line(x, ref y, "Altitud media", $"{p.MeanElevation:N0} m");
        if (p.RiverFlow > 0)
        {
            var row = new Rect(x, y, w, 24);
            Line(x, ref y, "Río", p.HasRiver ? "Gran río" : "Arroyo", p.HasRiver ? RiverColor : Theme.TextDim);
            if (Ui.Hover(row))
                Ui.Tooltip(p.HasRiver
                    ? $"Tierra fértil: +{GameRules.RiverFertility - 1:P0} de comida y de capacidad.\nQuien ataque debe cruzarlo: el defensor dispara un {MilitaryRules.RiverDefense - 1:P0} más."
                    : "Un arroyo: no cambia nada. Solo los grandes ríos fertilizan la tierra y protegen de los ataques.");
        }

        if (!p.IsClaimable)
        {
            y += 6;
            Paragraph(x, ref y, w, p.IsWater
                ? "Aguas abiertas: no se pueden reclamar y solo las unidades navales pueden navegarlas."
                : "Hielo polar: inhabitable. No se puede reclamar, pero sí atravesar.", Theme.TextDim);
            return;
        }

        if (p.IsOwned)
        {
            var owner = _session.Players[p.OwnerId];
            Line(x, ref y, "Dueño", owner.Name, new Rgba(owner.Color));
            Line(x, ref y, "Población", $"{p.Population:N0} / {_session.CapacityOf(p):N0}");
            if (p.Population >= 1)
            {
                var row = new Rect(x, y, w, 24);
                Line(x, ref y, "Humor", $"{p.Mood:0} · {GameRules.MoodName(p.Mood)}", Theme.Mood(p.Mood, Theme.Text));
                if (Ui.Hover(row)) Ui.Tooltip(MoodTooltip(p));
                row = new Rect(x, y, w, 24);
                Line(x, ref y, "Fertilidad", $"{p.Fertility:P0}", p.Fertility < 0.75 ? Theme.Bad : p.Fertility >= 1.15 ? Theme.Good : Theme.Text);
                if (Ui.Hover(row))
                    Ui.Tooltip("Nacimientos respecto a lo normal. Sube con el buen humor, cae con el hambre y cambia despacio.\n" +
                               $"Tiende a {_session.TargetFertility(p, owner, owner.IsStarving):P0}.");
            }
            int incoming = _session.Migrations.Where(m => m.ToProvinceId == p.Id).Sum(m => m.People);
            if (incoming > 0) Line(x, ref y, "En camino", $"{incoming:N0} migrantes");
        }
        else
        {
            Line(x, ref y, "Dueño", "Nadie", Theme.TextDim);
            Line(x, ref y, "Capacidad", $"{p.Capacity:N0} habitantes");
        }

        y += 6;
        Ui.Text(x, y, "Recursos", Theme.Text, FontSize.Normal, bold: true);
        y += 24;
        double fed = p.FoodYield * GameRules.FoodPerWorker;
        Line(x, ref y, "Comida", $"{fed * 1000:0} por mil hab./día");
        if (p.Info.WoodYield > 0) Line(x, ref y, "Madera", $"{p.Info.WoodYield:0.#} por mil hab./día");
        foreach (var r in Resources.Deposits.Where(r => p.Deposits[(int)r] > 0 && Human.Knows(r)))
        {
            var row = new Rect(x, y, w, 24);
            double left = p.Reserves[(int)r];
            if (left <= 0) Line(x, ref y, r.Name(), "Agotado", Theme.TextDim);
            else Line(x, ref y, r.Name(), $"{p.Deposits[(int)r]:0.0}/día · quedan {Compact(left)}");
            if (Ui.Hover(row))
                Ui.Tooltip(left <= 0 ? "Esta bolsa se ha agotado y ya no produce."
                    : $"Bolsa de {r.Name().ToLowerInvariant()}: quedan {left:N0} de {p.DepositSizes[(int)r] * GameRules.DepositSizeMultiplier:N0}.\n" +
                      $"Explotada al máximo ({GameRules.DepositFullWorkers:N0} habitantes) dura unos {left / p.Deposits[(int)r] / 365:0} años.");
        }

        if (p.OwnerId != Human.Id) return;

        if (city != null)
        {
            y += 10;
            Ui.Text(x, y, "Fiestas", Theme.Text, FontSize.Normal, bold: true);
            y += 26;
            var festival = _session.CanHoldFestival(city);
            string festivalLabel = city.HasFestival(_session.Date.Hours)
                ? $"De fiesta: quedan {GameSession.FormatHours(city.FestivalUntilHours - _session.Date.Hours)}"
                : $"Celebrar fiestas ({GameRules.FestivalCost(p.Population):N0} oro)";
            string festivalTip = $"+{GameRules.FestivalMood:0} al humor de la ciudad durante {GameRules.FestivalDays} días." +
                                 (festival.Ok ? "" : "\n" + festival.Message);
            if (Ui.Button(new Rect(x, y, w, 34), festivalLabel, festival.Ok, tooltip: festivalTip))
                Show(_session.HoldFestival(Human.Id, city.Id));
            y += 40;

            y += 10;
            Ui.Text(x, y, "Colonos", Theme.Text, FontSize.Normal, bold: true);
            y += 26;
            var settlers = _session.CanRecruitSettlers(city);
            string settlersTip = $"{GameRules.StartingCitizens} ciudadanos salen de la ciudad para fundar otra. Coste: {GameRules.SettlersCost}." +
                                 (settlers.Ok ? "" : "\n" + settlers.Message);
            if (Ui.Button(new Rect(x, y, w, 34), $"Enviar colonos ({GameRules.StartingCitizens} hab.)", settlers.Ok, tooltip: settlersTip))
                Show(_session.RecruitSettlers(Human.Id, city.Id));
            y += 40;
        }

        if (p.Population >= 1)
        {
            y += 10;
            Ui.Text(x, y, "Migración forzada", Theme.Text, FontSize.Normal, bold: true);
            y += 26;
            int keep = p.CityId.HasValue ? GameRules.MinCityPopulation : 0;
            int max = Math.Max(1, (int)p.Population - keep);
            _migrationAmount = Math.Clamp(_migrationAmount, 1, max);
            float bw = (w - 80) / 4;
            if (Ui.Button(new Rect(x, y, bw - 4, 28), "-100", size: FontSize.Small)) _migrationAmount -= 100;
            if (Ui.Button(new Rect(x + bw, y, bw - 4, 28), "-10", size: FontSize.Small)) _migrationAmount -= 10;
            Ui.TextCentered(new Rect(x + 2 * bw, y, 80, 28), _migrationAmount.ToString("N0"), Theme.Text, FontSize.Normal, bold: true);
            if (Ui.Button(new Rect(x + 2 * bw + 80, y, bw - 4, 28), "+10", size: FontSize.Small)) _migrationAmount += 10;
            if (Ui.Button(new Rect(x + 3 * bw + 80, y, bw - 4, 28), "+100", size: FontSize.Small)) _migrationAmount += 100;
            _migrationAmount = Math.Clamp(_migrationAmount, 1, max);
            y += 34;
            double cost = GameRules.ForcedMigrationCost(_migrationAmount);
            bool affordable = Human.Stockpile[ResourceType.Gold] >= cost;
            Ui.Text(x, y, $"Coste: {cost:N0} de oro", affordable ? Theme.TextDim : Theme.Bad, FontSize.Small);
            if (Ui.Button(new Rect(x + w - 60, y - 2, 60, 22), "Máx.", size: FontSize.Small)) _migrationAmount = max;
            y += 24;
            string label = _choosingMigrationTarget ? "Elige el destino en el mapa..." : "Enviar a otra provincia";
            if (Ui.Button(new Rect(x, y, w, 34), label, affordable && p.Population - keep >= 1, active: _choosingMigrationTarget,
                    tooltip: "Haz clic en una de tus provincias. Los ciudadanos viajan a 10 km/h."))
                _choosingMigrationTarget = !_choosingMigrationTarget;
        }
    }

    /// <summary>
    /// The province's buildings: the one under construction, the finished ones and, in your own
    /// provinces, a button for each building you can put up; the rest say what they are missing.
    /// Buildings whose advance you have not discovered are not listed.
    /// </summary>
    private void BuildingsPanel(Province p, float x, ref float y, float w)
    {
        if (p.Constructing.HasValue || p.PlannedCityName != null)
        {
            var (name, days) = p.Constructing is BuildingType building
                ? (building.Info().Name, building.Info().Days)
                : ($"ciudad de {p.PlannedCityName}", GameRules.CityBuildingDays);
            Ui.Text(x, y, $"En obras: {name}", Theme.Accent, bold: true);
            y += 26;
            float done = 1 - p.ConstructionDaysLeft / (float)days;
            Batch.Rect(x, y, w, 8, Theme.ButtonDisabled);
            Batch.Rect(x, y, w * done, 8, Theme.Accent);
            y += 14;
            Ui.Text(x, y, $"Quedan {p.ConstructionDaysLeft} días", Theme.TextDim, FontSize.Small);
            y += 30;
        }

        Ui.Text(x, y, "Construidos", Theme.Text, bold: true);
        y += 26;
        if (p.Buildings.Count == 0)
        {
            Ui.Text(x, y, "Ninguno todavía.", Theme.TextDim, FontSize.Small);
            y += 22;
        }
        foreach (var built in Buildings.All.Where(p.Buildings.Contains))
        {
            Ui.Text(x, y, built.Info().Name, Theme.Good);
            y += 22;
            Ui.Text(x + 10, y, built.Info().Description, Theme.TextDim, FontSize.Small);
            y += 24;
        }

        if (p.OwnerId != Human.Id) return;
        y += 10;
        Ui.Text(x, y, "Construir", Theme.Text, bold: true);
        y += 26;
        var missing = new List<(string Name, string Reason)>();
        if (!p.CityId.HasValue && p.PlannedCityName == null)
        {
            var site = _session.IsCitySite(Human.Id, p);
            if (!site.Ok) missing.Add(("Ciudad", site.Message));
            else
            {
                var can = _session.CanBuildCity(Human.Id, p);
                string tip = $"Los habitantes de la provincia levantan una ciudad con el nombre que elijas.\nCoste: {GameRules.CityCost}. Tarda {GameRules.CityBuildingDays} días."
                    + (can.Ok ? "" : "\n" + can.Message);
                if (Ui.Button(new Rect(x, y, w, 30), $"Ciudad  ·  {GameRules.CityCost}  ·  {GameRules.CityBuildingDays} d", can.Ok, tooltip: tip, size: FontSize.Small))
                    OpenCityNaming(null, p.Id);
                y += 34;
            }
        }
        // Buildings of advances not yet discovered stay out of the list altogether.
        foreach (var type in Buildings.All.Where(t => !p.Buildings.Contains(t) && p.Constructing != t && IsBuildingKnown(t)))
        {
            var available = _session.IsBuildingAvailable(p, type);
            if (!available.Ok)
            {
                missing.Add((type.Info().Name, available.Message));
                continue;
            }
            var info = type.Info();
            var can = _session.CanBuild(Human.Id, p, type);
            string tip = $"{info.Description}\nCoste: {info.Cost}. Tarda {info.Days} días." + (can.Ok ? "" : "\n" + can.Message);
            if (Ui.Button(new Rect(x, y, w, 30), $"{info.Name}  ·  {info.Cost}  ·  {info.Days} d", can.Ok, tooltip: tip, size: FontSize.Small))
                Show(_session.Build(Human.Id, p.Id, type));
            y += 34;
        }
        if (missing.Count == 0) return;
        y += 6;
        foreach (var (name, reason) in missing)
        {
            Ui.Text(x, y, $"{name}: {reason.TrimEnd('.').ToLowerInvariant()}", Theme.TextDisabled, FontSize.Small);
            y += 20;
        }
    }

    private bool IsBuildingKnown(BuildingType type) => type.Info().RequiresTech is not Tech tech || Human.Techs.Contains(tech);

    private void DrawBottomBar()
    {
        var s = _app.ScreenSize;
        var bar = new Rect(8, s.Y - 52, 732, 44);
        Ui.Panel(bar);
        string[] names = ["Terreno", "Político", "Población", "Humor", "Fertilidad", "Recursos"];
        for (int i = 0; i < names.Length; i++)
        {
            if (Ui.Button(new Rect(bar.X + 6 + i * 120, bar.Y + 6, 114, 32), names[i], active: (int)_renderer.Mode == i, tooltip: "Modo de mapa (Tab)"))
            {
                _renderer.Mode = (MapMode)i;
                _mapDirty = true;
            }
        }
        if (_renderer.Mode == MapMode.Resources) DrawResourceFilter(bar);
        // With the nation screen open this strip shows the latest message instead (see DrawMessages).
        if (_nation.Visible) return;

        string help = _choosingMigrationTarget
            ? "Clic izquierdo: elegir provincia de destino  ·  Esc: cancelar"
            : "Clic: seleccionar  ·  Arrastrar: mover mapa  ·  Clic dcho: mover unidad  ·  Rueda: zoom  ·  Espacio: pausa  ·  1-5: velocidad  ·  Inicio: tu capital";
        float hw = Ui.Font.Measure(help, FontSize.Small) + 20;
        Ui.Panel(new Rect(bar.Right + 6, s.Y - 44, hw, 30));
        Ui.Text(bar.Right + 16, s.Y - 38, help, _choosingMigrationTarget ? Theme.Accent : Theme.TextDim, FontSize.Small);
    }

    /// <summary>Row above the map modes that shows every deposit or only one resource; it doubles as the legend.</summary>
    private void DrawResourceFilter(Rect modes)
    {
        const float Bw = 96;
        var known = Resources.Deposits.Where(Human.Knows).ToList();
        var panel = new Rect(modes.X, modes.Y - 50, 12 + (known.Count + 1) * (Bw + 4) - 4, 44);
        Ui.Panel(panel);
        float x = panel.X + 6;
        if (Ui.Button(new Rect(x, panel.Y + 6, Bw, 32), "Todos", active: _renderer.ResourceFilter is null,
                tooltip: "Color del yacimiento principal de cada provincia", size: FontSize.Small))
        {
            _renderer.ResourceFilter = null;
            _mapDirty = true;
        }
        foreach (var r in known)
        {
            x += Bw + 4;
            var rect = new Rect(x, panel.Y + 6, Bw, 32);
            if (Ui.Button(rect, "    " + r.Name(), active: _renderer.ResourceFilter == r,
                    tooltip: $"Solo {r.Name().ToLowerInvariant()}: más intenso cuanto más queda en la bolsa", size: FontSize.Small))
            {
                _renderer.ResourceFilter = _renderer.ResourceFilter == r ? null : r;
                _mapDirty = true;
            }
            Batch.Rect(rect.X + 8, rect.Y + 11, 10, 10, MapRenderer.ResourceColor(r));
        }
    }

    private void DrawMessages()
    {
        const double Lifetime = 8;
        _messages.RemoveAll(m => _realTime - m.Time > Lifetime);
        var s = _app.ScreenSize;
        if (_nation.Visible)
        {
            DrawLatestMessageInStrip(Lifetime);
            return;
        }
        float y = s.Y - (_renderer.Mode == MapMode.Resources ? 120 : 70); // above the resource filter when it is open
        foreach (var (text, time, ok) in _messages.AsEnumerable().Reverse().Take(5))
        {
            float alpha = (float)Math.Clamp((Lifetime - (_realTime - time)) / 1.5, 0, 1);
            float w = Ui.Font.Measure(text, FontSize.Normal) + 24;
            var r = new Rect(s.X / 2 - w / 2, y - 30, w, 28);
            Batch.Rect(r.X, r.Y, r.W, r.H, Theme.Panel.WithAlpha(0.9f * alpha));
            Ui.TextCentered(r, text, (ok ? Theme.Text : Theme.Bad).WithAlpha(alpha));
            y -= 32;
        }
    }

    /// <summary>
    /// The nation screen covers the space where messages stack, so only the latest one shows, in the
    /// strip beside the map modes, cut short if it does not fit.
    /// </summary>
    private void DrawLatestMessageInStrip(double lifetime)
    {
        if (_messages.Count == 0) return;
        var (text, time, ok) = _messages[^1];
        var s = _app.ScreenSize;
        float x = 8 + 732 + 6, maxW = s.X - x - 8;
        if (Ui.Font.Measure(text, FontSize.Small) + 20 > maxW)
        {
            while (text.Length > 0 && Ui.Font.Measure(text + "...", FontSize.Small) + 20 > maxW) text = text[..^1];
            text = text.TrimEnd() + "...";
        }
        float alpha = (float)Math.Clamp((lifetime - (_realTime - time)) / 1.5, 0, 1);
        var r = new Rect(x, s.Y - 44, Ui.Font.Measure(text, FontSize.Small) + 20, 30);
        Batch.Rect(r.X, r.Y, r.W, r.H, Theme.Panel.WithAlpha(0.9f * alpha));
        Batch.Outline(r.X, r.Y, r.W, r.H, Theme.PanelBorder.WithAlpha(alpha));
        Ui.Text(r.X + 10, r.Y + 6, text, (ok ? Theme.Text : Theme.Bad).WithAlpha(alpha), FontSize.Small);
    }

    private void HoverTooltip()
    {
        var p = Map.Provinces[_hoverProvince];
        string owner = !p.IsClaimable ? "No reclamable" : p.IsOwned ? _session.Players[p.OwnerId].Name : "Sin dueño";
        var city = _session.CityIn(p);
        string text = (city != null ? $"{city.Name}  ·  {p.DisplayName}" : p.DisplayName)
            + (p.Name.Length > 0 ? $"  ·  {p.Info.Name.ToLowerInvariant()}" : "") + $"  ·  {owner}";
        if (p.HasRiver) text += "  ·  gran río";
        if (p.IsOwned) text += $"\n{p.Population:N0} habitantes";
        if (p.IsOwned && p.Population >= 1) text += $"\nHumor {p.Mood:0} ({GameRules.MoodName(p.Mood)})  ·  Fertilidad {p.Fertility:P0}";
        if (_renderer.Mode == MapMode.Resources)
            foreach (var r in Resources.Deposits.Where(r => p.Deposits[(int)r] > 0 && Human.Knows(r)))
                text += p.HasDeposit(r) ? $"\n{r.Name()}: {p.Deposits[(int)r]:0.0}/día, quedan {Compact(p.Reserves[(int)r])}" : $"\n{r.Name()}: agotado";
        if (_choosingMigrationTarget) text += "\nClic para enviar aquí a los migrantes";
        Ui.Tooltip(text);
    }

    private void DrawPauseMenu()
    {
        var s = _app.ScreenSize;
        Batch.Rect(0, 0, s.X, s.Y, Rgba.Black.WithAlpha(0.45f));
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        var panel = new Rect(s.X / 2 - 160, s.Y / 2 - 175, 320, 340);
        Ui.Panel(panel);
        Ui.TextCentered(new Rect(panel.X, panel.Y + 10, panel.W, 36), "Pausa", Theme.Accent, FontSize.Large, bold: true);
        float x = panel.X + 30, y = panel.Y + 60, w = panel.W - 60;
        if (Ui.Button(new Rect(x, y, w, 40), "Continuar")) _menuOpen = false;
        if (Ui.Button(new Rect(x, y + 50, w, 40), "Guardar partida", tooltip: $"Se guarda en {SaveFiles.Folder}")) SaveCurrentGame();
        if (Ui.Button(new Rect(x, y + 100, w, 40), "Historial de versiones")) { _changelog.Visible = true; _menuOpen = false; }
        if (Ui.Button(new Rect(x, y + 150, w, 40), "Menú principal")) _app.Show(new MainMenuScreen(_app));
        if (Ui.Button(new Rect(x, y + 200, w, 40), "Salir del juego")) _app.Quit();
        Ui.TextCentered(new Rect(panel.X, panel.Bottom - 30, panel.W, 24), $"Conquer {ConquerApp.Version}", Theme.TextDim, FontSize.Small);
    }

    /// <summary>Opens the dialog to name a city founded by these settlers, or built by this province's citizens.</summary>
    private void OpenCityNaming(int? unitId, int provinceId)
    {
        _naming = (unitId, provinceId);
        _cityName = _session.SuggestCityName();
    }

    private void ConfirmCityName()
    {
        if (_naming is not var (unitId, provinceId)) return;
        var result = unitId is int unit ? _session.FoundCity(Human.Id, unit, _cityName) : _session.BuildCity(Human.Id, provinceId, _cityName);
        Show(result);
        if (!result.Ok) return;
        _naming = null;
        if (unitId.HasValue)
        {
            _selectedUnitId = null;
            _selectedProvince = provinceId;
        }
    }

    /// <summary>Modal dialog: the city's name, suggested and editable, and whether it is free.</summary>
    private void DrawCityNaming()
    {
        var (unitId, provinceId) = _naming!.Value;
        var s = _app.ScreenSize;
        Batch.Rect(0, 0, s.X, s.Y, Rgba.Black.WithAlpha(0.45f));
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        var panel = new Rect(s.X / 2 - 230, s.Y / 2 - 130, 460, 250);
        Ui.Panel(panel);
        float x = panel.X + 24, y = panel.Y + 20, w = panel.W - 48;
        Ui.Text(x, y, unitId.HasValue ? "Fundar ciudad" : "Construir ciudad", Theme.Accent, FontSize.Large, bold: true);
        y += 34;
        string where = unitId.HasValue
            ? $"Los colonos fundarán la ciudad en {Map.Provinces[provinceId].DisplayName}."
            : $"Coste: {GameRules.CityCost}. Estará lista en {GameRules.CityBuildingDays} días.";
        Ui.Text(x, y, where, Theme.TextDim, FontSize.Small);
        y += 26;
        Ui.Text(x, y, "Nombre de la ciudad", Theme.Text);
        y += 24;
        _cityName = Ui.TextField(new Rect(x, y, w - 130, 36), _cityName, GameRules.MaxCityNameLength);
        if (Ui.Button(new Rect(x + w - 120, y, 120, 36), "Otro nombre", size: FontSize.Small)) _cityName = _session.SuggestCityName();
        y += 42;
        var check = _session.CheckCityName(_cityName);
        if (!check.Ok) Ui.Text(x, y, check.Message, Theme.Bad, FontSize.Small);

        float by = panel.Bottom - 56, bw = (w - 12) / 2;
        if (Ui.Button(new Rect(x, by, bw, 40), "Cancelar")) _naming = null;
        if (Ui.Button(new Rect(x + bw + 12, by, bw, 40), unitId.HasValue ? "Fundar" : "Construir", check.Ok)) ConfirmCityName();
    }

    private void SaveCurrentGame()
    {
        try
        {
            string name = SaveFiles.Save(_session);
            Show(CommandResult.Success($"Partida guardada: {name}."));
            _menuOpen = false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Show(CommandResult.Fail($"No se pudo guardar la partida: {e.Message}"));
        }
    }

    public void Dispose() => _renderer.Dispose();
}
