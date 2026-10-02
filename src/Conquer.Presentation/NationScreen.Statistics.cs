using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>The figures the statistics tab can draw.</summary>
public enum StatisticsMetric
{
    Population,
    Soldiers,
    Gold,
    Provinces,
    Science,
}

/// <summary>The Estadísticas tab of the nation screen: how each nation's people, army, gold, land and science have changed.</summary>
public sealed partial class NationScreen
{
    private static readonly string[] MetricNames = ["Población", "Ejército", "Oro al día", "Provincias", "Ciencia al día"];

    public StatisticsMetric Metric { get; set; }

    private static double Value(HistorySample h, StatisticsMetric metric) => metric switch
    {
        StatisticsMetric.Population => h.Population,
        StatisticsMetric.Soldiers => h.Soldiers,
        StatisticsMetric.Gold => h.Gold,
        StatisticsMetric.Provinces => h.Provinces,
        _ => h.Science,
    };

    private StatisticsPage Statistics()
    {
        var metrics = MetricNames.Select((name, i) => new Button(name, () => Metric = (StatisticsMetric)i, Active: (int)Metric == i, Size: TextSize.Small)).ToList();
        string title = $"{MetricNames[(int)Metric]} de cada nación";
        // The ledger, and every standing nation's figures today so the lines reach the present.
        var samples = Session.History.Concat(Session.Players.Where(p => !p.Eliminated).Select(Session.Sample)).ToList();
        long start = samples.Min(h => h.Hours), end = samples.Max(h => h.Hours);
        if (end - start < 24 * 2)
            return new StatisticsPage(metrics, title, "Aún no hay datos: la primera anotación se hace al cabo de un mes.", [], [], []);

        double low = Math.Min(0, samples.Min(h => Value(h, Metric))), high = samples.Max(h => Value(h, Metric));
        if (high - low < 1) high = low + 1;
        float X(long hours) => (float)(hours - start) / (end - start);
        float Y(double value) => (float)((value - low) / (high - low));

        var series = Session.Players
            .Select(p => (Player: p, Points: samples.Where(h => h.PlayerId == p.Id).OrderBy(h => h.Hours).ToList()))
            .Where(s => s.Points.Count > 0)
            .OrderByDescending(s => s.Player.Id == Player.Id).ThenByDescending(s => Value(s.Points[^1], Metric))
            .Select(s => new ChartSeries(s.Player.Eliminated ? $"{s.Player.Name} (eliminada)" : s.Player.Name, s.Player.Color,
                s.Points.Select(h => (X(h.Hours), Y(Value(h, Metric)))).ToList(),
                TextFormat.Compact(Value(s.Points[^1], Metric)), s.Player.Id == Player.Id))
            .ToList();
        var yTicks = Enumerable.Range(0, 5).Select(i => new ChartTick(i / 4f, TextFormat.Compact(low + (high - low) * i / 4))).ToList();
        int firstYear = new GameDate(start).Year, lastYear = new GameDate(end).Year;
        int step = Math.Max(1, (int)Math.Ceiling((lastYear - firstYear) / 6.0));
        var xTicks = new List<ChartTick>();
        for (int year = firstYear; year <= lastYear; year += step)
        {
            long hours = Math.Max(start, (year - 1) * 365L * 24);
            xTicks.Add(new ChartTick(X(hours), $"Año {year}"));
        }
        return new StatisticsPage(metrics, title, null, series, yTicks, xTicks);
    }
}
