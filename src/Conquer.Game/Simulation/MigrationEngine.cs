using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// The migration engine: who leaves which city, where they go and what happens when they arrive. People only move
/// within their nation.
/// <list type="bullet">
/// <item>Each day (<see cref="Daily"/>) the cities with little room left send part of their people
/// (<see cref="GameRules.EmigrationShare"/>) in groups of <see cref="GameRules.MigrationGroup"/>. Each province without a
/// city takes them from the nearest city that is sending people, as long as it is emptier than <see cref="Wanted"/>;
/// each group goes to the one that pulls hardest (<see cref="Pull"/>): the emptiest, and among them the nearest.</item>
/// <item>From the industrial age (<see cref="GameRules.UrbanizationEra"/>) the flow turns round: the countryside empties
/// into the cities (<see cref="Urbanize"/>).</item>
/// <item>The player may also force a migration, for gold (<see cref="Force"/>).</item>
/// <item>Migrants walk at citizen speed; on arrival (<see cref="Arrive"/>) they settle and bring their mood, culture and faith.</item>
/// </list>
/// The figures were balanced by simulating a nation of a capital, two cities and ten provinces for 60 years: migrants
/// reach every province almost every year, and migration lasts until the countryside is as full as its cities.
/// </summary>
public sealed class MigrationEngine
{
    private readonly GameSession _session;
    private readonly List<Migration> _inTransit = [];

    internal MigrationEngine(GameSession session) => _session = session;

    private WorldMap Map => _session.Map;

    /// <summary>Every group of migrants on the road, of every nation.</summary>
    public IReadOnlyList<Migration> InTransit => _inTransit;

    /// <summary>The id the next group takes; saved with the game.</summary>
    internal int NextId { get; set; }

    /// <summary>What each city's emigration has left over towards its next group, by its province; saved with the game.</summary>
    internal Dictionary<int, double> Carry { get; } = [];

    /// <summary>People of a nation on the road.</summary>
    public int PeopleOnTheWay(int playerId) => _inTransit.Where(m => m.OwnerId == playerId).Sum(m => m.People);

    /// <summary>People on their way to a province.</summary>
    public int ComingTo(int provinceId) => _inTransit.Where(m => m.ToProvinceId == provinceId).Sum(m => m.People);

    /// <summary>Puts a group on the road as it is (loading a game, or a test).</summary>
    internal void Add(Migration migration) => _inTransit.Add(migration);

    /// <summary>A nation that falls takes its people on the road with it.</summary>
    internal void Forget(int playerId) => _inTransit.RemoveAll(m => m.OwnerId == playerId);

    // ------------------------------------------------------------------ rules

    /// <summary>How full a province is: its people over the people its land feeds.</summary>
    public double Fill(Province p) => p.Population / _session.CapacityOf(p);

    /// <summary>Whether a city is sending people out: not occupied, big enough and short of room (<see cref="GameRules.EmigrationShare"/>).</summary>
    public bool IsEmigrating(Province city) =>
        city.CityId.HasValue && !city.IsOccupied && city.Population > GameRules.MinEmigrationCityPopulation
        && GameRules.EmigrationShare(Fill(city)) > 0;

    /// <summary>
    /// The people a province without a city wants to hold: <see cref="GameRules.MigrationTargetShare"/> of its
    /// capacity, or as full as the city that sends it people if that is fuller. So migration goes on as long as the
    /// cities are fuller than their countryside.
    /// </summary>
    public static double Wanted(double capacity, double sourceFill) => capacity * Math.Max(GameRules.MigrationTargetShare, sourceFill);

    /// <summary>
    /// How hard a province pulls the next group: its gap (<paramref name="deficit"/>) as a share of what it
    /// <paramref name="wanted"/>, a little less the further it is (<see cref="GameRules.MigrationDistanceDays"/>), and
    /// <see cref="GameRules.UnsettledMigrationPull"/> times as hard while nobody lives there yet, so new land gets its
    /// first settlers first.
    /// </summary>
    public static double Pull(double deficit, double wanted, double hours, bool unsettled) =>
        deficit / wanted / (1 + hours / 24 / GameRules.MigrationDistanceDays) * (unsettled ? GameRules.UnsettledMigrationPull : 1);

    // ------------------------------------------------------------------ every day

    /// <summary>A day of a nation's spontaneous migration (see the class summary), or of its urbanization from the industrial age.</summary>
    internal void Daily(Player player)
    {
        if (player.Era >= GameRules.UrbanizationEra)
        {
            Urbanize(player);
            return;
        }
        var sources = player.Provinces.Select(id => Map.Provinces[id]).Where(IsEmigrating).Select(p => p.Id).ToList();
        if (sources.Count == 0) return;
        var unsettled = player.Provinces.Where(id => !Map.Provinces[id].CityId.HasValue).ToHashSet();
        if (unsettled.Count == 0) return;

        var (hours, nearest) = _session.Pathfinder.FromSources(sources, maxHours: 24 * 60, unsettled);
        var incoming = _inTransit.Where(m => m.OwnerId == player.Id)
            .GroupBy(m => m.ToProvinceId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.People));

        var targetsBySource = new Dictionary<int, List<(int Province, double Pull, double Deficit)>>();
        foreach (int id in player.Provinces)
        {
            var p = Map.Provinces[id];
            if (p.CityId.HasValue || p.IsOccupied || nearest[id] < 0) continue;
            double coming = incoming.GetValueOrDefault(id);
            double wanted = Wanted(_session.CapacityOf(p), Fill(Map.Provinces[nearest[id]]));
            double deficit = wanted - p.Population - coming;
            if (deficit < 1) continue;
            if (!targetsBySource.TryGetValue(nearest[id], out var list)) targetsBySource[nearest[id]] = list = [];
            list.Add((id, Pull(deficit, wanted, hours[id], p.Population + coming < GameRules.SettledPopulation), deficit));
        }

        foreach (var (sourceId, targets) in targetsBySource)
        {
            var source = Map.Provinces[sourceId];
            // Emigrants leave in groups; what falls short of one carries over to the next day.
            double quota = Carry.GetValueOrDefault(sourceId) + source.Population * GameRules.EmigrationShare(Fill(source));
            int groups = (int)(Math.Min(quota, source.Population - GameRules.MinEmigrationCityPopulation) / GameRules.MigrationGroup);
            Carry[sourceId] = Math.Min(quota - Math.Max(groups, 0) * GameRules.MigrationGroup, GameRules.MigrationGroup);
            // Each group goes to the strongest pull, which weakens as its gap fills.
            var open = targets.ToList();
            for (int g = 0; g < groups && open.Count > 0; g++)
            {
                int best = 0;
                for (int i = 1; i < open.Count; i++) if (open[i].Pull > open[best].Pull) best = i;
                var (target, pull, deficit) = open[best];
                int people = GameRules.MigrationGroup;
                source.Population -= people;
                long arrive = _session.Date.Hours + Math.Max(1, (long)Math.Ceiling(hours[target]));
                _inTransit.Add(new Migration(NextId++, player.Id, sourceId, target, people, _session.Date.Hours, arrive, forced: false, source.Mood));
                if (deficit <= people) open.RemoveAt(best);
                else open[best] = (target, pull * (deficit - people) / deficit, deficit - people);
            }
        }
    }

    /// <summary>
    /// The people a city wants to hold in an industrial nation, given how full the province that sends them is: up to
    /// <see cref="GameRules.UrbanizationGap"/> fuller than it, and never more than <see cref="GameRules.UrbanizationCityFill"/>.
    /// </summary>
    public static double UrbanWanted(double cityCapacity, double sourceFill) =>
        cityCapacity * Math.Min(GameRules.UrbanizationCityFill, sourceFill + GameRules.UrbanizationGap);

    /// <summary>
    /// Industrial urbanization: the countryside empties into the cities, which industry has made far bigger
    /// (<see cref="GameRules.CityCapacity"/>). Each province without a city sends its share of emigrants
    /// (<see cref="GameRules.UrbanizationShareOf"/>, by how full it is) to its nearest city while that city wants
    /// them (<see cref="UrbanWanted"/>). Cities no longer send people to the countryside.
    /// </summary>
    private void Urbanize(Player player)
    {
        var cities = player.Provinces.Where(id => Map.Provinces[id].CityId.HasValue && !Map.Provinces[id].IsOccupied).ToList();
        var countryside = player.Provinces.Select(id => Map.Provinces[id])
            .Where(p => !p.CityId.HasValue && !p.IsOccupied && p.Population > GameRules.MinEmigrationCityPopulation && GameRules.UrbanizationShareOf(Fill(p)) > 0)
            .Select(p => p.Id).ToHashSet();
        if (cities.Count == 0 || countryside.Count == 0) return;

        var (hours, nearest) = _session.Pathfinder.FromSources(cities, maxHours: 24 * 60, countryside);
        var coming = cities.ToDictionary(id => id, id => (double)ComingTo(id));
        foreach (int id in player.Provinces)
        {
            if (!countryside.Contains(id) || nearest[id] < 0) continue;
            var source = Map.Provinces[id];
            int city = nearest[id];
            double quota = Carry.GetValueOrDefault(id) + source.Population * GameRules.UrbanizationShareOf(Fill(source));
            int groups = (int)(Math.Min(quota, source.Population - GameRules.MinEmigrationCityPopulation) / GameRules.MigrationGroup);
            Carry[id] = Math.Min(quota - Math.Max(groups, 0) * GameRules.MigrationGroup, GameRules.MigrationGroup);
            var town = Map.Provinces[city];
            double room = UrbanWanted(_session.CapacityOf(town), Fill(source)) - town.Population - coming[city];
            for (int g = 0; g < groups && room >= GameRules.MigrationGroup; g++)
            {
                int people = GameRules.MigrationGroup;
                source.Population -= people;
                room -= people;
                coming[city] += people;
                long arrive = _session.Date.Hours + Math.Max(1, (long)Math.Ceiling(hours[id]));
                _inTransit.Add(new Migration(NextId++, player.Id, id, city, people, _session.Date.Hours, arrive, forced: false, source.Mood));
            }
        }
    }

    // ------------------------------------------------------------------ every hour

    /// <summary>
    /// The groups that reach their destination settle there. Settlers of the ruler's culture and faith count as
    /// assimilated and converted; if the destination was lost on the way, they settle in the capital.
    /// </summary>
    internal void Arrive()
    {
        for (int i = _inTransit.Count - 1; i >= 0; i--)
        {
            var m = _inTransit[i];
            if (m.ArriveHours > _session.Date.Hours) continue;
            _inTransit.RemoveAt(i);
            var target = Map.Provinces[m.ToProvinceId];
            if (target.OwnerId == m.OwnerId && !target.IsOccupied)
            {
                if (GameSession.HasForeignCulture(target))
                    target.Assimilation = Math.Min(1, (target.Assimilation * target.Population + m.People) / (target.Population + m.People));
                if (_session.HasOtherFaith(target))
                    target.Conversion = Math.Min(1, (target.Conversion * target.Population + m.People) / (target.Population + m.People));
                GameSession.Settle(target, m.People, m.Mood);
                if (m.Forced && m.OwnerId == GameSession.HumanPlayerId)
                    _session.Notify(m.OwnerId, $"{m.People} ciudadanos han llegado a su nuevo hogar.");
            }
            else if (_session.Players[m.OwnerId].CapitalCityId is int capital && _session.CityById(capital) is { } city)
            {
                GameSession.Settle(Map.Provinces[city.ProvinceId], m.People, m.Mood);
            }
        }
    }

    // ------------------------------------------------------------------ forced migration

    public CommandResult CanForce(int playerId, int fromId, int toId, int people)
    {
        var from = Map.Provinces[fromId];
        var to = Map.Provinces[toId];
        if (from.OwnerId != playerId || to.OwnerId != playerId) return CommandResult.Fail("Origen y destino deben ser tuyos.");
        if (from.IsOccupied || to.IsOccupied) return CommandResult.Fail("La provincia está ocupada por el enemigo.");
        if (fromId == toId) return CommandResult.Fail("Elige otra provincia de destino.");
        if (people < 1) return CommandResult.Fail("Elige cuántos ciudadanos.");
        int keep = from.CityId.HasValue ? GameRules.MinCityPopulation : 0;
        if (from.Population - people < keep) return CommandResult.Fail($"Solo pueden irse {Math.Max(0, (int)from.Population - keep)}.");
        double cost = GameRules.ForcedMigrationCost(people);
        if (_session.Players[playerId].Stockpile[ResourceType.Gold] < cost) return CommandResult.Fail($"Cuesta {cost:0} de oro.");
        return CommandResult.Success();
    }

    /// <summary>The player pays gold to send a chosen number of citizens between two of their provinces; they arrive less happy.</summary>
    public CommandResult Force(int playerId, int fromId, int toId, int people)
    {
        var check = CanForce(playerId, fromId, toId, people);
        if (!check.Ok) return check;
        if (_session.Pathfinder.FindPath(fromId, toId) is not { } route) return CommandResult.Fail("No hay camino por tierra hasta allí.");
        double hours = route.Hours;
        _session.Players[playerId].Stockpile[ResourceType.Gold] -= GameRules.ForcedMigrationCost(people);
        Map.Provinces[fromId].Population -= people;
        long arrive = _session.Date.Hours + Math.Max(1, (long)Math.Ceiling(hours));
        double mood = Math.Max(0, Map.Provinces[fromId].Mood - GameRules.ForcedMigrantMoodPenalty);
        _inTransit.Add(new Migration(NextId++, playerId, fromId, toId, people, _session.Date.Hours, arrive, forced: true, mood));
        return CommandResult.Success($"{people} ciudadanos en camino; llegarán en {GameSession.FormatHours(hours)}.");
    }
}
