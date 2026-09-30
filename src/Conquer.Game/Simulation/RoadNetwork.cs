using Conquer.Game.Economy;
using Conquer.Game.Science;

namespace Conquer.Game.Simulation;

/// <summary>What joins two neighbouring provinces: a road, or a railway (which counts as a road too).</summary>
public enum RoadKind
{
    Road = 1,
    Railway = 2,
}

/// <param name="CostPerLink">Paid up front for each stretch between two neighbouring provinces still to be laid.</param>
/// <param name="DaysPerLink">Days of engineers' work for each stretch (<see cref="Rules.MilitaryRules.EngineerWorkDays"/> per battalion a day).</param>
/// <param name="Speed">Travel along it goes this many times faster.</param>
/// <param name="Feminine">For Spanish: «la carretera», «el ferrocarril».</param>
public sealed record RoadInfo(string Name, string Plural, ResourceCost CostPerLink, int DaysPerLink, Tech Requires, double Speed, bool Feminine);

public static class RoadKinds
{
    public static readonly RoadKind[] All = [RoadKind.Road, RoadKind.Railway];

    private static readonly Dictionary<RoadKind, RoadInfo> Table = new()
    {
        [RoadKind.Road] = new("Carretera", "Carreteras", new ResourceCost((ResourceType.Wood, 20), (ResourceType.Gold, 8)), 10, Tech.Engineering, 1.5, true),
        [RoadKind.Railway] = new("Ferrocarril", "Ferrocarriles",
            new ResourceCost((ResourceType.Wood, 25), (ResourceType.Gold, 20), (ResourceType.Iron, 15), (ResourceType.Coal, 5)), 15, Tech.Railroad, 2, false),
    };

    public static RoadInfo Info(this RoadKind kind) => Table[kind];
}

/// <summary>
/// The roads and railways of the world: links between neighbouring provinces, whoever built them. They speed up
/// travel (<see cref="Pathfinder.StepHours"/>) and carry supply (<see cref="GameSession.IsSupplied"/>).
/// </summary>
public sealed class RoadNetwork
{
    private readonly Dictionary<long, RoadKind> _links = [];

    /// <summary>Goes up every time a link is laid, so the map knows when to redraw.</summary>
    public int Version { get; private set; }

    public int Count => _links.Count;

    private static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

    /// <summary>The best link between two neighbours, if any.</summary>
    public RoadKind? Between(int a, int b) => _links.TryGetValue(Key(a, b), out var kind) ? kind : null;

    /// <summary>Whether two neighbours are joined by at least this kind of link (a railway serves as a road).</summary>
    public bool Has(int a, int b, RoadKind kind) => Between(a, b) >= kind;

    /// <summary>Lays a link, or upgrades a road to a railway; never downgrades.</summary>
    internal void Lay(int a, int b, RoadKind kind)
    {
        if (Has(a, b, kind)) return;
        _links[Key(a, b)] = kind;
        Version++;
    }

    internal void Clear()
    {
        _links.Clear();
        Version++;
    }

    public IEnumerable<(int A, int B, RoadKind Kind)> Links =>
        _links.Select(l => ((int)(l.Key >> 32), (int)(l.Key & 0xFFFFFFFF), l.Value));
}

/// <summary>
/// A road or railway being laid by a nation's engineers along <see cref="Route"/>, one stretch after another from
/// its start. Work goes on while engineers of the builder stand anywhere along the route.
/// </summary>
public sealed class RoadProject
{
    public int Id { get; }
    public int OwnerId { get; }
    public RoadKind Kind { get; }
    /// <summary>Provinces from the start to the end, both included.</summary>
    public IReadOnlyList<int> Route { get; }
    /// <summary>Days of work each stretch takes this nation (fewer with advances that speed up building).</summary>
    public int DaysPerLink { get; }
    /// <summary>The stretch being laid: from Route[Next] to Route[Next + 1].</summary>
    public int Next { get; internal set; }
    /// <summary>Days of work left on that stretch.</summary>
    public double WorkLeft { get; internal set; }

    public RoadProject(int id, int ownerId, RoadKind kind, IReadOnlyList<int> route, int daysPerLink, int next, double workLeft)
    {
        Id = id;
        OwnerId = ownerId;
        Kind = kind;
        Route = route;
        DaysPerLink = daysPerLink;
        Next = next;
        WorkLeft = workLeft;
    }

    public int From => Route[0];
    public int To => Route[^1];
}

/// <summary>
/// What a new road or railway would take: its route (start and end included), how many stretches are still to be
/// laid, what they cost, the days of work they need, the cities it would join on the way and the hours an army
/// marches along the route today.
/// </summary>
public sealed record RoadPlan(List<int> Route, int NewLinks, ResourceCost Cost, int WorkDays, List<int> CitiesOnTheWay, double Hours);
