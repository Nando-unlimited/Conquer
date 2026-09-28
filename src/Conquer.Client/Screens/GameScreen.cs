using System.Numerics;
using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Silk.NET.Input;

namespace Conquer.Client.Screens;

public sealed class GameScreen : IScreen
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
    private readonly List<(int UnitId, Rect Bounds)> _unitHitBoxes = [];
    private readonly List<(string Text, double Time, bool Ok)> _messages = [];

    private int _speed = 1, _lastSpeed = 1;
    private double _hourAccumulator, _realTime;
    private bool _mapDirty = true, _menuOpen, _dragging;
    private long _lastRefreshDay = -1;
    private int _seenNotifications;
    private int? _selectedUnitId;
    private int _selectedProvince = -1, _hoverProvince = -1;
    private bool _choosingMigrationTarget;
    private int _migrationAmount = 50;

    private Ui Ui => _app.Ui;
    private Batch2D Batch => _app.Batch;
    private WorldMap Map => _session.Map;
    private Player Human => _session.Human;

    public GameScreen(ConquerApp app, GameSession session, MapRenderer.Prepared pixels)
    {
        _app = app;
        _session = session;
        _renderer = new MapRenderer(app.Gl, session.Map, pixels);
        _camera = new Camera(session.Map.Width, session.Map.Height) { Screen = app.ScreenSize };
        session.OwnershipChanged += _ => _mapDirty = true;

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
        DrawCities();
        DrawMigrations();
        DrawUnits();

        DrawTopBar();
        DrawSidePanel();
        DrawBottomBar();
        DrawMessages();
        if (_menuOpen) DrawPauseMenu();
        _changelog.Frame(Ui, new Rect(_app.ScreenSize.X / 2 - 380, 70, 760, _app.ScreenSize.Y - 140));

        if (!_menuOpen && !_changelog.Visible) HandleMapMouse();
        if (!Ui.MouseOverUi && !_menuOpen && _hoverProvince >= 0 && !_dragging) HoverTooltip();
    }

    // ------------------------------------------------------------------ time and input

    private void AdvanceTime(double dt)
    {
        if (_menuOpen || _changelog.Visible || _speed == 0) return;
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
        foreach (var key in input.KeysPressed)
        {
            switch (key)
            {
                case Key.Escape:
                    if (_changelog.Visible) _changelog.Visible = false;
                    else if (_choosingMigrationTarget) _choosingMigrationTarget = false;
                    else if (_selectedUnitId.HasValue || _selectedProvince >= 0) { _selectedUnitId = null; _selectedProvince = -1; }
                    else _menuOpen = !_menuOpen;
                    break;
                case Key.Space: SetSpeed(_speed == 0 ? _lastSpeed : 0); break;
                case >= Key.Number1 and <= Key.Number5: SetSpeed(key - Key.Number1 + 1); break;
                case Key.Tab: _renderer.Mode = (MapMode)(((int)_renderer.Mode + 1) % Enum.GetValues<MapMode>().Length); _mapDirty = true; break;
                case Key.Home: CenterOnHome(); break;
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

    /// <summary>NATO-style counters: a cross for infantry, a "C" for settlers.</summary>
    private void DrawUnits()
    {
        _unitHitBoxes.Clear();
        var stackIndex = new Dictionary<int, int>();
        foreach (var unit in _session.Units)
        {
            var pos = unit.IsMoving ? Between(unit.ProvinceId, unit.Path[0], unit.StepProgress) : Center(unit.ProvinceId);
            var s = _camera.MapToScreen(pos);
            if (!OnScreen(s)) continue;
            int stack = stackIndex.GetValueOrDefault(unit.ProvinceId);
            stackIndex[unit.ProvinceId] = stack + 1;
            s += new Vector2(stack * 5, -stack * 5 - 14);

            bool selected = unit.Id == _selectedUnitId;
            if (selected) DrawPath(unit, s);

            // Counters shrink when zoomed out so they don't bury the map.
            float scale = selected ? 1 : Math.Clamp(_camera.Zoom / 3, 0.45f, 1);
            float W = 28 * scale, H = 19 * scale;
            var r = new Rect(s.X - W / 2, s.Y - H / 2, W, H);
            var color = new Rgba(_session.Players[unit.OwnerId].Color);
            Batch.Rect(r.X - 2, r.Y - 2, r.W + 4, r.H + 4, selected ? Theme.Accent : Rgba.Black);
            Batch.Rect(r.X, r.Y, r.W, r.H, color.Scale(0.55f).WithAlpha(1));
            Batch.Rect(r.X + 2, r.Y + 2, r.W - 4, r.H - 4, color);
            if (unit.Info.IsMilitary)
            {
                Batch.Line(new(r.X + 2, r.Y + 2), new(r.Right - 2, r.Bottom - 2), Rgba.Black, 1.5f);
                Batch.Line(new(r.X + 2, r.Bottom - 2), new(r.Right - 2, r.Y + 2), Rgba.Black, 1.5f);
            }
            else if (scale > 0.7f) Ui.TextCentered(r, unit.Info.Symbol, Rgba.Black, FontSize.Small, bold: true);
            _unitHitBoxes.Add((unit.Id, r));
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
        double population = Human.Provinces.Sum(id => Map.Provinces[id].Population);
        double mood = _session.AverageMood(Human);
        Ui.Text(x, 30, $"{population:N0} hab. · humor {mood:0}", MoodColor(mood, Theme.TextDim), FontSize.Small);
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

        foreach (var r in Resources.All)
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
            if (x > s.X - 110) break;
        }

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

    private static Rgba MoodColor(double mood, Rgba normal) =>
        mood < GameRules.UnrestMood ? Theme.Bad : mood >= 65 ? Theme.Good : normal;

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

    private void UnitPanel(Unit unit, float x, ref float y, float w)
    {
        var owner = _session.Players[unit.OwnerId];
        var here = Map.Provinces[unit.ProvinceId];
        Ui.Text(x, y, unit.Info.Name, Theme.Accent, FontSize.Large, bold: true);
        y += 36;
        Line(x, ref y, "Nación", owner.Name, new Rgba(owner.Color));
        Line(x, ref y, "Ciudadanos", unit.Citizens.ToString("N0"));
        Line(x, ref y, "Ubicación", here.Info.Name);
        if (unit.IsMoving && unit.Destination is int dest)
        {
            double hours = unit.HoursToNext;
            for (int i = 0; i + 1 < unit.Path.Count; i++) hours += _session.Pathfinder.StepHours(unit.Path[i], unit.Path[i + 1]);
            Line(x, ref y, "Destino", Map.Provinces[dest].Info.Name);
            Line(x, ref y, "Llegada en", GameSession.FormatHours(hours));
        }
        else Line(x, ref y, "Estado", "Esperando órdenes");
        y += 8;

        if (unit.OwnerId != Human.Id) return;
        Paragraph(x, ref y, w, "Clic derecho en el mapa para mover la unidad. Viaja a 10 km/h por tierra, más despacio por montañas, selvas y hielo; no puede entrar en el mar.", Theme.TextDim);
        y += 10;

        if (unit.Info.CanFoundCity)
        {
            var can = _session.CanFoundCity(unit);
            if (Ui.Button(new Rect(x, y, w, 36), "Fundar ciudad", can.Ok, tooltip: can.Ok ? "Reclama esta provincia y funda una ciudad con estos colonos." : can.Message))
                Show(_session.FoundCity(Human.Id, unit.Id));
            y += 44;
        }
        if (unit.Info.IsMilitary)
        {
            var can = _session.CanClaim(unit);
            if (Ui.Button(new Rect(x, y, w, 36), "Reclamar provincia", can.Ok, tooltip: can.Ok ? "Esta provincia pasará a ser tuya." : can.Message))
                Show(_session.Claim(Human.Id, unit.Id));
            y += 44;
        }
        bool canSettle = here.OwnerId == Human.Id;
        if (Ui.Button(new Rect(x, y, w, 36), "Asentarse aquí", canSettle,
                tooltip: canSettle ? "Disuelve la unidad; sus ciudadanos se quedan a vivir en esta provincia." : "Solo en una provincia propia."))
        {
            Show(_session.Disband(Human.Id, unit.Id));
            _selectedProvince = here.Id;
            return;
        }
        y += 44;
        if (unit.IsMoving && Ui.Button(new Rect(x, y, w, 36), "Detener")) _session.MoveUnit(Human.Id, unit.Id, unit.ProvinceId);
    }

    private void ProvincePanel(Province p, float x, ref float y, float w)
    {
        var city = _session.CityIn(p);
        Ui.Text(x, y, city?.Name ?? p.Info.Name, Theme.Accent, FontSize.Large, bold: true);
        y += 36;
        if (city != null) Line(x, ref y, "Terreno", p.Info.Name);
        Line(x, ref y, "Superficie", $"{p.AreaKm2:N0} km²");
        Line(x, ref y, "Altitud media", $"{p.MeanElevation:N0} m");

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
                Line(x, ref y, "Humor", $"{p.Mood:0} · {GameRules.MoodName(p.Mood)}", MoodColor(p.Mood, Theme.Text));
                if (Ui.Hover(row)) Ui.Tooltip(MoodTooltip(p));
                row = new Rect(x, y, w, 24);
                Line(x, ref y, "Fertilidad", $"{p.Fertility:P0}", p.Fertility < 0.75 ? Theme.Bad : p.Fertility >= 1.15 ? Theme.Good : Theme.Text);
                if (Ui.Hover(row))
                    Ui.Tooltip("Nacimientos respecto a lo normal. Sube con el buen humor, cae con el hambre y cambia despacio.\n" +
                               $"Tiende a {GameRules.TargetFertility(p.Mood, owner.IsStarving):P0}.");
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
        double fed = p.Info.FoodYield * GameRules.FoodPerWorker;
        Line(x, ref y, "Comida", $"{fed * 1000:0} por mil hab./día");
        if (p.Info.WoodYield > 0) Line(x, ref y, "Madera", $"{p.Info.WoodYield:0.#} por mil hab./día");
        foreach (var r in Resources.Deposits.Where(r => p.Deposits[(int)r] > 0))
            Line(x, ref y, r.Name(), $"{p.Deposits[(int)r]:0.0} al día");

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
            Ui.Text(x, y, "Reclutar", Theme.Text, FontSize.Normal, bold: true);
            y += 26;
            foreach (var type in new[] { UnitType.Settlers, UnitType.Warriors })
            {
                var info = type.Info();
                var can = _session.CanRecruit(city, type);
                string tip = $"{info.Citizens} ciudadanos de la ciudad. Coste: {info.Cost}." + (can.Ok ? "" : "\n" + can.Message);
                if (Ui.Button(new Rect(x, y, w, 34), $"{info.Name} ({info.Citizens} hab.)", can.Ok, tooltip: tip))
                    Show(_session.Recruit(Human.Id, city.Id, type));
                y += 40;
            }
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

    private void DrawBottomBar()
    {
        var s = _app.ScreenSize;
        var bar = new Rect(8, s.Y - 52, 612, 44);
        Ui.Panel(bar);
        string[] names = ["Terreno", "Político", "Población", "Humor", "Fertilidad"];
        for (int i = 0; i < names.Length; i++)
        {
            if (Ui.Button(new Rect(bar.X + 6 + i * 120, bar.Y + 6, 114, 32), names[i], active: (int)_renderer.Mode == i, tooltip: "Modo de mapa (Tab)"))
            {
                _renderer.Mode = (MapMode)i;
                _mapDirty = true;
            }
        }

        string help = _choosingMigrationTarget
            ? "Clic izquierdo: elegir provincia de destino  ·  Esc: cancelar"
            : "Clic: seleccionar  ·  Arrastrar: mover mapa  ·  Clic dcho: mover unidad  ·  Rueda: zoom  ·  Espacio: pausa  ·  1-5: velocidad  ·  Inicio: tu capital";
        float hw = Ui.Font.Measure(help, FontSize.Small) + 20;
        Ui.Panel(new Rect(bar.Right + 6, s.Y - 44, hw, 30));
        Ui.Text(bar.Right + 16, s.Y - 38, help, _choosingMigrationTarget ? Theme.Accent : Theme.TextDim, FontSize.Small);
    }

    private void DrawMessages()
    {
        const double Lifetime = 8;
        _messages.RemoveAll(m => _realTime - m.Time > Lifetime);
        var s = _app.ScreenSize;
        float y = s.Y - 70;
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

    private void HoverTooltip()
    {
        var p = Map.Provinces[_hoverProvince];
        string owner = !p.IsClaimable ? "No reclamable" : p.IsOwned ? _session.Players[p.OwnerId].Name : "Sin dueño";
        string text = $"{p.Info.Name}  ·  {owner}";
        if (p.IsOwned) text += $"\n{p.Population:N0} habitantes";
        if (p.IsOwned && p.Population >= 1) text += $"\nHumor {p.Mood:0} ({GameRules.MoodName(p.Mood)})  ·  Fertilidad {p.Fertility:P0}";
        if (_choosingMigrationTarget) text += "\nClic para enviar aquí a los migrantes";
        Ui.Tooltip(text);
    }

    private void DrawPauseMenu()
    {
        var s = _app.ScreenSize;
        Batch.Rect(0, 0, s.X, s.Y, Rgba.Black.WithAlpha(0.45f));
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        var panel = new Rect(s.X / 2 - 160, s.Y / 2 - 150, 320, 290);
        Ui.Panel(panel);
        Ui.TextCentered(new Rect(panel.X, panel.Y + 10, panel.W, 36), "Pausa", Theme.Accent, FontSize.Large, bold: true);
        float x = panel.X + 30, y = panel.Y + 60, w = panel.W - 60;
        if (Ui.Button(new Rect(x, y, w, 40), "Continuar")) _menuOpen = false;
        if (Ui.Button(new Rect(x, y + 50, w, 40), "Historial de versiones")) { _changelog.Visible = true; _menuOpen = false; }
        if (Ui.Button(new Rect(x, y + 100, w, 40), "Menú principal")) _app.Show(new MainMenuScreen(_app));
        if (Ui.Button(new Rect(x, y + 150, w, 40), "Salir del juego")) _app.Quit();
        Ui.TextCentered(new Rect(panel.X, panel.Bottom - 30, panel.W, 24), $"Conquer {ConquerApp.Version}", Theme.TextDim, FontSize.Small);
    }

    public void Dispose() => _renderer.Dispose();
}
