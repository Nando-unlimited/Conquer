using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Roads and railways: engineers lay them from a city or HQ to another one, along the fastest route, and they
/// speed up travel and carry supply.
/// </summary>
public sealed partial class GameSession
{
    private readonly List<RoadProject> _roadProjects = [];
    private int _nextRoadProjectId;

    public RoadNetwork Roads { get; } = new();
    public IReadOnlyList<RoadProject> RoadProjects => _roadProjects;

    /// <summary>Where a player's roads start and end: their own cities (unless occupied) and their HQs.</summary>
    public bool IsRoadHub(int playerId, int provinceId)
    {
        var p = Map.Provinces[provinceId];
        return p.CityId is int city && CityById(city)!.OwnerId == playerId && !p.IsOccupied
               || Units.Any(u => u.OwnerId == playerId && u.IsHeadquarters && !u.IsAboard && u.ProvinceId == provinceId);
    }

    /// <summary>Every province a player's roads can start or end in.</summary>
    public IEnumerable<int> RoadHubs(int playerId) =>
        Cities.Where(c => c.OwnerId == playerId).Select(c => c.ProvinceId)
            .Concat(Units.Where(u => u.OwnerId == playerId && u.IsHeadquarters && !u.IsAboard).Select(u => u.ProvinceId))
            .Distinct().Where(id => IsRoadHub(playerId, id));

    /// <summary>Land a player's roads may cross: their own, the enemy land they occupy, and land nobody owns.</summary>
    private bool CanLayRoad(int playerId, int provinceId)
    {
        var p = Map.Provinces[provinceId];
        return !p.IsWater && (!p.IsOwned || p.ControllerId == playerId);
    }

    /// <summary>Whether two provinces are joined through the network by links of at least this kind.</summary>
    public bool Connected(int from, int to, RoadKind kind)
    {
        if (from == to) return true;
        var seen = new HashSet<int> { from };
        var queue = new Queue<int>([from]);
        while (queue.TryDequeue(out int current))
            foreach (int next in Map.Provinces[current].Neighbors)
            {
                if (!Roads.Has(current, next, kind) || !seen.Add(next)) continue;
                if (next == to) return true;
                queue.Enqueue(next);
            }
        return false;
    }

    /// <summary>
    /// The road or railway a player's engineers would lay between two provinces: the route an army would march (so it
    /// follows existing roads where they help), through land the player may build on, or null if there is none.
    /// </summary>
    public RoadPlan? PlanRoad(int playerId, int from, int to, RoadKind kind)
    {
        if (from == to) return null;
        if (Pathfinder.FindPath(from, to, id => CanLayRoad(playerId, id)) is not { } found) return null;
        List<int> route = [from, .. found.Path];
        int links = 0;
        for (int i = 0; i + 1 < route.Count; i++)
            if (!Roads.Has(route[i], route[i + 1], kind) && !Planned(playerId, route[i], route[i + 1], kind)) links++;
        var info = kind.Info();
        var cities = route.Skip(1).SkipLast(1).Where(id => Map.Provinces[id].CityId.HasValue).ToList();
        return new RoadPlan(route, links, info.CostPerLink.Times(links), links * BuildDays(Players[playerId], info.DaysPerLink), cities, found.Hours);
    }

    /// <summary>Whether one of the player's works under way will already lay this stretch.</summary>
    private bool Planned(int playerId, int a, int b, RoadKind kind) =>
        _roadProjects.Any(r => r.OwnerId == playerId && r.Kind >= kind && Enumerable.Range(r.Next, r.Route.Count - 1 - r.Next)
            .Any(i => (r.Route[i] == a && r.Route[i + 1] == b) || (r.Route[i] == b && r.Route[i + 1] == a)));

    public CommandResult CanBuildRoad(int playerId, int from, int to, RoadKind kind)
    {
        var info = kind.Info();
        var player = Players[playerId];
        if (!player.Techs.Contains(info.Requires)) return CommandResult.Fail($"Requiere {info.Requires.Info().Name.ToLowerInvariant()}.");
        if (!IsRoadHub(playerId, from)) return CommandResult.Fail("Solo desde una provincia con una ciudad o un cuartel general tuyos.");
        if (EngineersIn(playerId, from) == 0) return CommandResult.Fail("Hacen falta ingenieros en la provincia.");
        if (!IsRoadHub(playerId, to) || to == from) return CommandResult.Fail("Elige otra ciudad o cuartel general tuyos.");
        if (PlanRoad(playerId, from, to, kind) is not { } plan) return CommandResult.Fail("No hay camino por tierra tuya o libre.");
        if (plan.NewLinks == 0) return CommandResult.Fail($"Ya están unidas por {info.Name.ToLowerInvariant()} (o lo estarán con las obras en curso).");
        if (!player.Stockpile.Has(plan.Cost)) return CommandResult.Fail($"Cuesta {plan.Cost}.");
        return CommandResult.Success();
    }

    /// <summary>Pays for the stretches still missing along the route and puts the engineers to work, starting from <paramref name="from"/>.</summary>
    public CommandResult BuildRoad(int playerId, int from, int to, RoadKind kind)
    {
        var check = CanBuildRoad(playerId, from, to, kind);
        if (!check.Ok) return check;
        var plan = PlanRoad(playerId, from, to, kind)!;
        Players[playerId].Stockpile.TrySpend(plan.Cost);
        int days = BuildDays(Players[playerId], kind.Info().DaysPerLink);
        _roadProjects.Add(new RoadProject(_nextRoadProjectId++, playerId, kind, plan.Route, days, 0, days));
        return CommandResult.Success($"{kind.Info().Name} en obras de {PlaceName(Map.Provinces[from])} a {PlaceName(Map.Provinces[to])}: " +
                                     $"{plan.NewLinks} tramos, {plan.WorkDays} días de trabajo de un batallón de ingenieros.");
    }

    /// <summary>Stretches of a work not laid yet (some may have been laid meanwhile by another).</summary>
    public int LinksLeft(RoadProject project) =>
        Enumerable.Range(project.Next, project.Route.Count - 1 - project.Next).Count(i => !Roads.Has(project.Route[i], project.Route[i + 1], project.Kind));

    /// <summary>Stops a work; what was paid for the stretches not laid is given back.</summary>
    public CommandResult CancelRoad(int playerId, int projectId)
    {
        if (_roadProjects.FirstOrDefault(r => r.Id == projectId && r.OwnerId == playerId) is not { } project) return CommandResult.Fail("Obra no válida.");
        _roadProjects.Remove(project);
        foreach (var (type, amount) in project.Kind.Info().CostPerLink.Times(LinksLeft(project)).Items) Players[playerId].Stockpile[type] += amount;
        return CommandResult.Success("Obra cancelada; se devuelve lo que costaban los tramos sin hacer.");
    }

    /// <summary>Battalions of the builder's engineers standing somewhere along a work's route: they are the ones working on it.</summary>
    public int EngineersOn(RoadProject project) => project.Route.Distinct().Sum(id => EngineersIn(project.OwnerId, id));

    /// <summary>
    /// Once a day each work advances by a day of work per battalion of engineers along it, stretch after stretch from
    /// its start; a stretch whose land the builder no longer holds waits. Finished works are announced.
    /// </summary>
    private void DailyRoadWork()
    {
        foreach (var project in _roadProjects.ToList())
        {
            double work = EngineersOn(project) * MilitaryRules.EngineerWorkDays;
            while (work > 0 && project.Next < project.Route.Count - 1)
            {
                int a = project.Route[project.Next], b = project.Route[project.Next + 1];
                if (Roads.Has(a, b, project.Kind))
                {
                    project.Next++;
                    continue;
                }
                if (!CanLayRoad(project.OwnerId, a) || !CanLayRoad(project.OwnerId, b)) break;
                double done = Math.Min(work, project.WorkLeft);
                project.WorkLeft -= done;
                work -= done;
                if (project.WorkLeft > 1e-9) break;
                Roads.Lay(a, b, project.Kind);
                _supplied.Clear();
                project.Next++;
                project.WorkLeft = project.DaysPerLink;
            }
            if (project.Next < project.Route.Count - 1 && LinksLeft(project) > 0) continue;
            _roadProjects.Remove(project);
            if (project.OwnerId == HumanPlayerId)
                Notify(HumanPlayerId, $"{(project.Kind.Info().Feminine ? "Terminada la" : "Terminado el")} {project.Kind.Info().Name.ToLowerInvariant()} de {PlaceName(Map.Provinces[project.From])} a {PlaceName(Map.Provinces[project.To])}.");
        }
    }

    /// <summary>
    /// Provinces a player's supply reaches by road or railway: along links through land they hold (or nobody owns)
    /// from their cities.
    /// </summary>
    private HashSet<int> SupplyNetwork(int playerId, IEnumerable<int> sources)
    {
        var reached = new HashSet<int>(sources);
        var queue = new Queue<int>(reached);
        while (queue.TryDequeue(out int current))
            foreach (int next in Map.Provinces[current].Neighbors)
                if (Roads.Between(current, next) != null && CanLayRoad(playerId, next) && reached.Add(next)) queue.Enqueue(next);
        return reached;
    }
}
