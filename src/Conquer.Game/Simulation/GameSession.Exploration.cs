using Conquer.Game.Entities;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Scouts left to explore on their own: each walks to the best free province along its nation's borders,
/// claims it and looks for the next, until no free land is left within reach.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>How far, in hours of a citizen's walk, scouts look for free land.</summary>
    private const double AutoClaimSearchHours = 24 * 30;

    public CommandResult CanAutoClaim(Unit unit) =>
        unit.IsScouting ? CommandResult.Success() : CommandResult.Fail("Solo las unidades de exploradores exploran solas.");

    /// <summary>Lets the scouts explore and claim land on their own, or hands them back to the player (they keep any path they have).</summary>
    public CommandResult SetAutoClaim(int playerId, int unitId, bool on)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        if (on)
        {
            var check = CanAutoClaim(unit);
            if (!check.Ok) return check;
        }
        unit.AutoClaim = on;
        if (!on) return CommandResult.Success();
        return AutoClaim(unit, quiet: true)
            ? CommandResult.Success($"{unit.Name} explora y reclama tierra libre por su cuenta.")
            : CommandResult.Fail("No queda tierra libre a su alcance junto a tus fronteras.");
    }

    /// <summary>Every idle exploring unit claims the province it stands in, if it can, and heads for the next.</summary>
    private void AutoClaimUnits()
    {
        foreach (var unit in Units.Where(u => u.AutoClaim).ToList())
            AutoClaim(unit, quiet: false);
    }

    /// <summary>Returns whether the unit goes on exploring; when it runs out of land it stops, saying so unless <paramref name="quiet"/>.</summary>
    private bool AutoClaim(Unit unit, bool quiet)
    {
        if (unit.IsMoving || unit.AttackingProvinceId.HasValue || unit.IsAboard) return true;
        if (!unit.IsScouting) return unit.AutoClaim = false;
        var player = Players[unit.OwnerId];
        if (CanClaim(unit).Ok)
        {
            var here = Map.Provinces[unit.ProvinceId];
            SetOwner(here, unit.OwnerId);
            if (player.IsHuman) Notify(unit.OwnerId, $"{unit.Name} reclama la provincia y la llama {here.Name}.");
        }

        if (NextAutoClaimTarget(unit) is { } path)
        {
            unit.Path.Clear();
            unit.Path.AddRange(path);
            unit.StepHours = unit.HoursToNext = UnitStepHours(unit, unit.ProvinceId, path[0]);
            return true;
        }
        unit.AutoClaim = false;
        if (player.IsHuman && !quiet) Notify(unit.OwnerId, $"{unit.Name}: no queda tierra libre a su alcance junto a tus fronteras. Deja de explorar.");
        return false;
    }

    /// <summary>
    /// The way to the free province along the nation's borders (or next to the unit, while it has none) that is best
    /// to live in for how far it is, through free or own land only, skipping those other scouts are already heading for.
    /// </summary>
    private List<int>? NextAutoClaimTarget(Unit unit)
    {
        var player = Players[unit.OwnerId];
        var taken = Units.Where(u => u.AutoClaim && u != unit && u.OwnerId == unit.OwnerId && u.Destination.HasValue)
            .Select(u => u.Destination!.Value).ToHashSet();
        IEnumerable<int> border = player.Provinces.Count > 0 ? player.Provinces : [unit.ProvinceId];
        var targets = border.SelectMany(id => Map.Provinces[id].Neighbors)
            .Where(id => Map.Provinces[id] is { IsOwned: false, IsClaimable: true } && !taken.Contains(id)).ToHashSet();
        if (targets.Count == 0) return null;

        bool CanEnter(int id) => CanUnitEnter(unit, id) && Map.Provinces[id] is var p && (!p.IsOwned || p.ControllerId == unit.OwnerId);
        var (hours, _) = Pathfinder.FromSources([unit.ProvinceId], AutoClaimSearchHours, targets, CanEnter);
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

    /// <summary>How many people the province's land can feed, roughly: what makes it worth claiming.</summary>
    private static double LandScore(Province p) => 1 + Math.Min(p.Capacity, 40000) * p.FoodYield / 100;
}
