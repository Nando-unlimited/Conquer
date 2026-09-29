using Conquer.Game.Buildings;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Travel through the province graph. Without a rule of its own a traveller goes over land only;
/// a rule (<c>canEnter</c>) decides everything, the sea included, as it does for nations that sail and
/// for aircraft. Polar ice can be crossed although it cannot be claimed. A step between neighbours
/// takes the distance between their centres divided by the walking speed, scaled by how easy both
/// terrains are to cross and how fast their roads are; ships sail faster.
/// </summary>
public sealed class Pathfinder
{
    private readonly WorldMap _map;

    public Pathfinder(WorldMap map) => _map = map;

    /// <summary>Whether land travellers (migrants, and anyone without a rule of their own) may enter the province.</summary>
    public bool CanEnter(int province) => !_map.Provinces[province].IsWater;

    private bool Allowed(int province, Func<int, bool>? canEnter) => canEnter?.Invoke(province) ?? CanEnter(province);

    public double StepHours(int from, int to)
    {
        var a = _map.Provinces[from];
        var b = _map.Provinces[to];
        double speed = GameRules.CitizenSpeedKmh * (Speed(a) + Speed(b)) / 2;
        return _map.DistanceKm(a, b) / speed;
    }

    /// <summary>How fast a province is crossed: its terrain, sped up by its roads and railways; the sea at sailing speed.</summary>
    private static double Speed(Province p) => p.IsWater ? GameRules.SailingSpeed : p.Info.MoveSpeed * (1 + p.BuildingBonuses.MoveSpeed);

    /// <summary>The fastest a province can be crossed: the best terrain with every road-like building on it, or the sea.</summary>
    private static readonly double FastestSpeed = Math.Max(GameRules.SailingSpeed,
        Enum.GetValues<Biome>().Max(b => b.Info().MoveSpeed) * (1 + Enum.GetValues<BuildingType>().Sum(b => b.Info().Effects.MoveSpeed)));

    /// <summary>
    /// Fastest route (A*), excluding the start province, or null when unreachable. <paramref name="canEnter"/>
    /// is where this traveller may go (not a foreign country at peace, for an army; the sea, for a nation that sails).
    /// </summary>
    public (List<int> Path, double Hours)? FindPath(int from, int to, Func<int, bool>? canEnter = null)
    {
        if (from == to) return ([], 0);
        if (!Allowed(to, canEnter)) return null;
        int n = _map.Provinces.Count;
        var cost = new double[n];
        Array.Fill(cost, double.PositiveInfinity);
        var previous = new int[n];
        var open = new PriorityQueue<int, double>();
        cost[from] = 0;
        open.Enqueue(from, Heuristic(from, to));

        while (open.TryDequeue(out int current, out double priority))
        {
            if (current == to) break;
            if (priority - Heuristic(current, to) > cost[current] + 1e-9) continue;
            foreach (int next in _map.Provinces[current].Neighbors)
            {
                if (!Allowed(next, canEnter)) continue;
                double c = cost[current] + StepHours(current, next);
                if (c >= cost[next]) continue;
                cost[next] = c;
                previous[next] = current;
                open.Enqueue(next, c + Heuristic(next, to));
            }
        }

        if (double.IsPositiveInfinity(cost[to])) return null;
        var path = new List<int>();
        for (int p = to; p != from; p = previous[p]) path.Add(p);
        path.Reverse();
        return (path, cost[to]);
    }

    /// <summary>
    /// Travel hours from the nearest of several sources to every province (Dijkstra), and which source
    /// that is. Provinces beyond <paramref name="maxHours"/> stay at infinity. With
    /// <paramref name="targets"/>, the search stops once all of them are settled; only their results are then final. <paramref name="canEnter"/>, as in <see cref="FindPath"/>,
    /// is where it may go.
    /// </summary>
    public (double[] Hours, int[] Source) FromSources(IEnumerable<int> sources, double maxHours = double.PositiveInfinity,
        IReadOnlySet<int>? targets = null, Func<int, bool>? canEnter = null)
    {
        int remaining = targets?.Count ?? -1;
        int n = _map.Provinces.Count;
        var hours = new double[n];
        var source = new int[n];
        Array.Fill(hours, double.PositiveInfinity);
        Array.Fill(source, -1);
        var open = new PriorityQueue<int, double>();
        foreach (int s in sources)
        {
            hours[s] = 0;
            source[s] = s;
            open.Enqueue(s, 0);
        }

        while (open.TryDequeue(out int current, out double h))
        {
            if (h > hours[current]) continue;
            if (targets != null && targets.Contains(current) && --remaining == 0) break;
            foreach (int next in _map.Provinces[current].Neighbors)
            {
                if (!Allowed(next, canEnter)) continue;
                double c = h + StepHours(current, next);
                if (c >= hours[next] || c > maxHours) continue;
                hours[next] = c;
                source[next] = source[current];
                open.Enqueue(next, c);
            }
        }
        return (hours, source);
    }

    // Straight-line distance at the fastest possible pace never overestimates.
    private double Heuristic(int from, int to) =>
        _map.DistanceKm(_map.Provinces[from], _map.Provinces[to]) / (GameRules.CitizenSpeedKmh * FastestSpeed);
}
