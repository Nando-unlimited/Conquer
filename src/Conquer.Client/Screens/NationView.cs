using Conquer.Client.Graphics;
using Conquer.Client.UI;
using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Client.Screens;

public enum NationTab
{
    Summary,
    Cities,
    Provinces,
    Science,
    Army,
    Diplomacy,
}

/// <summary>
/// The nation screen: an overview of the player's people and economy, and tables of their cities and
/// provinces with the actions that apply to each. Time keeps running while it is open.
/// </summary>
public sealed partial class NationView
{
    private const float RowHeight = 34;
    private static readonly string[] TabNames = ["Resumen", "Ciudades", "Provincias", "Ciencia", "Ejército", "Diplomacia"];
    /// <summary>Colours of the mood levels, worst first.</summary>
    private static readonly Rgba[] MoodLevelColors = [Theme.Bad, new(0xFFE0A050), new(0xFFB9C08A), Theme.Good];

    private readonly GameSession _session;
    private readonly Player _player;
    private readonly Action<int> _viewProvince;
    private readonly Action<int> _viewUnit;
    private readonly Action<CommandResult> _show;
    private readonly float[] _scroll = new float[TabNames.Length];
    private readonly int[] _sortColumn = new int[TabNames.Length];
    private readonly bool[] _sortAscending = new bool[TabNames.Length];

    public bool Visible { get; set; }
    public NationTab Tab { get; set; }

    /// <param name="viewProvince">Called when the player asks to see a province on the map; the view closes.</param>
    /// <param name="viewUnit">Called when the player asks to see a unit on the map; the view closes.</param>
    /// <param name="show">Shows the result of an order to the player.</param>
    public NationView(GameSession session, Player player, Action<int> viewProvince, Action<int> viewUnit, Action<CommandResult> show)
    {
        _session = session;
        _player = player;
        _viewProvince = viewProvince;
        _viewUnit = viewUnit;
        _show = show;
        // Tables start sorted by population, largest first.
        _sortColumn[(int)NationTab.Cities] = _sortColumn[(int)NationTab.Provinces] = 1;
    }

    private WorldMap Map => _session.Map;

    public void Frame(Ui ui, Rect area)
    {
        if (!Visible) return;
        ui.Batch.Rect(area.X, area.Y, area.W, area.H, Theme.Panel.WithAlpha(1)); // opaque: the map would clutter the tables
        ui.Panel(area);
        ui.Text(area.X + 20, area.Y + 14, _player.Name, Theme.Accent, FontSize.Large, bold: true);
        float tx = area.X + 40 + ui.Font.Measure(_player.Name, FontSize.Large, true);
        for (int i = 0; i < TabNames.Length; i++)
            if (ui.Button(new Rect(tx + i * 116, area.Y + 12, 110, 32), TabNames[i], active: (int)Tab == i)) Tab = (NationTab)i;
        if (ui.Button(new Rect(area.Right - 120, area.Y + 12, 100, 32), "Cerrar", tooltip: "Cerrar (N o Esc)")) Visible = false;

        var content = new Rect(area.X + 20, area.Y + 60, area.W - 40, area.H - 76);
        switch (Tab)
        {
            case NationTab.Summary: Summary(ui, content); break;
            case NationTab.Cities: Cities(ui, content); break;
            case NationTab.Provinces: Provinces(ui, content); break;
            case NationTab.Science: Science(ui, content); break;
            case NationTab.Army: Army(ui, content); break;
            case NationTab.Diplomacy: Diplomacy(ui, content); break;
        }
    }

    // ------------------------------------------------------------------ summary

    private void Summary(Ui ui, Rect r)
    {
        var stats = _session.Stats(_player);
        float colW = (r.W - 30) / 2;
        float x = r.X, y = r.Y;

        Heading(ui, x, ref y, "Población");
        Row(ui, x, ref y, colW, "Total", $"{stats.Total:N0}");
        Row(ui, x, ref y, colW, "En sus provincias", $"{stats.Settled:N0}");
        Row(ui, x, ref y, colW, "En unidades", $"{stats.InUnits:N0}");
        Row(ui, x, ref y, colW, "Migrando", $"{stats.Migrating:N0}");
        Row(ui, x, ref y, colW, "Fertilidad media", $"{stats.AverageFertility:P0}",
            stats.AverageFertility < 0.75 ? Theme.Bad : stats.AverageFertility >= 1.15 ? Theme.Good : Theme.Text);
        y += 10;

        Heading(ui, x, ref y, "Humor");
        Row(ui, x, ref y, colW, "Humor medio", $"{stats.AverageMood:0} · {GameRules.MoodName(stats.AverageMood)}",
            Theme.Mood(stats.AverageMood, Theme.Text));
        MoodBar(ui, x, ref y, colW, stats);
        y += 10;

        Heading(ui, x, ref y, "Territorio");
        string capital = _player.CapitalCityId is int id && _session.CityById(id) is { } c ? c.Name : "Ninguna";
        Row(ui, x, ref y, colW, "Capital", capital);
        Row(ui, x, ref y, colW, "Provincias", $"{stats.Provinces:N0}");
        Row(ui, x, ref y, colW, "Ciudades", $"{stats.Cities:N0}");
        Row(ui, x, ref y, colW, "Unidades", $"{stats.Units:N0}");
        y += 10;

        Heading(ui, x, ref y, "Ciencia");
        Row(ui, x, ref y, colW, "Puntos por día", $"{_player.LastDayScience:0.##}");
        Row(ui, x, ref y, colW, "Investigando", _player.Researching is Tech current
            ? $"{current.Info().Name} ({_player.ResearchProgress[(int)current] / current.Info().Cost:P0})"
            : "Nada", _player.Researching is null ? Theme.Accent : Theme.Text);

        x = r.X + colW + 30;
        y = r.Y;
        Heading(ui, x, ref y, "Comida");
        double food = _player.Stockpile[ResourceType.Food];
        double foodNet = _player.LastDayNet[(int)ResourceType.Food];
        Row(ui, x, ref y, colW, "Almacén", $"{food:N0}", _player.IsStarving ? Theme.Bad : Theme.Text);
        Row(ui, x, ref y, colW, "Balance diario", $"{foodNet:+#,0.#;-#,0.#;0}", foodNet < 0 ? Theme.Bad : Theme.Good);
        string reserve = _player.IsStarving ? "¡Hambre!" : $"{_player.FoodReserveDays:N0} días";
        Row(ui, x, ref y, colW, "Reservas", reserve,
            _player.FoodReserveDays >= GameRules.FoodReserveFullDays ? Theme.Good : _player.FoodReserveDays < 7 ? Theme.Bad : Theme.Text);
        if (ui.Hover(new Rect(x, y - 24, colW, 24)))
            ui.Tooltip($"Días que dura la comida al consumo actual. Con {GameRules.FoodReserveFullDays:0} días o más la población gana +{GameRules.FoodReserveMood:0} de humor.");
        y += 10;

        Heading(ui, x, ref y, "Recursos");
        ui.Text(x + colW - 310, y, "Almacén", Theme.TextDim, FontSize.Small);
        ui.Text(x + colW - 200, y, "Por día", Theme.TextDim, FontSize.Small);
        ui.Text(x + colW - 90, y, "En bolsas", Theme.TextDim, FontSize.Small);
        if (ui.Hover(new Rect(x + colW - 90, y, 90, 20)))
            ui.Tooltip("Lo que queda en los yacimientos de tus provincias. Cada bolsa se agota al explotarla.");
        y += 22;
        foreach (var res in Resources.All.Where(res => res != ResourceType.Food && _player.Knows(res)))
        {
            double net = _player.LastDayNet[(int)res];
            ui.Text(x, y, res.Name(), Theme.TextDim);
            ui.Text(x + colW - 310, y, $"{_player.Stockpile[res]:N0}");
            ui.Text(x + colW - 200, y, Math.Abs(net) < 0.005 ? "-" : $"{net:+#,0.##;-#,0.##}", net > 0 ? Theme.Good : net < 0 ? Theme.Bad : Theme.TextDim);
            bool mined = Resources.Deposits.Contains(res);
            ui.Text(x + colW - 90, y, mined ? Compact(stats.Reserves[(int)res]) : "-", mined && stats.Reserves[(int)res] > 0 ? Theme.Text : Theme.TextDim);
            y += 24;
        }
    }

    /// <summary>A bar split by how many citizens live at each mood level, with its legend.</summary>
    private static void MoodBar(Ui ui, float x, ref float y, float w, NationStats stats)
    {
        double total = stats.PopulationByMood.Sum();
        float bx = x;
        for (int i = 0; i < MoodLevelColors.Length && total > 0; i++)
        {
            float bw = (float)(w * stats.PopulationByMood[i] / total);
            ui.Batch.Rect(bx, y, bw, 16, MoodLevelColors[i]);
            bx += bw;
        }
        if (total <= 0) ui.Batch.Rect(x, y, w, 16, Theme.ButtonDisabled);
        y += 24;
        for (int i = MoodLevelColors.Length - 1; i >= 0; i--)
        {
            ui.Batch.Rect(x, y + 5, 12, 12, MoodLevelColors[i]);
            ui.Text(x + 20, y, GameRules.MoodNames[i], Theme.TextDim);
            string share = total > 0 ? $"{stats.PopulationByMood[i]:N0} ({stats.PopulationByMood[i] / total:P0})" : "-";
            ui.Text(x + w - ui.Font.Measure(share, FontSize.Normal), y, share);
            y += 24;
        }
    }

    private static void Heading(Ui ui, float x, ref float y, string text)
    {
        ui.Text(x, y, text, Theme.Accent, FontSize.Normal, bold: true);
        y += 28;
    }

    private static void Row(Ui ui, float x, ref float y, float w, string label, string value, Rgba? color = null)
    {
        ui.Text(x, y, label, Theme.TextDim);
        ui.Text(x + w - ui.Font.Measure(value, FontSize.Normal), y, value, color ?? Theme.Text);
        y += 24;
    }

    // ------------------------------------------------------------------ cities

    private void Cities(Ui ui, Rect r)
    {
        (string Title, float Width)[] columns =
            [("Ciudad", 190), ("Población", 150), ("Humor", 130), ("Fertilidad", 100), ("Fiestas", 170), ("Reclutar", 210), ("", 60)];
        var cities = _session.Cities.Where(c => c.OwnerId == _player.Id);
        var rows = Sort(NationTab.Cities, cities, c => Map.Provinces[c.ProvinceId], c => c.Name).ToList();
        if (rows.Count == 0)
        {
            ui.Text(r.X, r.Y, "Aún no tienes ciudades. Funda una con tus colonos.", Theme.TextDim);
            return;
        }

        float y = Header(ui, r, NationTab.Cities, columns, sortable: 4);
        foreach (var (city, rowY) in Rows(ui, r, y, rows))
        {
            var p = Map.Provinces[city.ProvinceId];
            float x = r.X + 8;
            bool capital = _player.CapitalCityId == city.Id;
            ui.Text(x, rowY + 6, city.Name, capital ? Theme.Accent : Theme.Text, bold: capital);
            x += columns[0].Width;
            ProvinceCells(ui, ref x, rowY, p, columns);

            var festival = _session.CanHoldFestival(city);
            string label = city.HasFestival(_session.Date.Hours)
                ? $"Quedan {GameSession.FormatHours(city.FestivalUntilHours - _session.Date.Hours)}"
                : $"Celebrar ({GameRules.FestivalCost(p.Population):N0} oro)";
            string tip = $"+{GameRules.FestivalMood:0} al humor de la ciudad durante {GameRules.FestivalDays} días." + (festival.Ok ? "" : "\n" + festival.Message);
            if (ui.Button(new Rect(x, rowY + 3, columns[4].Width - 10, RowHeight - 6), label, festival.Ok, tooltip: tip, size: FontSize.Small))
                _show(_session.HoldFestival(_player.Id, city.Id));
            x += columns[4].Width;

            float bw = (columns[5].Width - 14) / 2;
            var settlers = _session.CanRecruitSettlers(city);
            string settlersTip = $"{GameRules.StartingCitizens} ciudadanos salen a fundar otra ciudad. Coste: {GameRules.SettlersCost}." +
                                 (settlers.Ok ? "" : "\n" + settlers.Message);
            if (ui.Button(new Rect(x, rowY + 3, bw, RowHeight - 6), "Colonos", settlers.Ok, tooltip: settlersTip, size: FontSize.Small))
                _show(_session.RecruitSettlers(_player.Id, city.Id));
            x += bw + 4;
            var warriors = BattalionType.Warriors.Info();
            var train = _session.CanTrain(city, BattalionType.Warriors);
            string trainTip = $"Entrena {Formations.BattalionName(warriors, _player.ArmyEra).ToLowerInvariant()} ({warriors.Men} hombres). Coste: {warriors.Cost}. " +
                              $"Tarda {warriors.TrainingDays} días." + (city.Training.Count > 0 ? $"\nEn instrucción: {city.Training.Count}." : "") +
                              (train.Ok ? "" : "\n" + train.Message);
            if (ui.Button(new Rect(x, rowY + 3, bw, RowHeight - 6), warriors.Name, train.Ok, tooltip: trainTip, size: FontSize.Small))
                _show(_session.Train(_player.Id, city.Id, BattalionType.Warriors));
            x = r.X + 8 + columns.Take(6).Sum(c => c.Width);
            ViewButton(ui, x, rowY, columns[6].Width, p.Id);
        }
    }

    // ------------------------------------------------------------------ provinces

    private void Provinces(Ui ui, Rect r)
    {
        (string Title, float Width)[] columns =
            [("Provincia", 190), ("Población", 150), ("Humor", 130), ("Fertilidad", 100), ("Terreno", 150), ("En camino", 110), ("", 60)];
        var incoming = _session.Migrations.Where(m => m.OwnerId == _player.Id)
            .GroupBy(m => m.ToProvinceId).ToDictionary(g => g.Key, g => g.Sum(m => m.People));
        var provinces = _player.Provinces.Select(id => Map.Provinces[id]);
        var rows = Sort(NationTab.Provinces, provinces, p => p, ProvinceName).ToList();

        float y = Header(ui, r, NationTab.Provinces, columns, sortable: 4);
        foreach (var (p, rowY) in Rows(ui, r, y, rows))
        {
            float x = r.X + 8;
            ui.Text(x, rowY + 6, ProvinceName(p), p.CityId.HasValue ? Theme.Accent : Theme.Text);
            x += columns[0].Width;
            ProvinceCells(ui, ref x, rowY, p, columns);
            ui.Text(x, rowY + 6, p.Info.Name, Theme.TextDim);
            x += columns[4].Width;
            int people = incoming.GetValueOrDefault(p.Id);
            ui.Text(x, rowY + 6, people > 0 ? $"{people:N0}" : "-", people > 0 ? Theme.Text : Theme.TextDim);
            x += columns[5].Width;
            ViewButton(ui, x, rowY, columns[6].Width, p.Id);
        }
    }

    /// <summary>A city's name, or the province's terrain and number for the countryside.</summary>
    private string ProvinceName(Province p) => _session.CityIn(p)?.Name ?? $"{p.Info.Name} {p.Id}";

    // ------------------------------------------------------------------ science

    private void Science(Ui ui, Rect r)
    {
        float y = r.Y;
        double perDay = _session.SciencePerDay(_player);
        ui.Text(r.X, y, $"Ciencia: {perDay:0.##} puntos al día", Theme.Text, FontSize.Normal, bold: true);
        if (ui.Hover(new Rect(r.X, y, 300, 24)))
            ui.Tooltip($"Cada ciudad aporta {GameRules.ScienceBasePerCity:0.#} puntos más {GameRules.SciencePerCityCitizen * 1000:0.#} por cada mil habitantes, " +
                       $"multiplicado por su humor." + (_player.Bonuses.Science > 0 ? $"\nAvances: +{_player.Bonuses.Science:P0}." : ""));
        y += 30;

        if (_player.Researching is Tech current)
        {
            var info = current.Info();
            double done = _player.ResearchProgress[(int)current];
            string eta = perDay > 0 ? $" · unos {GameSession.FormatHours(Math.Ceiling((info.Cost - done) / perDay) * 24)}" : "";
            ui.Text(r.X, y, $"Investigando {info.Name}: {done:0} / {info.Cost:0}{eta}", Theme.Accent);
            y += 26;
            ProgressBar(ui, new Rect(r.X, y, Math.Min(520, r.W), 12), done / info.Cost, Theme.Accent);
            y += 24;
        }
        else
        {
            ui.Text(r.X, y, "No estás investigando nada: elige un avance." +
                (_player.SpareScience >= 1 ? $" Tienes {_player.SpareScience:0} puntos guardados para él." : ""), Theme.Accent);
            y += 36;
        }

        const float CardH = 172, Gap = 12;
        const int PerRow = 5;
        float cardW = (r.W - Gap * (PerRow - 1)) / PerRow;
        int perRow = PerRow;
        for (int i = 0; i < Techs.All.Length; i++)
        {
            var card = new Rect(r.X + i % perRow * (cardW + Gap), y + i / perRow * (CardH + Gap), cardW, CardH);
            TechCard(ui, card, Techs.All[i]);
        }
    }

    /// <summary>One advance: its state, cost, effect, requirements, the buildings it unlocks and the button to research it.</summary>
    private void TechCard(Ui ui, Rect c, Tech tech)
    {
        var info = tech.Info();
        bool known = _player.Techs.Contains(tech), current = _player.Researching == tech;
        var can = _session.CanResearch(_player, tech);
        var border = known ? Theme.Good : current ? Theme.Accent : can.Ok ? Theme.PanelBorder : Theme.ButtonDisabled;
        ui.Batch.Rect(c.X, c.Y, c.W, c.H, Theme.Button.WithAlpha(known ? 0.25f : 0.55f));
        ui.Batch.Outline(c.X, c.Y, c.W, c.H, border);

        float x = c.X + 10, y = c.Y + 8;
        ui.Text(x, y, info.Name, known ? Theme.Good : current ? Theme.Accent : can.Ok ? Theme.Text : Theme.TextDisabled, bold: true);
        double done = _player.ResearchProgress[(int)tech];
        string cost = known ? "Descubierto" : done > 0 ? $"{done:0} / {info.Cost:0}" : $"{info.Cost:0} puntos";
        y += 22;
        ui.Text(x, y, cost, Theme.TextDim, FontSize.Small);
        y += 20;
        foreach (var line in ui.Font.Wrap(info.Description, c.W - 20, FontSize.Small).Take(2))
        {
            ui.Text(x, y, line, known || can.Ok ? Theme.Text : Theme.TextDim, FontSize.Small);
            y += ui.Font.LineHeight(FontSize.Small);
        }
        if (info.Requires.Length > 0)
            ui.Text(x, c.Y + 84, "Requiere: " + string.Join(", ", info.Requires.Select(t => t.Info().Name)),
                info.Requires.All(_player.Techs.Contains) ? Theme.TextDim : Theme.Bad, FontSize.Small);
        // Buildings and battalions stay hidden until their advance is known, so the card says what it brings.
        var unlocks = Buildings.All.Where(b => b.Info().RequiresTech == tech).Select(b => b.Info().Name)
            .Concat(Battalions.All.Where(b => b.Info().Requires.Contains(tech)).Select(b => b.Info().Name)).ToList();
        float uy = c.Y + 102;
        if (unlocks.Count > 0)
            foreach (var line in ui.Font.Wrap("Permite: " + string.Join(", ", unlocks), c.W - 20, FontSize.Small).Take(2))
            {
                ui.Text(x, uy, line, known || can.Ok ? Theme.Accent : Theme.TextDim, FontSize.Small);
                uy += ui.Font.LineHeight(FontSize.Small);
            }

        if (known) return;
        if (!current && done > 0) ProgressBar(ui, new Rect(x, c.Bottom - 40, c.W - 20, 4), done / info.Cost, Theme.TextDim);
        var button = new Rect(x, c.Bottom - 32, c.W - 20, 26);
        if (current) ProgressBar(ui, new Rect(x, c.Bottom - 20, c.W - 20, 8), done / info.Cost, Theme.Accent);
        else if (ui.Button(button, "Investigar", can.Ok, tooltip: can.Ok ? null : can.Message, size: FontSize.Small))
            _show(_session.Research(_player.Id, tech));
    }

    private static void ProgressBar(Ui ui, Rect r, double share, Rgba color)
    {
        ui.Batch.Rect(r.X, r.Y, r.W, r.H, Theme.ButtonDisabled);
        ui.Batch.Rect(r.X, r.Y, (float)(r.W * Math.Clamp(share, 0, 1)), r.H, color);
    }

    // ------------------------------------------------------------------ table helpers

    /// <summary>Population, mood and fertility cells, shared by both tables (columns 1 to 3).</summary>
    private void ProvinceCells(Ui ui, ref float x, float rowY, Province p, (string Title, float Width)[] columns)
    {
        double capacity = _session.CapacityOf(p);
        ui.Text(x, rowY + 6, $"{p.Population:N0}", p.Population > capacity ? Theme.Bad : Theme.Text);
        ui.Text(x + ui.Font.Measure($"{p.Population:N0} ", FontSize.Normal), rowY + 8, $"/ {Compact(capacity)}", Theme.TextDim, FontSize.Small);
        x += columns[1].Width;

        bool populated = p.Population >= 1;
        ui.Text(x, rowY + 6, populated ? $"{p.Mood:0} {GameRules.MoodName(p.Mood)}" : "-", populated ? Theme.Mood(p.Mood, Theme.Text) : Theme.TextDim);
        if (populated && ui.Hover(new Rect(x, rowY, columns[2].Width, RowHeight)))
        {
            var factors = _session.MoodFactors(p).Select(f => $"{f.Points:+0;-0;0}  {f.Reason}");
            ui.Tooltip($"Tiende a {_session.TargetMood(p):0}.\n" + string.Join("\n", factors));
        }
        x += columns[2].Width;

        ui.Text(x, rowY + 6, populated ? $"{p.Fertility:P0}" : "-",
            !populated ? Theme.TextDim : p.Fertility < 0.75 ? Theme.Bad : p.Fertility >= 1.15 ? Theme.Good : Theme.Text);
        x += columns[3].Width;
    }

    private void ViewButton(Ui ui, float x, float rowY, float w, int provinceId)
    {
        if (ui.Button(new Rect(x, rowY + 3, w - 6, RowHeight - 6), "Ver", tooltip: "Mostrar en el mapa", size: FontSize.Small))
        {
            Visible = false;
            _viewProvince(provinceId);
        }
    }

    /// <summary>Draws the column titles; the first <paramref name="sortable"/> sort the table when clicked. Returns where rows start.</summary>
    private float Header(Ui ui, Rect r, NationTab tab, (string Title, float Width)[] columns, int sortable)
    {
        int t = (int)tab;
        float x = r.X;
        for (int i = 0; i < columns.Length; i++)
        {
            var (title, width) = columns[i];
            if (i < sortable)
            {
                string arrow = _sortColumn[t] == i ? (_sortAscending[t] ? " ^" : " v") : "";
                if (ui.Button(new Rect(x, r.Y, width - 6, 28), title + arrow, active: _sortColumn[t] == i, tooltip: "Ordenar", size: FontSize.Small))
                {
                    // Names read best A→Z; numbers largest first.
                    _sortAscending[t] = _sortColumn[t] == i ? !_sortAscending[t] : i == 0;
                    _sortColumn[t] = i;
                }
            }
            else ui.Text(x + 8, r.Y + 5, title, Theme.TextDim, FontSize.Small);
            x += width;
        }
        return r.Y + 36;
    }

    /// <summary>Orders rows by the tab's sort column: name, population, mood or fertility.</summary>
    private IEnumerable<T> Sort<T>(NationTab tab, IEnumerable<T> rows, Func<T, Province> province, Func<T, string> name)
    {
        int t = (int)tab;
        if (_sortColumn[t] == 0)
            return _sortAscending[t] ? rows.OrderBy(name, StringComparer.CurrentCulture) : rows.OrderByDescending(name, StringComparer.CurrentCulture);
        Func<T, double> key = _sortColumn[t] switch
        {
            1 => row => province(row).Population,
            2 => row => province(row).Mood,
            _ => row => province(row).Fertility,
        };
        return _sortAscending[t] ? rows.OrderBy(key) : rows.OrderByDescending(key);
    }

    /// <summary>Scrolls the rows with the mouse wheel and yields those that fit in view, with their y.</summary>
    private IEnumerable<(T Row, float Y)> Rows<T>(Ui ui, Rect r, float top, List<T> rows)
    {
        int t = (int)Tab;
        float visible = r.Bottom - top;
        if (ui.Hover(r)) _scroll[t] -= ui.Input.Scroll * RowHeight * 3;
        _scroll[t] = Math.Clamp(_scroll[t], 0, Math.Max(0, rows.Count * RowHeight - visible));

        int first = (int)(_scroll[t] / RowHeight);
        for (int i = first; i < rows.Count; i++)
        {
            float y = top + i * RowHeight - _scroll[t];
            if (y < top - 0.5f) continue;
            if (y + RowHeight > r.Bottom) break;
            if (i % 2 == 0) ui.Batch.Rect(r.X, y, r.W, RowHeight, Theme.Button.WithAlpha(0.35f));
            if (ui.Hover(new Rect(r.X, y, r.W, RowHeight))) ui.Batch.Outline(r.X, y, r.W, RowHeight, Theme.PanelBorder);
            yield return (rows[i], y);
        }
        if (rows.Count * RowHeight > visible)
        {
            float barH = visible * visible / (rows.Count * RowHeight);
            float barY = top + (visible - barH) * _scroll[t] / (rows.Count * RowHeight - visible);
            ui.Batch.Rect(r.Right - 4, barY, 4, barH, Theme.PanelBorder);
        }
    }

    private static string Compact(double value) =>
        value >= 1_000_000 ? $"{value / 1_000_000:0.#}M" : value >= 10_000 ? $"{value / 1000:0.#}k" : $"{value:N0}";
}
