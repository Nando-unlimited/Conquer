using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Scouts left to explore on their own. Exploring, each walks to the nearest land its nation has never seen, until none is
/// left within reach. Claiming, each walks to the best free province along its nation's borders, claims it and looks for
/// the next, until no free land is left within reach.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>How far, in hours of a citizen's walk, scouts look for free or unknown land.</summary>
    private const double AutoScoutSearchHours = 24 * 30;

    public CommandResult CanAutoScout(Unit unit) =>
        unit.IsScouting ? CommandResult.Success() : CommandResult.Fail("Solo las unidades de exploradores exploran solas.");

    /// <summary>Lets the scouts explore (and claim land, if so ordered) on their own, or hands them back to the player (they keep any path they have).</summary>
    public CommandResult SetScoutOrders(int playerId, int unitId, ScoutOrders orders)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        if (orders != ScoutOrders.None)
        {
            var check = CanAutoScout(unit);
            if (!check.Ok) return check;
        }
        unit.ScoutOrders = orders;
        if (orders == ScoutOrders.None) return CommandResult.Success();
        // A new order sets off from where the unit stands.
        if (unit.IsMoving && !unit.AttackingProvinceId.HasValue && !unit.IsAboard)
        {
            unit.Path.Clear();
            unit.StepHours = unit.HoursToNext = 0;
        }
        bool going = AutoScout(unit, quiet: true);
        return orders == ScoutOrders.Claim
            ? going ? CommandResult.Success($"{unit.Name} explora y reclama tierra libre por su cuenta.")
                : CommandResult.Fail("No queda tierra libre a su alcance junto a tus fronteras.")
            : going ? CommandResult.Success($"{unit.Name} explora tierras desconocidas por su cuenta.")
                : CommandResult.Fail("No queda tierra desconocida a su alcance.");
    }

    /// <summary>Every exploring unit goes on: claiming the province it stands in, if so ordered, and heading for the next.</summary>
    private void AutoScoutUnits()
    {
        foreach (var unit in Units.Where(u => u.ExploresAlone).ToList())
            AutoScout(unit, quiet: false);
    }

    /// <summary>Returns whether the unit goes on exploring; when it runs out of land it stops, saying so unless <paramref name="quiet"/>.</summary>
    private bool AutoScout(Unit unit, bool quiet)
    {
        if (unit.AttackingProvinceId.HasValue || unit.IsAboard) return true;
        if (!unit.IsScouting)
        {
            unit.ScoutOrders = ScoutOrders.None;
            return false;
        }
        return unit.ScoutOrders == ScoutOrders.Claim ? AutoClaim(unit, quiet) : AutoExplore(unit, quiet);
    }

    private bool AutoClaim(Unit unit, bool quiet)
    {
        if (unit.IsMoving) return true;
        var player = Players[unit.OwnerId];
        if (CanClaim(unit).Ok)
        {
            var here = Map.Provinces[unit.ProvinceId];
            SetOwner(here, unit.OwnerId);
            if (player.IsHuman) Notify(unit.OwnerId, $"{unit.Name} reclama la provincia y la llama {here.Name}.");
        }

        if (NextAutoClaimTarget(unit) is { } path)
        {
            SetScoutPath(unit, path);
            return true;
        }
        unit.ScoutOrders = ScoutOrders.None;
        if (player.IsHuman && !quiet) Notify(unit.OwnerId, $"{unit.Name}: no queda tierra libre a su alcance junto a tus fronteras. Deja de explorar.");
        return false;
    }

    /// <summary>
    /// Heads for the nearest unknown land; once the place it heads for has been seen on the way, it turns to the next,
    /// so it keeps walking on the edge of what the nation knows.
    /// </summary>
    private bool AutoExplore(Unit unit, bool quiet)
    {
        var player = Players[unit.OwnerId];
        VisibleProvinces(unit.OwnerId); // what it sees now counts as explored
        if (unit.Destination is int dest && !player.Explored.Contains(dest)) return true;

        if (NextExploreTarget(unit) is { } path)
        {
            SetScoutPath(unit, path);
            return true;
        }
        if (unit.IsMoving) return true; // it ends the way it was going
        unit.ScoutOrders = ScoutOrders.None;
        if (player.IsHuman && !quiet) Notify(unit.OwnerId, $"{unit.Name}: no queda tierra desconocida a su alcance. Deja de explorar.");
        return false;
    }

    /// <summary>Sets the unit on its way; a step already under way towards the same province carries on where it was.</summary>
    private void SetScoutPath(Unit unit, List<int> path)
    {
        bool sameStep = unit.IsMoving && unit.Path[0] == path[0];
        unit.Path.Clear();
        unit.Path.AddRange(path);
        if (!sameStep) unit.StepHours = unit.HoursToNext = UnitStepHours(unit, unit.ProvinceId, path[0]);
    }

    /// <summary>
    /// The way to the free province along the nation's borders (or next to the unit, while it has none) that is best
    /// to live in for how far it is, through free or own land only, skipping those other scouts are already heading for.
    /// </summary>
    private List<int>? NextAutoClaimTarget(Unit unit)
    {
        var player = Players[unit.OwnerId];
        var taken = ScoutDestinations(unit, ScoutOrders.Claim);
        IEnumerable<int> border = player.Provinces.Count > 0 ? player.Provinces : [unit.ProvinceId];
        var targets = border.SelectMany(id => Map.Provinces[id].Neighbors)
            .Where(id => Map.Provinces[id] is { IsOwned: false, IsClaimable: true } && !taken.Contains(id)).ToHashSet();
        if (targets.Count == 0) return null;

        bool CanEnter(int id) => CanUnitEnter(unit, id) && Map.Provinces[id] is var p && (!p.IsOwned || p.ControllerId == unit.OwnerId);
        var (hours, _) = Pathfinder.FromSources([unit.ProvinceId], AutoScoutSearchHours, targets, CanEnter);
        int best = -1;
        double bestScore = double.MinValue;
        foreach (int id in targets)
        {
            if (double.IsPositiveInfinity(hours[id])) continue;
            double score = LandScore(Map.Provinces[id]) / (1 + hours[id] / 48);
            if (score > bestScore) (best, bestScore) = (id, score);
        }
        return best < 0 ? null : Pathfinder.FindPath(unit.ProvinceId, best, CanEnter)?.Path;
    }

    /// <summary>
    /// The way to the nearest land the nation has never seen, through land it may cross in peace (never an enemy's),
    /// away from where other exploring scouts are heading so they spread out.
    /// </summary>
    private List<int>? NextExploreTarget(Unit unit)
    {
        var player = Players[unit.OwnerId];
        var taken = ScoutDestinations(unit, ScoutOrders.Explore);
        taken.UnionWith(taken.SelectMany(id => Map.Provinces[id].Neighbors).ToList());

        bool CanEnter(int id) => CanUnitEnter(unit, id) && Map.Provinces[id] is var p
            && (!p.IsOwned || p.ControllerId == unit.OwnerId || !AtWar(unit.OwnerId, p.ControllerId));
        int from = unit.IsMoving ? unit.Path[0] : unit.ProvinceId;
        var (hours, _) = Pathfinder.FromSources([from], AutoScoutSearchHours, canEnter: CanEnter);
        int best = -1;
        for (int id = 0; id < hours.Length; id++)
        {
            if (double.IsPositiveInfinity(hours[id]) || player.Explored.Contains(id) || taken.Contains(id)) continue;
            if (best < 0 || hours[id] < hours[best]) best = id;
        }
        if (best < 0 || Pathfinder.FindPath(from, best, CanEnter)?.Path is not { } path) return null;
        // While on the way, it first ends the step it is taking.
        if (from != unit.ProvinceId) path.Insert(0, from);
        return path;
    }

    /// <summary>Where the nation's other scouts with the same orders are heading.</summary>
    private HashSet<int> ScoutDestinations(Unit unit, ScoutOrders orders) =>
        Units.Where(u => u.ScoutOrders == orders && u != unit && u.OwnerId == unit.OwnerId && u.Destination.HasValue)
            .Select(u => u.Destination!.Value).ToHashSet();

    /// <summary>How many people the province's land can feed, roughly: what makes it worth claiming.</summary>
    private static double LandScore(Province p) => 1 + Math.Min(p.Capacity, 40000) * p.FoodYield / 100;
}
