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
    Templates,
    Diplomacy,
}

/// <summary>
/// The nation screen: an overview of the player's people and economy, and tables of their cities and
/// provinces with the actions that apply to each. Time keeps running while it is open.
/// </summary>
public sealed partial class NationView
{
    private const float RowHeight = 34;
    private static readonly string[] TabNames = ["Resumen", "Ciudades", "Provincias", "Ciencia", "Ejército", "Plantillas", "Diplomacia"];
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
    /// <summary>The age the science tab shows; null for the first one with advances left to discover.</summary>
    private Era? _scienceEra;

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
        ui.Panel(area, opaque: true); // opaque: the map would clutter the tables
        ui.Text(area.X + 20, area.Y + 14, _player.Name, Theme.Accent, FontSize.Large, bold: true);
        float tx = area.X + 40 + ui.Font.Measure(_player.Name, FontSize.Large, true);
        for (int i = 0; i < TabNames.Length; i++)
            if (ui.Button(new Rect(tx + i * 108, area.Y + 12, 102, 32), TabNames[i], active: (int)Tab == i)) Tab = (NationTab)i;
        if (ui.Button(new Rect(area.Right - 120, area.Y + 12, 100, 32), "Cerrar", tooltip: "Cerrar (N o Esc)")) Visible = false;

        var content = new Rect(area.X + 20, area.Y + 60, area.W - 40, area.H - 76);
        switch (Tab)
        {
            case NationTab.Summary: Summary(ui, content); break;
            case NationTab.Cities: Cities(ui, content); break;
            case NationTab.Provinces: Provinces(ui, content); break;
            case NationTab.Science: Science(ui, content); break;
            case NationTab.Army: Army(ui, content); break;
            case NationTab.Templates: Templates(ui, content); break;
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
        Row(ui, x, ref y, colW, "Nacimientos al día", $"+{stats.DailyBirths:0.#}", _player.IsStarving ? Theme.Bad : Theme.Text);
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
        foreach (var branch in Techs.Branches)
            Row(ui, x, ref y, colW, $"{branch.Name()} ({_player.ScienceShare(branch):P0})", _player.Researching[(int)branch] is Tech current
                ? $"{current.Info().Name} ({_player.ResearchProgress[(int)current] / _session.ResearchCost(_player, current):P0})"
                : "Nada", _player.Researching[(int)branch] is null ? Theme.Accent : Theme.Text);
        foreach (var institution in Institutions.All)
        {
            bool adopted = _player.Institutions.Contains(institution), born = _session.IsBorn(institution);
            Row(ui, x, ref y, colW, institution.Info().Name, adopted ? "Adoptado" : born ? $"{_session.InstitutionShare(_player, institution):P0} de tu población" : "Sin nacer",
                adopted ? Theme.Good : Theme.Text);
        }

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
            string settlersTip = $"{GameRules.SettlerCitizens} ciudadanos salen a fundar otra ciudad. Coste: {GameRules.SettlersCost}." +
                                 (settlers.Ok ? "" : "\n" + settlers.Message);
            if (ui.Button(new Rect(x, rowY + 3, bw, RowHeight - 6), "Colonos", settlers.Ok, tooltip: settlersTip, size: FontSize.Small))
                _show(_session.RecruitSettlers(_player.Id, city.Id));
            x += bw + 4;
            var warriors = BattalionType.Warriors.Info();
            var train = _session.CanTrain(p, BattalionType.Warriors);
            string trainTip = $"Entrena {Formations.BattalionName(warriors).ToLowerInvariant()} ({warriors.Men} hombres). Coste: {warriors.Cost}. " +
                              TrainingDaysText(GameSession.TrainingDays(_player, BattalionType.Warriors), warriors.TrainingDays) + (p.Training.Count > 0 ? $"\nEn instrucción: {p.Training.Count}." : "") +
                              (train.Ok ? "" : "\n" + train.Message);
            if (ui.Button(new Rect(x, rowY + 3, bw, RowHeight - 6), warriors.Name, train.Ok, tooltip: trainTip, size: FontSize.Small))
                _show(_session.Train(_player.Id, p.Id, BattalionType.Warriors));
            x = r.X + 8 + columns.Take(6).Sum(c => c.Width);
            ViewButton(ui, x, rowY, columns[6].Width, p.Id);
        }
    }

    // ------------------------------------------------------------------ provinces

    private void Provinces(Ui ui, Rect r)
    {
        (string Title, float Width)[] columns =
            [("Provincia", 190), ("Población", 150), ("Humor", 130), ("Fertilidad", 90), ("Terreno", 130), ("En camino", 90), ("En curso", 190), ("", 60)];
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
            Work(ui, x, rowY, columns[6].Width - 14, p);
            x += columns[6].Width;
            ViewButton(ui, x, rowY, columns[7].Width, p.Id);
        }
    }

    /// <summary>
    /// What the province is building and training: the first job with its days left, a bar with its
    /// progress and how many more there are, with all of them in the tooltip; a dash when nothing.
    /// </summary>
    private void Work(Ui ui, float x, float rowY, float w, Province p)
    {
        var jobs = new List<(string Kind, string Name, int DaysLeft, int TotalDays)>();
        if (p.Constructing is BuildingType building)
            jobs.Add(("Obras", building.Info().Name, p.ConstructionDaysLeft, building.Info().Days));
        else if (p.PlannedCityName != null)
            jobs.Add(("Obras", $"Ciudad de {p.PlannedCityName}", p.ConstructionDaysLeft, GameRules.CityBuildingDays));
        jobs.AddRange(p.Training.Select(o => ("Instrucción", o.Name, o.DaysLeft, o.TotalDays)));
        if (jobs.Count == 0)
        {
            ui.Text(x, rowY + 6, "-", Theme.TextDim);
            return;
        }

        var first = jobs[0];
        string left = $" · {first.DaysLeft} d" + (jobs.Count > 1 ? $" (+{jobs.Count - 1})" : "");
        string shown = ui.Font.Wrap(first.Name, w - ui.Font.Measure(left, FontSize.Small), FontSize.Normal).First();
        ui.Text(x, rowY + 4, shown, Theme.Accent);
        ui.Text(x + ui.Font.Measure(shown, FontSize.Normal), rowY + 6, left, Theme.TextDim, FontSize.Small);
        ProgressBar(ui, new Rect(x, rowY + RowHeight - 8, w, 3), 1 - first.DaysLeft / (double)first.TotalDays, Theme.Accent);
        if (ui.Hover(new Rect(x, rowY, w, RowHeight)))
            ui.Tooltip(string.Join("\n", jobs.Select(j => $"{j.Kind}: {j.Name}, quedan {j.DaysLeft} de {j.TotalDays} días.")));
    }

    /// <summary>A city's name, or the province's terrain and number for the countryside.</summary>
    private string ProvinceName(Province p) => _session.CityIn(p) is { } city ? $"{city.Name} ({p.DisplayName})" : p.DisplayName;

    // ------------------------------------------------------------------ science

    /// <summary>The three branches side by side: each with its priority, what it is researching and its advances, level by level.</summary>
    private void Science(Ui ui, Rect r)
    {
        float y = r.Y;
        double perDay = _session.SciencePerDay(_player);
        string points = $"Ciencia: {perDay:0.##} puntos al día" + (_player.SpareScience >= 1 ? $" · {_player.SpareScience:0} guardados" : "");
        ui.Text(r.X, y, points, _player.SpareScience >= 1 ? Theme.Accent : Theme.Text, FontSize.Normal, bold: true);
        if (ui.Hover(new Rect(r.X, y, ui.Font.Measure(points, FontSize.Normal, true), 24)))
            ui.Tooltip($"Cada ciudad aporta {GameRules.ScienceBasePerCity:0.#} puntos más {GameRules.SciencePerCityCitizen * 1000:0.#} por cada mil habitantes, " +
                       $"multiplicado por su humor." + (_player.Bonuses.Science > 0 ? $"\nAvances: +{_player.Bonuses.Science:P0}." : "") +
                       "\nSe reparte entre las ramas según su prioridad. Si una rama no investiga nada, su parte va a las demás." +
                       $"\nCada vecino que ya conoce un avance te lo abarata un {GameRules.NeighbourResearchDiscount:P0} (hasta {GameRules.MaxNeighbourDiscounts})." +
                       (_player.SpareScience >= 1 ? "\nLos puntos guardados entran en el próximo avance que elijas." : ""));

        // One age at a time: a button for each on the second row, and the institution that opens the one shown on the right.
        var eras = Techs.All.Select(t => t.Info().Era).Distinct().Order().ToList();
        var shown = _scienceEra ?? eras.FirstOrDefault(e => Techs.All.Any(t => t.Info().Era == e && !_player.Techs.Contains(t)), eras[^1]);
        float ix = r.Right;
        foreach (var institution in Institutions.All.Where(i => i.Info().Opens == shown)) ix = InstitutionStatus(ui, ix, y - 4, institution) - 24;
        y += 30;
        float ex = r.X;
        foreach (var era in eras)
        {
            double multiplier = GameSession.EraCostMultiplier(_player, era);
            string? tip = multiplier > 1 ? $"Sus avances te cuestan un {multiplier - 1:P0} más hasta que adoptes su institución." : null;
            if (ui.Button(new Rect(ex, y, 130, 26), era.Name() + (multiplier > 1 ? " (+)" : ""), active: era == shown, tooltip: tip, size: FontSize.Small))
                _scienceEra = era;
            ex += 136;
        }
        y += 38;

        const float Gap = 16;
        float colW = (r.W - Gap * 2) / 3;
        var neighbours = _session.NeighbourNations(_player);
        foreach (var branch in Techs.Branches)
            Branch(ui, new Rect(r.X + (int)branch * (colW + Gap), y, colW, r.Bottom - y), branch, shown, perDay, neighbours);
    }

    /// <summary>An institution, right-aligned at <paramref name="right"/>: adopted, how far it has spread and the button to adopt it, or not yet born.</summary>
    /// <returns>Where its left edge ended up.</returns>
    private float InstitutionStatus(Ui ui, float right, float y, Institution institution)
    {
        var info = institution.Info();
        bool adopted = _player.Institutions.Contains(institution), born = _session.IsBorn(institution);
        if (born && !adopted)
        {
            double cost = _session.AdoptionCost(_player, institution);
            var can = _session.CanAdopt(_player, institution);
            var button = new Rect(right - 150, y, 150, 28);
            if (ui.Button(button, $"Adoptar ({cost:N0} oro)", can.Ok, tooltip: can.Ok ? info.Description : can.Message, size: FontSize.Small))
                _show(_session.Adopt(_player.Id, institution));
            right -= 158;
        }
        string text = adopted ? $"{info.Name}: adoptado" : born ? $"{info.Name}: {_session.InstitutionShare(_player, institution):P0} de tu población"
            : $"{info.Name}: aún no ha nacido";
        float w = ui.Font.Measure(text, FontSize.Small);
        ui.Text(right - w, y + 7, text, adopted ? Theme.Good : born ? Theme.Accent : Theme.TextDim, FontSize.Small);
        if (ui.Hover(new Rect(right - w, y, w, 28)))
            ui.Tooltip($"{info.Birth}\n{info.Description}\nMientras no lo adoptes, los avances de la era {info.Opens.Name()} cuestan un " +
                       $"{GameRules.InstitutionPenalty:P0} más. Se adopta al llegar a la {GameRules.InstitutionAdoptionShare:P0} de tu población, o antes pagando oro.");
        return right - w;
    }

    private void Branch(Ui ui, Rect r, TechBranch branch, Era era, double perDay, IReadOnlySet<int> neighbours)
    {
        float x = r.X, y = r.Y;
        ui.Text(x, y, branch.Name(), Theme.Accent, FontSize.Large, bold: true);

        // Priority: - n + and the share of science it brings.
        int priority = _player.ResearchPriorities[(int)branch];
        float px = r.Right - 170;
        if (ui.Button(new Rect(px, y, 30, 28), "-", priority > 0, tooltip: "Menos prioridad"))
            _show(_session.SetResearchPriority(_player.Id, branch, priority - 1));
        ui.TextCentered(new Rect(px + 30, y, 34, 28), priority.ToString());
        if (ui.Button(new Rect(px + 64, y, 30, 28), "+", priority < GameRules.MaxResearchPriority, tooltip: "Más prioridad"))
            _show(_session.SetResearchPriority(_player.Id, branch, priority + 1));
        ui.Text(px + 102, y + 5, $"{_player.ScienceShare(branch):P0}", Theme.TextDim);
        y += 40;

        if (_player.Researching[(int)branch] is Tech current)
        {
            double cost = _session.ResearchCost(_player, current, neighbours), done = _player.ResearchProgress[(int)current];
            double rate = perDay * _player.ScienceShare(branch);
            string eta = rate > 0 ? $"unos {GameSession.FormatHours(Math.Ceiling((cost - done) / rate) * 24)}" : "sin ciencia";
            ui.Text(x, y, $"Investigando {current.Info().Name}: {done:0} / {cost:0} · {eta}", Theme.Text, FontSize.Small);
            y += 20;
            ProgressBar(ui, new Rect(x, y, r.W, 8), done / cost, Theme.Accent);
            y += 18;
        }
        else
        {
            bool done = Techs.InBranch(branch).All(_player.Techs.Contains);
            bool any = Techs.InBranch(branch).Any(t => GameSession.CanResearch(_player, t).Ok);
            ui.Text(x, y, done ? "Rama completa: su ciencia va a las demás." : any ? "Elige qué investigar: mientras, su ciencia va a las demás."
                : "Nada disponible: faltan avances de otras ramas.", done ? Theme.Good : any ? Theme.Accent : Theme.TextDim, FontSize.Small);
            y += 30;
        }

        const float CardH = 88, CardGap = 6;
        foreach (int level in Techs.InBranch(branch).Where(t => t.Info().Era == era).Select(t => t.Info().Level).Distinct())
        {
            bool open = GameSession.IsLevelOpen(_player, branch, level);
            string title = $"Nivel {level}";
            if (!open) title += $" · se abre con {Techs.NeededToOpenNext(branch, level - 1)} avance del nivel {level - 1}";
            else if (level < Techs.Levels(branch) && Techs.InLevel(branch, level).Count() > 1)
                title += $" · {Techs.NeededToOpenNext(branch, level)} de {Techs.InLevel(branch, level).Count()} abren el siguiente";
            ui.Text(x, y, title, open ? Theme.TextDim : Theme.TextDisabled, FontSize.Small);
            y += 16;
            foreach (var tech in Techs.InLevel(branch, level))
            {
                TechCard(ui, new Rect(x, y, r.W, CardH), tech, neighbours);
                y += CardH + CardGap;
            }
        }
    }

    /// <summary>One advance: its state, cost, effect, the advances it needs, what it unlocks and the button to research it.</summary>
    private void TechCard(Ui ui, Rect c, Tech tech, IReadOnlySet<int> neighbours)
    {
        var info = tech.Info();
        bool known = _player.Techs.Contains(tech), current = _player.Researching[(int)info.Branch] == tech;
        var can = GameSession.CanResearch(_player, tech);
        var border = known ? Theme.Good : current ? Theme.Accent : can.Ok ? Theme.PanelBorder : Theme.ButtonDisabled;
        ui.Batch.RoundedRect(c.X, c.Y, c.W, c.H, Theme.ButtonRadius, Theme.ButtonTop.WithAlpha(known ? 0.3f : 0.7f), Theme.ButtonBottom.WithAlpha(known ? 0.3f : 0.7f));
        ui.Batch.RoundedOutline(c.X, c.Y, c.W, c.H, Theme.ButtonRadius, border);

        float x = c.X + 10, y = c.Y + 6;
        ui.Text(x, y, info.Name, known ? Theme.Good : current ? Theme.Accent : can.Ok ? Theme.Text : Theme.TextDisabled, bold: true);
        double cost = _session.ResearchCost(_player, tech, neighbours), done = _player.ResearchProgress[(int)tech];
        string state = known ? "Descubierto" : (done > 0 ? $"{done:0} / {cost:0}" : $"{cost:0} puntos") +
                       (cost < info.Cost ? $" (-{1 - cost / info.Cost:P0})" : cost > info.Cost ? $" (+{cost / info.Cost - 1:P0})" : "");
        float right = c.Right - 10;
        // Choosing it replaces what the branch was researching; the points already in either one stay.
        if (!known && !current)
        {
            if (ui.Button(new Rect(right - 84, y - 2, 84, 24), "Investigar", can.Ok, tooltip: can.Ok ? null : can.Message, size: FontSize.Small))
                _show(_session.Research(_player.Id, tech));
            right -= 92;
        }
        ui.Text(right - ui.Font.Measure(state, FontSize.Small), y + 3, state, Theme.TextDim, FontSize.Small);
        y += 24;
        foreach (var line in ui.Font.Wrap(info.Description, c.W - 20, FontSize.Small).Take(2))
        {
            ui.Text(x, y, line, known || can.Ok ? Theme.Text : Theme.TextDim, FontSize.Small);
            y += ui.Font.LineHeight(FontSize.Small);
        }
        // Buildings and battalions stay hidden until their advance is known, so the card says what it brings.
        var unlocks = Buildings.All.Where(b => b.Info().RequiresTech == tech).Select(b => b.Info().Name)
            .Concat(Battalions.All.Where(b => b.Info().Requires.Contains(tech)).Select(b => b.Info().Name))
            .Concat(RoadKinds.All.Where(r => r.Info().Requires == tech).Select(r => r.Info().Plural)).ToList();
        var notes = new List<(string Text, Rgba Color)>();
        if (info.Requires.Length > 0)
            notes.Add(("Requiere: " + string.Join(", ", info.Requires.Select(t => t.Info().Name)),
                info.Requires.All(_player.Techs.Contains) ? Theme.TextDim : Theme.Bad));
        if (unlocks.Count > 0) notes.Add(("Permite: " + string.Join(", ", unlocks), known ? Theme.TextDim : Theme.Accent));
        float ny = c.Bottom - 8 - notes.Count * ui.Font.LineHeight(FontSize.Small);
        foreach (var (text, color) in notes)
        {
            ui.Text(x, ny, ui.Font.Wrap(text, c.W - 20, FontSize.Small).First(), color, FontSize.Small);
            ny += ui.Font.LineHeight(FontSize.Small);
        }
        if (!known && done > 0) ProgressBar(ui, new Rect(c.X + 1, c.Bottom - 4, (c.W - 2), 3), done / cost, current ? Theme.Accent : Theme.TextDim);
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
        if (populated && ui.Hover(new Rect(x, rowY, columns[3].Width, RowHeight)))
            ui.Tooltip($"Nacimientos: +{_session.DailyBirths(p, _player.IsStarving):0.##} al día.");
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

    /// <summary>
    /// Key that sorts names in Spanish alphabetical order with plain ordinal comparison (the game runs
    /// without ICU, so culture-aware comparison is not available): case and accents are ignored and ñ goes after n.
    /// </summary>
    internal static string SpanishSortKey(string name)
    {
        var key = new System.Text.StringBuilder(name.Length + 2);
        foreach (char ch in name)
        {
            char c = char.ToLowerInvariant(ch);
            switch (c)
            {
                case 'à' or 'á' or 'â' or 'ã' or 'ä' or 'å': key.Append('a'); break;
                case 'ç': key.Append('c'); break;
                case 'è' or 'é' or 'ê' or 'ë': key.Append('e'); break;
                case 'ì' or 'í' or 'î' or 'ï': key.Append('i'); break;
                case 'ò' or 'ó' or 'ô' or 'õ' or 'ö': key.Append('o'); break;
                case 'ù' or 'ú' or 'û' or 'ü': key.Append('u'); break;
                case 'ý' or 'ÿ': key.Append('y'); break;
                // '~' sorts after every letter, so "ñ" lands between "nz" and "o".
                case 'ñ': key.Append("n~"); break;
                default: key.Append(c); break;
            }
        }
        return key.ToString();
    }

    /// <summary>Orders rows by the tab's sort column: name, population, mood or fertility.</summary>
    private IEnumerable<T> Sort<T>(NationTab tab, IEnumerable<T> rows, Func<T, Province> province, Func<T, string> name)
    {
        int t = (int)tab;
        if (_sortColumn[t] == 0)
        {
            Func<T, string> sortName = row => SpanishSortKey(name(row));
            return _sortAscending[t] ? rows.OrderBy(sortName, StringComparer.Ordinal) : rows.OrderByDescending(sortName, StringComparer.Ordinal);
        }
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
