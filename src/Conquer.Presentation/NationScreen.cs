using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Presentation;

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
/// The nation screen: an overview of the player's people and economy, tables of their cities, provinces, army and
/// neighbours with the actions that apply to each, the science tree and the unit designer. Time keeps running while it is open.
/// </summary>
public sealed partial class NationScreen
{
    public static readonly string[] TabNames = ["Resumen", "Ciudades", "Provincias", "Ciencia", "Ejército", "Plantillas", "Diplomacia"];

    private readonly GameController _game;
    private readonly int[] _sortColumn = new int[TabNames.Length];
    private readonly bool[] _sortAscending = new bool[TabNames.Length];

    public bool Visible { get; set; }
    public NationTab Tab { get; set; }

    public NationScreen(GameController game)
    {
        _game = game;
        // Tables start sorted by population, largest first.
        _sortColumn[(int)NationTab.Cities] = _sortColumn[(int)NationTab.Provinces] = 1;
    }

    private GameSession Session => _game.Session;
    private Player Player => _game.Human;
    private WorldMap Map => Session.Map;

    public string Title => Player.Name;

    /// <summary>What the current tab shows.</summary>
    public NationPage Page() => Tab switch
    {
        NationTab.Cities => Cities(),
        NationTab.Provinces => Provinces(),
        NationTab.Science => Science(),
        NationTab.Army => Army(),
        NationTab.Templates => Templates(),
        NationTab.Diplomacy => Diplomacy(),
        _ => Summary(),
    };

    private void Show(CommandResult result) => _game.Show(result);

    /// <summary>Closes the screen and shows the province on the map.</summary>
    private void ViewProvince(int provinceId)
    {
        Visible = false;
        _game.ViewProvince(provinceId);
    }

    private void ViewUnit(int unitId)
    {
        Visible = false;
        _game.ViewUnit(unitId);
    }

    // ------------------------------------------------------------------ summary

    private static Heading Title2(string text) => new(text, Tone.Accent, Height: 28);

    private SummaryPage Summary()
    {
        var stats = Session.Stats(Player);
        var left = new Document();
        left.Add(Title2("Población"));
        left.Add(new Pair("Total", $"{stats.Total:N0}"));
        left.Add(new Pair("En sus provincias", $"{stats.Settled:N0}"));
        left.Add(new Pair("En unidades", $"{stats.InUnits:N0}"));
        left.Add(new Pair("Migrando", $"{stats.Migrating:N0}"));
        left.Add(new Pair("Fertilidad media", $"{stats.AverageFertility:P0}",
            stats.AverageFertility < 0.75 ? Tone.Bad : stats.AverageFertility >= 1.15 ? Tone.Good : Tone.Normal));
        left.Add(new Pair("Nacimientos al día", $"+{stats.DailyBirths:0.#}", Player.IsStarving ? Tone.Bad : Tone.Normal));
        left.Add(new Space(10));

        left.Add(Title2("Moral"));
        left.Add(new Pair("Moral media", $"{stats.AverageMood:0} · {GameRules.MoodName(stats.AverageMood)}", Ink.Mood(stats.AverageMood, Tone.Normal)));
        left.Add(MoodDistribution(stats));
        left.Add(new Space(10));

        left.Add(Title2("Territorio"));
        string capital = Player.CapitalCityId is int id && Session.CityById(id) is { } c ? c.Name : "Ninguna";
        left.Add(new Pair("Capital", capital));
        left.Add(new Pair("Provincias", $"{stats.Provinces:N0}"));
        left.Add(new Pair("Ciudades", $"{stats.Cities:N0}"));
        left.Add(new Pair("Unidades", $"{stats.Units:N0}"));
        left.Add(new Space(10));

        left.Add(Title2("Ciencia"));
        left.Add(new Pair("Puntos por día", $"{Player.LastDayScience:0.##}"));
        foreach (var branch in Techs.Branches)
            left.Add(new Pair($"{branch.Name()} ({Player.ScienceShare(branch):P0})", Player.Researching[(int)branch] is Tech current
                ? $"{current.Info().Name} ({Player.ResearchProgress[(int)current] / Session.ResearchCost(Player, current):P0})"
                : "Nada", Player.Researching[(int)branch] is null ? Tone.Accent : Tone.Normal));
        foreach (var institution in Institutions.All)
        {
            bool adopted = Player.Institutions.Contains(institution), born = Session.IsBorn(institution);
            left.Add(new Pair(institution.Info().Name, adopted ? "Adoptado" : born ? $"{Session.InstitutionShare(Player, institution):P0} de tu población" : "Sin nacer",
                adopted ? Tone.Good : Tone.Normal));
        }

        var right = new Document();
        right.Add(Title2("Comida"));
        double food = Player.Stockpile[ResourceType.Food];
        double foodNet = Player.LastDayNet[(int)ResourceType.Food];
        right.Add(new Pair("Almacén", $"{food:N0}", Player.IsStarving ? Tone.Bad : Tone.Normal));
        right.Add(new Pair("Balance diario", $"{foodNet:+#,0.#;-#,0.#;0}", foodNet < 0 ? Tone.Bad : Tone.Good));
        right.Add(new Pair("Reservas", Player.IsStarving ? "¡Hambre!" : $"{Player.FoodReserveDays:N0} días",
            Player.FoodReserveDays >= GameRules.FoodReserveFullDays ? Tone.Good : Player.FoodReserveDays < 7 ? Tone.Bad : Tone.Normal,
            $"Días que dura la comida al consumo actual. Con {GameRules.FoodReserveFullDays:0} días o más la población gana +{GameRules.FoodReserveMood:0} de moral."));
        right.Add(new Space(10));

        right.Add(Title2("Recursos"));
        float[] at = [310, 200, 90];
        right.Add(new Columns("", [new("Almacén", Tone.Dim), new("Por día", Tone.Dim),
            new("En bolsas", Tone.Dim, "Lo que queda en los yacimientos de tus provincias. Cada bolsa se agota al explotarla.")], at, TextSize.Small, 22));
        foreach (var res in Resources.All.Where(res => res != ResourceType.Food && Player.Knows(res)))
        {
            double net = Player.LastDayNet[(int)res];
            bool mined = Resources.Deposits.Contains(res);
            right.Add(new Columns(res.Name(),
            [
                new($"{Player.Stockpile[res]:N0}"),
                new(Math.Abs(net) < 0.005 ? "-" : $"{net:+#,0.##;-#,0.##}", net > 0 ? Tone.Good : net < 0 ? Tone.Bad : Tone.Dim),
                new(mined ? TextFormat.Compact(stats.Reserves[(int)res]) : "-", mined && stats.Reserves[(int)res] > 0 ? Tone.Normal : Tone.Dim),
            ], at));
        }
        return new SummaryPage(left, right);
    }

    /// <summary>How many citizens live at each mood level, worst first.</summary>
    private static Distribution MoodDistribution(NationStats stats)
    {
        Ink[] inks = [Tone.Bad, Tone.Uneasy, Tone.Calm, Tone.Good];
        double total = stats.PopulationByMood.Sum();
        return new Distribution(inks.Select((ink, i) => new Share(total > 0 ? stats.PopulationByMood[i] / total : 0, ink, GameRules.MoodNames[i],
            total > 0 ? $"{stats.PopulationByMood[i]:N0} ({stats.PopulationByMood[i] / total:P0})" : "-")).ToList());
    }

    // ------------------------------------------------------------------ cities and provinces

    private TablePage Cities()
    {
        Column[] columns = [new("Ciudad", 190), new("Población", 150), new("Moral", 130), new("Fertilidad", 100), new("Fiestas", 170), new("Reclutar", 210), new("", 60)];
        var cities = Session.Cities.Where(c => c.OwnerId == Player.Id);
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var city in Sort(NationTab.Cities, cities, c => Map.Provinces[c.ProvinceId], c => c.Name))
        {
            var p = Map.Provinces[city.ProvinceId];
            bool capital = Player.CapitalCityId == city.Id;
            var cells = new List<Cell> { new TextCell(city.Name, capital ? Tone.Accent : Tone.Normal, Bold: capital) };
            cells.AddRange(ProvinceCells(p));

            var festival = Session.CanHoldFestival(city);
            string label = city.HasFestival(Session.Date.Hours)
                ? $"Quedan {GameSession.FormatHours(city.FestivalUntilHours - Session.Date.Hours)}"
                : $"Celebrar ({GameRules.FestivalCost(p.Population):N0} oro)";
            string tip = $"+{GameRules.FestivalMood:0} a la moral de la ciudad durante {GameRules.FestivalDays} días." + (festival.Ok ? "" : "\n" + festival.Message);
            cells.Add(new ButtonsCell([new Button(label, () => Show(Session.HoldFestival(Player.Id, city.Id)), festival.Ok, Tooltip: tip, Size: TextSize.Small)], Inset: 10));

            var settlers = Session.CanRecruitSettlers(city);
            string settlersTip = $"{GameRules.SettlerCitizens} ciudadanos salen a fundar otra ciudad. Coste: {GameRules.SettlersCost}." +
                                 (settlers.Ok ? "" : "\n" + settlers.Message);
            var warriors = BattalionType.Warriors.Info();
            var train = Session.CanTrain(p, BattalionType.Warriors);
            string trainTip = $"Entrena {Formations.BattalionName(warriors).ToLowerInvariant()} ({warriors.Men} hombres). Coste: {warriors.Cost}. " +
                              TextFormat.TrainingDaysText(GameSession.TrainingDays(Player, BattalionType.Warriors), warriors.TrainingDays) +
                              (p.Training.Count > 0 ? $"\nEn instrucción: {p.Training.Count}." : "") + (train.Ok ? "" : "\n" + train.Message);
            cells.Add(new ButtonsCell(
            [
                new Button("Colonos", () => Show(Session.RecruitSettlers(Player.Id, city.Id)), settlers.Ok, Tooltip: settlersTip, Size: TextSize.Small),
                new Button(warriors.Name, () => Show(Session.Train(Player.Id, p.Id, BattalionType.Warriors)), train.Ok, Tooltip: trainTip, Size: TextSize.Small),
            ], Inset: 10));
            cells.Add(ViewButton(p.Id));
            rows.Add(cells);
        }
        return new TablePage(SortableTable(NationTab.Cities, columns, rows, "Aún no tienes ciudades. Funda una con tus colonos."));
    }

    private TablePage Provinces()
    {
        Column[] columns = [new("Provincia", 190), new("Población", 150), new("Moral", 130), new("Fertilidad", 90), new("Terreno", 130), new("En camino", 90), new("En curso", 190), new("", 60)];
        var incoming = Session.Migrations.Where(m => m.OwnerId == Player.Id)
            .GroupBy(m => m.ToProvinceId).ToDictionary(g => g.Key, g => g.Sum(m => m.People));
        var provinces = Player.Provinces.Select(id => Map.Provinces[id]);
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var p in Sort(NationTab.Provinces, provinces, p => p, ProvinceName))
        {
            var cells = new List<Cell> { new TextCell(ProvinceName(p), p.CityId.HasValue ? Tone.Accent : Tone.Normal) };
            cells.AddRange(ProvinceCells(p));
            cells.Add(new TextCell(p.Info.Name, Tone.Dim));
            int people = incoming.GetValueOrDefault(p.Id);
            cells.Add(new TextCell(people > 0 ? $"{people:N0}" : "-", people > 0 ? Tone.Normal : Tone.Dim));
            cells.Add(Work(p));
            cells.Add(ViewButton(p.Id));
            rows.Add(cells);
        }
        return new TablePage(SortableTable(NationTab.Provinces, columns, rows));
    }

    /// <summary>
    /// What the province is building and training: the first job with its days left, a bar with its
    /// progress and how many more there are, with all of them in the tooltip; a dash when nothing.
    /// </summary>
    private static TextCell Work(Province p)
    {
        var jobs = new List<(string Kind, string Name, int DaysLeft, int TotalDays)>();
        if (p.Constructing is BuildingType building)
            jobs.Add(("Obras", building.Info().Name, p.ConstructionDaysLeft, building.Info().Days));
        else if (p.PlannedCityName != null)
            jobs.Add(("Obras", $"Ciudad de {p.PlannedCityName}", p.ConstructionDaysLeft, GameRules.CityBuildingDays));
        jobs.AddRange(p.Training.Select(o => ("Instrucción", o.Name, o.DaysLeft, o.TotalDays)));
        if (jobs.Count == 0) return new TextCell("-", Tone.Dim);

        var first = jobs[0];
        return new TextCell(first.Name, Tone.Accent, Top: 4,
            Suffix: $" · {first.DaysLeft} d" + (jobs.Count > 1 ? $" (+{jobs.Count - 1})" : ""),
            Tooltip: string.Join("\n", jobs.Select(j => $"{j.Kind}: {j.Name}, quedan {j.DaysLeft} de {j.TotalDays} días.")),
            Bar: new CellBar(1 - first.DaysLeft / (double)first.TotalDays, Tone.Accent, 26, 3, 14));
    }

    /// <summary>A city's name, or the province's terrain and number for the countryside.</summary>
    private string ProvinceName(Province p) => Session.CityIn(p) is { } city ? $"{city.Name} ({p.DisplayName})" : p.DisplayName;

    /// <summary>Population, mood and fertility cells, shared by both tables.</summary>
    private IEnumerable<Cell> ProvinceCells(Province p)
    {
        double capacity = Session.CapacityOf(p);
        yield return new TextCell($"{p.Population:N0}", p.Population > capacity ? Tone.Bad : Tone.Normal, Suffix: $" / {TextFormat.Compact(capacity)}");
        bool populated = p.Population >= 1;
        yield return new TextCell(populated ? $"{p.Mood:0} {GameRules.MoodName(p.Mood)}" : "-", populated ? Ink.Mood(p.Mood, Tone.Normal) : Tone.Dim,
            Tooltip: populated ? $"Tiende a {Session.TargetMood(p):0}.\n" + string.Join("\n", Session.MoodFactors(p).Select(f => $"{f.Points:+0;-0;0}  {f.Reason}")) : null);
        yield return new TextCell(populated ? $"{p.Fertility:P0}" : "-",
            !populated ? Tone.Dim : p.Fertility < 0.75 ? Tone.Bad : p.Fertility >= 1.15 ? Tone.Good : Tone.Normal,
            Tooltip: populated ? $"Nacimientos: +{Session.DailyBirths(p, Player.IsStarving):0.##} al día." : null);
    }

    private ButtonsCell ViewButton(int provinceId) =>
        new([new Button("Ver", () => ViewProvince(provinceId), Tooltip: "Mostrar en el mapa", Size: TextSize.Small)]);

    /// <summary>A table whose first four columns (name, population, mood, fertility) sort it.</summary>
    private Table SortableTable(NationTab tab, Column[] columns, List<IReadOnlyList<Cell>> rows, string? empty = null)
    {
        int t = (int)tab;
        return new Table(columns, rows, 4, _sortColumn[t], _sortAscending[t], column =>
        {
            // Names read best A→Z; numbers largest first.
            _sortAscending[t] = _sortColumn[t] == column ? !_sortAscending[t] : column == 0;
            _sortColumn[t] = column;
        }, empty);
    }

    /// <summary>Orders rows by the tab's sort column: name, population, mood or fertility.</summary>
    private IEnumerable<T> Sort<T>(NationTab tab, IEnumerable<T> rows, Func<T, Province> province, Func<T, string> name)
    {
        int t = (int)tab;
        if (_sortColumn[t] == 0)
        {
            Func<T, string> sortName = row => TextFormat.SpanishSortKey(name(row));
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
}
