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
using Conquer.Presentation;
using Silk.NET.Input;

namespace Conquer.Client.Screens;

public sealed partial class GameScreen : IScreen, IAudibleScreen
{
    private const float TopBarHeight = 56;
    private const float SidePanelWidth = 340;

    private readonly ConquerApp _app;
    private readonly GameSession _session;
    private readonly GameController _game;

    public IReadOnlyList<string> Playlist => _game.Playlist;
    public IReadOnlyList<SoundCue> TakeSounds() => _game.TakeSounds();
    private readonly MapRenderer _renderer;
    private readonly RiverLayer _rivers;
    private readonly RoadLayer _roads;
    private readonly ChangelogView _changelog = new();
    private readonly HelpView _help = new();
    private readonly NationView _nation;
    private readonly List<(int UnitId, Rect Bounds)> _unitHitBoxes = [];

    private bool _mapDirty = true, _dragging;
    private long _lastRefreshDay = -1;
    private int _lastFog;
    /// <summary>The month the map was last coloured in: the terrain map shows the winter snow.</summary>
    private int _lastRefreshMonth = -1;

    private Ui Ui => _app.Ui;
    private Batch2D Batch => _app.Batch;
    private WorldMap Map => _session.Map;
    private Player Human => _session.Human;

    /// <param name="loaded">A saved game being carried on: it opens on the player's capital and does not repeat old messages.</param>
    public GameScreen(ConquerApp app, GameSession session, MapRenderer.Prepared pixels, bool loaded = false)
    {
        _app = app;
        _session = session;
        _game = new GameController(session, loaded);
        _game.Camera.Screen = app.ScreenSize;
        _renderer = new MapRenderer(app.Gl, session.Map, pixels);
        _rivers = new RiverLayer(session.Map);
        _roads = new RoadLayer(session.Map);
        session.OwnershipChanged += _ => _mapDirty = true;
        _nation = new NationView(_game.Nation);
        _renderer.IsResourceKnown = session.Human.Knows;
        if (!loaded) ApplyTestOptions(app.Options, _game.SelectedUnit!);
    }

    /// <summary>Command-line shortcuts for testing: found the capital, fast-forward and set the view.</summary>
    private void ApplyTestOptions(StartOptions options, Unit settlers)
    {
        if (options.Days > 0)
        {
            int capitalProvince = settlers.ProvinceId;
            _session.FoundCity(Human.Id, settlers.Id);
            for (int h = 0; h < options.Days * 24; h++) _session.Step();
            _game.SelectProvince(capitalProvince);
        }
        if (options.Zoom is float zoom) _game.Camera.LookAt(_game.Camera.Center, zoom);
        if (options.At is { } at)
            _game.Camera.LookAt(new Vector2((at.Longitude + 180) / 360 * Map.Width, (90 - at.Latitude) / 180 * Map.Height));
        if (Enum.TryParse<MapMode>(options.Mode, ignoreCase: true, out var mode)) _game.Mode = mode;
        if (Enum.TryParse<NationTab>(options.Nation, ignoreCase: true, out var tab)) { _game.Nation.Tab = tab; _game.Nation.Visible = true; }
        _game.ProvinceTab = options.Panel switch { "buildings" => ProvinceTab.Buildings, "army" => ProvinceTab.Army, _ => ProvinceTab.General };
        if (options.Panel is "regiment" or "march" or "edit" && Human.CapitalCityId is int capital) ShowSampleArmy(capital, march: options.Panel == "march");
        if (options.Panel == "edit" && _game.SelectedUnitId is int sample && _session.UnitById(sample) is { HasOfficer: true }) ShowSampleOfficers(sample);
        if (options.Panel == "found" && _session.UnitById(settlers.Id) != null) _game.OpenCityNaming(settlers.Id, settlers.ProvinceId);
        if (options.Panel == "battle") _game.OpenFirstBattle();
        if (options.Panel == "decision" && Human.CapitalCityId is int city) _session.StartDecision(Human.Id, DecisionKind.Drought, _session.CityById(city)!.ProvinceId);
    }

    /// <summary>
    /// For <c>--panel regiment</c>: trains three battalions and a corps HQ in the capital, merges the
    /// battalions into one regiment under the corps and selects it. With <paramref name="march"/> (<c>--panel march</c>)
    /// it also orders the regiment to the nearest land province some way off, to show its route.
    /// </summary>
    private void ShowSampleArmy(int capitalId, bool march = false)
    {
        var city = _session.CityById(capitalId)!;
        Map.Provinces[city.ProvinceId].Population += 1000;
        Map.Provinces[city.ProvinceId].AddBuilding(BuildingType.Barracks);
        foreach (var r in new[] { ResourceType.Wood, ResourceType.Gold }) Human.Stockpile[r] += 1000;
        Human.Learn(Tech.Archery);
        foreach (var type in new[] { BattalionType.Warriors, BattalionType.Archers, BattalionType.Warriors }) _session.Train(Human.Id, city.ProvinceId, type);
        _session.RaiseHeadquarters(Human.Id, city.ProvinceId, 1);
        for (int h = 0; h < 24 * 25; h++) _session.Step();
        var regiments = _session.Units.Where(u => u.OwnerId == Human.Id && u.IsMilitary).ToList();
        foreach (var other in regiments.Skip(1)) _session.Merge(Human.Id, regiments[0].Id, other.Id);
        if (_session.Units.FirstOrDefault(u => u.OwnerId == Human.Id && u.IsHeadquarters) is { } corps) _session.Attach(Human.Id, regiments[0].Id, corps.Id);
        _game.SelectUnit(regiments[0].Id);
        if (!march) return;
        var from = Center(city.ProvinceId);
        foreach (var p in Map.Provinces.Where(p => !p.IsWater).OrderBy(p => MathF.Abs(Vector2.Distance(Center(p.Id), from) - 30)))
            if (_session.MoveUnit(Human.Id, regiments[0].Id, p.Id).Ok) break;
    }

    public void Frame(double dt)
    {
        _game.Camera.Screen = _app.ScreenSize;
        HandleKeys(dt);
        _game.Tick(dt, _game.TimeStopped);

        if (_renderer.Mode != _game.Mode || _renderer.ResourceFilter != _game.ResourceFilter)
        {
            _renderer.Mode = _game.Mode;
            _renderer.ResourceFilter = _game.ResourceFilter;
            _mapDirty = true;
        }
        if (_game.FogSignature != _lastFog)
        {
            _lastFog = _game.FogSignature;
            _mapDirty = true;
        }
        if (_mapDirty || (_game.Mode >= MapMode.Population && _session.Date.Days != _lastRefreshDay)
            || (_game.Mode == MapMode.Terrain && _session.Date.MonthAndDay.Month != _lastRefreshMonth))
        {
            _renderer.Refresh(_session, _game.VisibleProvinces);
            _mapDirty = false;
            _lastRefreshDay = _session.Date.Days;
            _lastRefreshMonth = _session.Date.MonthAndDay.Month;
        }

        var mouseMap = _game.Camera.ScreenToMap(Ui.Input.Mouse);
        _game.HoverProvince = MapRenderer.ProvinceAt(Map, mouseMap, _game.Camera.Zoom);

        _renderer.Draw(_game.Camera, _game.SelectedProvince, _game.HoverProvince, _app.PixelScale, _game.Now);
        DrawRivers();
        _roads.Draw(Batch, _game.Camera, _session.Roads, _session.RoadProjects.Where(r => r.OwnerId == Human.Id), _game.PlannedRoute);
        DrawMarkers();

        DrawTopBar();
        if (!_game.Nation.Visible) DrawAlerts();
        if (!_game.Nation.Visible) DrawSidePanel();
        DrawBottomBar();
        _nation.Frame(Ui, NationRect);
        DrawMessages();
        if (_game.Naming.HasValue) DrawCityNaming();
        if (_game.EditingUnitId.HasValue) DrawUnitEditor();
        if (_game.BattleWindowOpen) DrawBattleWindow();
        if (_game.RoadWindowOpen) DrawRoadWindow();
        if (_game.DecisionOpen && !_game.RoadWindowOpen && !_game.Naming.HasValue && !_game.EditingUnitId.HasValue) DrawDecision();
        if (_game.MenuOpen) DrawPauseMenu();
        if (_game.ChangelogOpen && _changelog.Frame(Ui, new Rect(_app.ScreenSize.X / 2 - 380, 70, 760, _app.ScreenSize.Y - 140))) _game.ChangelogOpen = false;
        if (_game.HelpOpen && _help.Frame(Ui, new Rect(Math.Max(8, _app.ScreenSize.X / 2 - 520), 70, Math.Min(1040, _app.ScreenSize.X - 16), _app.ScreenSize.Y - 140))) _game.HelpOpen = false;

        bool modal = _game.MenuOpen || _game.Naming.HasValue || _game.EditingUnitId.HasValue || _game.BattleWindowOpen || _game.RoadWindowOpen || _game.DecisionOpen;
        if (!modal && !_game.ChangelogOpen && !_game.HelpOpen && !_game.Nation.Visible) HandleMapMouse();
        if (!Ui.MouseOverUi && !modal && !_game.HelpOpen && !_game.Nation.Visible && _game.HoverProvince >= 0 && !_dragging) HoverTooltip();
    }

    // ------------------------------------------------------------------ input

    private void HandleKeys(double dt)
    {
        var input = Ui.Input;
        if (_game.Naming.HasValue)
        {
            // While the city's name is being typed, keys belong to the text field.
            if (input.KeysPressed.Contains(Key.Escape)) _game.CancelCityNaming();
            else if (input.KeysPressed.Contains(Key.Enter) || input.KeysPressed.Contains(Key.KeypadEnter)) _game.ConfirmCityName();
            return;
        }
        if (_game.EditingUnitId.HasValue)
        {
            // Likewise while a unit is being edited: typing goes to its name.
            if (input.KeysPressed.Contains(Key.Escape)) _game.CloseUnitEditor();
            else if (input.KeysPressed.Contains(Key.Enter) || input.KeysPressed.Contains(Key.KeypadEnter)) _game.RenameEditedUnit();
            return;
        }
        foreach (var key in input.KeysPressed)
        {
            switch (key)
            {
                case Key.Escape: _game.Escape(); break;
                case Key.Space: _game.Clock.TogglePause(); break;
                case >= Key.Number1 and <= Key.Number5: _game.Clock.SetSpeed(key - Key.Number1 + 1); break;
                case Key.Tab: _game.CycleMode(); break;
                case Key.Home: _game.CenterOnHome(); break;
                case Key.N when !_game.MenuOpen: _game.Nation.Visible = !_game.Nation.Visible; break;
                case Key.F1 when !_game.MenuOpen: _game.HelpOpen = !_game.HelpOpen; break;
                case Key.KeypadAdd or Key.Equal: _game.Camera.ZoomAt(_game.Camera.Screen / 2, 1.25f); break;
                case Key.KeypadSubtract or Key.Minus: _game.Camera.ZoomAt(_game.Camera.Screen / 2, 0.8f); break;
            }
        }

        if (_game.MenuOpen) return;
        var pan = Vector2.Zero;
        if (input.KeysDown.Contains(Key.A) || input.KeysDown.Contains(Key.Left)) pan.X += 1;
        if (input.KeysDown.Contains(Key.D) || input.KeysDown.Contains(Key.Right)) pan.X -= 1;
        if (input.KeysDown.Contains(Key.W) || input.KeysDown.Contains(Key.Up)) pan.Y += 1;
        if (input.KeysDown.Contains(Key.S) || input.KeysDown.Contains(Key.Down)) pan.Y -= 1;
        if (pan != Vector2.Zero) _game.Camera.Pan(pan * (float)(900 * dt));
    }

    private void HandleMapMouse()
    {
        var input = Ui.Input;
        bool overUi = Ui.MouseOverUi;

        if (!overUi && input.Scroll != 0) _game.Camera.ZoomAt(input.Mouse, MathF.Pow(1.2f, input.Scroll));
        if (input.LeftPressed) _pressStartedOnUi = overUi;

        if (input.LeftDown && !_pressStartedOnUi && (_dragging || Vector2.Distance(input.Mouse, input.LeftPressPosition) > 5))
        {
            if (_dragging) _game.Camera.Pan(input.Mouse - _lastMouse);
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
        if (!_game.ChoosingMigrationTarget)
        {
            var battle = _battleHitBoxes.LastOrDefault(h => h.Bounds.Contains(Ui.Input.Mouse));
            if (battle.Bounds.W > 0)
            {
                _game.OpenBattle(battle.ProvinceId, battle.Battle);
                return;
            }
            var hit = _unitHitBoxes.LastOrDefault(h => h.Bounds.Contains(Ui.Input.Mouse));
            if (hit.Bounds.W > 0)
            {
                _game.SelectUnit(hit.UnitId);
                return;
            }
        }
        _game.ClickProvince();
    }

    private void RightClick() => _game.OrderMove();

    private Rect NationRect => new(Math.Max(8, _app.ScreenSize.X / 2 - 630), TopBarHeight + 12, Math.Min(1260, _app.ScreenSize.X - 16), _app.ScreenSize.Y - TopBarHeight - 76);

    // ------------------------------------------------------------------ map markers

    private void DrawRivers() => _rivers.Draw(Batch, _game.Camera, _game.Mode == MapMode.Terrain);

    private Vector2 Center(int provinceId) => _game.Center(provinceId);

    // ------------------------------------------------------------------ panels

    private void DrawTopBar()
    {
        var s = _app.ScreenSize;
        var bar = _game.TopBar();
        Ui.Panel(TopBarRect, radius: 0);
        float x = 12;
        Batch.Rect(x, 16, 24, 24, Rgba.Black);
        Batch.Rect(x + 2, 18, 20, 20, new Rgba(bar.Color));
        x += 32;
        Ui.Text(x, 8, bar.Nation, Theme.Text, FontSize.Normal, bold: true);
        Ui.Text(x, 30, bar.People, Theme.Of(bar.PeopleInk), FontSize.Small);
        if (Ui.Hover(new Rect(x, 28, 150, 20))) Ui.Tooltip(bar.PeopleTooltip);
        x += 150;

        Ui.Text(x, 8, bar.Date, Theme.Text);
        for (int i = 0; i < bar.Speeds.Count; i++) DocumentView.Press(Ui, bar.Speeds[i], new Rect(x + i * 29, 30, 26, 20));
        x += 200;

        foreach (var stock in bar.Resources)
        {
            // The icon, the amount in store and, under it, the last day's change.
            var rect = new Rect(x, 4, 88, 48);
            Icons.Resource(Batch, stock.Resource, new Vector2(x + 11, 28), 20);
            Ui.Text(x + 26, 9, stock.Amount, Theme.Of(stock.AmountInk), FontSize.Normal, bold: true);
            if (stock.Change != null) Ui.Text(x + 26, 31, stock.Change, Theme.Of(stock.ChangeInk), FontSize.Small);
            if (Ui.Hover(rect)) Ui.Tooltip(stock.Tooltip);
            x += 90;
            if (x > s.X - 330) break;
        }

        if (Ui.Button(new Rect(s.X - 190, 12, 92, 32), bar.NationButton, active: _game.Nation.Visible, tooltip: bar.NationTooltip))
            _game.Nation.Visible = !_game.Nation.Visible;
        if (Ui.Button(new Rect(s.X - 230, 12, 34, 32), "?", active: _game.HelpOpen, tooltip: "Ayuda (F1)")) _game.HelpOpen = !_game.HelpOpen;
        if (Ui.Button(new Rect(s.X - 90, 12, 78, 32), "Menú")) _game.MenuOpen = true;
    }

    /// <summary>The alerts in a column under the top bar, each a button with a mark of its colour on the left.</summary>
    private void DrawAlerts()
    {
        float y = TopBarHeight + 8;
        foreach (var alert in _game.Alerts())
        {
            var r = new Rect(8, y, Ui.Font.Measure(alert.Text, FontSize.Small) + 30, 26);
            if (Ui.Button(r, alert.Text, tooltip: alert.Tooltip, size: FontSize.Small)) alert.OnClick();
            Batch.Rect(r.X + 4, r.Y + 5, 3, r.H - 10, Theme.Of(alert.Tone));
            y += 30;
        }
    }

    private void DrawSidePanel()
    {
        if (_game.SidePanel() is not { } doc) return;
        var s = _app.ScreenSize;
        var panel = new Rect(s.X - SidePanelWidth - 8, TopBarHeight + 8, SidePanelWidth, s.Y - TopBarHeight - 70);
        Ui.Panel(panel);
        if (doc.OnClose != null && Ui.Button(new Rect(panel.Right - 34, panel.Y + 8, 26, 24), "x", size: FontSize.Small))
        {
            doc.OnClose();
            return;
        }
        float y = panel.Y + 14;
        DocumentView.Draw(Ui, doc, panel.X + 16, ref y, panel.W - 32);
    }

    private void Paragraph(float x, ref float y, float w, string text, Rgba color, FontSize size = FontSize.Small)
    {
        foreach (var l in Ui.Font.Wrap(text, w, size))
        {
            Ui.Text(x, y, l, color, size);
            y += Ui.Font.LineHeight(size);
        }
    }

    /// <summary>One 120-pixel button per map mode.</summary>
    private static readonly float ModeBarWidth = Enum.GetValues<MapMode>().Length * 120 + 12;

    private void DrawBottomBar()
    {
        var s = _app.ScreenSize;
        var bar = new Rect(8, s.Y - 52, ModeBarWidth, 44);
        Ui.Panel(bar);
        var modes = _game.ModeButtons();
        for (int i = 0; i < modes.Count; i++) DocumentView.Press(Ui, modes[i], new Rect(bar.X + 6 + i * 120, bar.Y + 6, 114, 32));
        DrawResourceFilter(bar);
        // With the nation screen open this strip shows the latest message instead (see DrawMessages).
        if (_game.Nation.Visible) return;

        var hints = _game.Hints();
        var items = hints.Items.ToList();
        // On a narrow screen the hints before the last give way, so "F1: ayuda" always shows.
        float room = s.X - bar.Right - 32;
        string help = string.Join("  ·  ", items);
        while (items.Count > 2 && Ui.Font.Measure(help, FontSize.Small) > room)
        {
            items.RemoveAt(items.Count - 2);
            help = string.Join("  ·  ", items);
        }
        float hw = Ui.Font.Measure(help, FontSize.Small) + 20;
        Ui.Panel(new Rect(bar.Right + 6, s.Y - 44, hw, 30));
        Ui.Text(bar.Right + 16, s.Y - 38, help, Theme.Of(hints.Ink), FontSize.Small);
    }

    /// <summary>Row above the map modes that shows every deposit or only one resource; it doubles as the legend.</summary>
    private void DrawResourceFilter(Rect modes)
    {
        const float Bw = 96;
        var buttons = _game.ResourceFilterButtons();
        if (buttons.Count == 0) return;
        var panel = new Rect(modes.X, modes.Y - 50, 12 + buttons.Count * (Bw + 4) - 4, 44);
        Ui.Panel(panel);
        for (int i = 0; i < buttons.Count; i++) DocumentView.Press(Ui, buttons[i], new Rect(panel.X + 6 + i * (Bw + 4), panel.Y + 6, Bw, 32));
    }

    private void DrawMessages()
    {
        var messages = _game.Messages.Current(_game.Now);
        var s = _app.ScreenSize;
        if (_game.Nation.Visible)
        {
            if (messages.Count > 0) DrawLatestMessageInStrip(messages[0]);
            return;
        }
        float y = s.Y - (_game.Mode == MapMode.Resources ? 120 : 70); // above the resource filter when it is open
        foreach (var message in messages.Take(5))
        {
            var (text, _, ok) = message;
            float alpha = message.Opacity(_game.Now);
            float w = Ui.Font.Measure(text, FontSize.Normal) + 24;
            var r = new Rect(s.X / 2 - w / 2, y - 30, w, 28);
            // A rounded strip with an accent (red for failures) along its left edge.
            Batch.RoundedRect(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, Theme.PanelTop.WithAlpha(0.92f * alpha), Theme.PanelBottom.WithAlpha(0.92f * alpha));
            Batch.Rect(r.X, r.Y + 4, 3, r.H - 8, (ok ? Theme.Accent : Theme.Bad).WithAlpha(alpha));
            Ui.TextCentered(r, text, (ok ? Theme.Text : Theme.Bad).WithAlpha(alpha));
            y -= 32;
        }
    }

    /// <summary>
    /// The nation screen covers the space where messages stack, so only the latest one shows, in the
    /// strip beside the map modes, cut short if it does not fit.
    /// </summary>
    private void DrawLatestMessageInStrip(Message message)
    {
        var (text, _, ok) = message;
        var s = _app.ScreenSize;
        float x = 8 + ModeBarWidth + 6, maxW = s.X - x - 8;
        if (Ui.Font.Measure(text, FontSize.Small) + 20 > maxW)
        {
            while (text.Length > 0 && Ui.Font.Measure(text + "...", FontSize.Small) + 20 > maxW) text = text[..^1];
            text = text.TrimEnd() + "...";
        }
        float alpha = message.Opacity(_game.Now);
        var r = new Rect(x, s.Y - 44, Ui.Font.Measure(text, FontSize.Small) + 20, 30);
        Batch.RoundedRect(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, Theme.PanelTop.WithAlpha(0.92f * alpha), Theme.PanelBottom.WithAlpha(0.92f * alpha));
        Batch.RoundedOutline(r.X, r.Y, r.W, r.H, Theme.ButtonRadius, Theme.PanelBorder.WithAlpha(alpha));
        Ui.Text(r.X + 10, r.Y + 6, text, (ok ? Theme.Text : Theme.Bad).WithAlpha(alpha), FontSize.Small);
    }

    private void HoverTooltip()
    {
        if (_game.MapTooltip() is { } tip) Ui.Tooltip(tip);
    }

    private void DrawPauseMenu()
    {
        var s = _app.ScreenSize;
        Batch.Rect(0, 0, s.X, s.Y, Rgba.Black.WithAlpha(0.45f));
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        var buttons = _game.PauseMenu(new MenuNavigator(_app), ConquerApp.Version);
        float height = 60 + buttons.Count * 50 + 40;
        var panel = new Rect(s.X / 2 - 160, s.Y / 2 - height / 2, 320, height);
        Ui.Panel(panel);
        Ui.TextCentered(new Rect(panel.X, panel.Y + 10, panel.W, 36), "Pausa", Theme.Accent, FontSize.Large, bold: true);
        float x = panel.X + 30, y = panel.Y + 60, w = panel.W - 60;
        foreach (var button in buttons)
        {
            DocumentView.Press(Ui, button, new Rect(x, y, w, 40));
            y += 50;
        }
        Ui.TextCentered(new Rect(panel.X, panel.Bottom - 30, panel.W, 24), $"Conquer {ConquerApp.Version}", Theme.TextDim, FontSize.Small);
    }

    /// <summary>Modal dialog: the city's name, suggested and editable, and whether it is free.</summary>
    private void DrawCityNaming()
    {
        if (_game.CityNamingDialog() is not { } dialog) return;
        var s = _app.ScreenSize;
        Batch.Rect(0, 0, s.X, s.Y, Rgba.Black.WithAlpha(0.45f));
        Ui.Block(new Rect(0, 0, s.X, s.Y));
        var panel = new Rect(s.X / 2 - 230, s.Y / 2 - 130, 460, 250);
        Ui.Panel(panel);
        float x = panel.X + 24, y = panel.Y + 20, w = panel.W - 48;
        Ui.Text(x, y, dialog.Title, Theme.Accent, FontSize.Large, bold: true);
        y += 34;
        Ui.Text(x, y, dialog.Detail, Theme.TextDim, FontSize.Small);
        y += 26;
        Ui.Text(x, y, "Nombre de la ciudad", Theme.Text);
        y += 24;
        _game.CityName = Ui.TextField(new Rect(x, y, w - 130, 36), _game.CityName, GameRules.MaxCityNameLength);
        if (Ui.Button(new Rect(x + w - 120, y, 120, 36), "Otro nombre", size: FontSize.Small)) _game.SuggestCityName();
        y += 42;
        if (dialog.Error != null) Ui.Text(x, y, dialog.Error, Theme.Bad, FontSize.Small);

        float by = panel.Bottom - 56, bw = (w - 12) / 2;
        if (Ui.Button(new Rect(x, by, bw, 40), "Cancelar")) _game.CancelCityNaming();
        if (Ui.Button(new Rect(x + bw + 12, by, bw, 40), dialog.Confirm, dialog.Error == null)) _game.ConfirmCityName();
    }

    public void Dispose() => _renderer.Dispose();
}
