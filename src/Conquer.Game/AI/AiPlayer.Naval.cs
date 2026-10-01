using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Game.AI;

/// <summary>
/// Invasions across the sea. Against an enemy it shares no border with, the rival gathers part of its army in its
/// biggest port, builds transports for it, embarks it, sails to the enemy's least defended coast and lands there;
/// the landed regiments then fight like any other (see AiPlayer.Military.cs). Nothing is remembered between days:
/// each step follows from where the troops and the ships are.
/// </summary>
internal sealed partial class AiPlayer
{
    /// <summary>Regiments it sends across the sea at most, and the share of its army it keeps at home.</summary>
    private const int MaxInvasionRegiments = 4;
    /// <summary>It only looks for enemies overseas this far (in km) from its ports, once its ships cross the ocean.</summary>
    private const double InvasionRangeKm = 8000;
    /// <summary>Without ships that cross the ocean (Cartography), only this far: along the coast and over narrow seas.</summary>
    private const double CoastalInvasionRangeKm = 1500;
    /// <summary>A war across the sea is harder: it asks for this much more advantage.</summary>
    private const double OverseasAppetitePenalty = 0.2;

    /// <summary>Its biggest port city, where invasions gather and set sail.</summary>
    private Province? StagingPort() =>
        _session.Cities.Where(c => c.OwnerId == _player.Id).Select(c => Map.Provinces[c.ProvinceId])
            .Where(p => _session.IsPort(p, _player.Id)).MaxBy(p => p.Population);

    /// <summary>The ship it builds to carry troops: the roomiest transport it can build in the port.</summary>
    private BattalionType? TransportType(Province port) =>
        Battalions.All.Where(t => t.Info().Naval && t.Info().Capacity >= t.Info().Men && _session.CanTrain(port, t).Ok)
            .Cast<BattalionType?>().MaxBy(t => t!.Value.Info().Capacity);

    /// <summary>Whether it can carry an army across the sea: a port, and transports it knows how to build.</summary>
    private bool CanInvade() => StagingPort() is { } port && TransportType(port) != null;

    /// <summary>Nations it has no border with but whose land lies within reach of its ports.</summary>
    private IEnumerable<int> OverseasNeighbours()
    {
        var ports = _session.Cities.Where(c => c.OwnerId == _player.Id).Select(c => Map.Provinces[c.ProvinceId])
            .Where(p => _session.IsPort(p, _player.Id)).ToList();
        if (ports.Count == 0) return [];
        var border = Neighbours().ToHashSet();
        double range = _player.Techs.Contains(Tech.Cartography) ? InvasionRangeKm : CoastalInvasionRangeKm;
        return _session.Players.Where(p => p.Id != _player.Id && !border.Contains(p.Id) && p.Provinces.Count > 0)
            .Where(p => p.Provinces.Any(id => IsCoast(Map.Provinces[id])
                                              && ports.Any(port => Map.DistanceKm(port, Map.Provinces[id]) <= range)))
            .Select(p => p.Id);
    }

    /// <summary>Land with sea next to it.</summary>
    private bool IsCoast(Province p) => !p.IsWater && p.Neighbors.Any(n => Map.Provinces[n].IsWater);

    /// <summary>
    /// Each day of a war with an enemy across the sea: build transports, bring the invasion force and the ships to the
    /// port, embark, set sail for the enemy's weakest coast, land, and send the empty ships home.
    /// </summary>
    private void Invade()
    {
        var border = Neighbours().ToHashSet();
        var enemies = _session.EnemiesOf(_player.Id).Where(e => !border.Contains(e.Id)).Select(e => e.Id).ToHashSet();
        if (enemies.Count == 0 || StagingPort() is not { } port) return;

        var transports = _session.Units.Where(u => u.OwnerId == _player.Id && u.IsFleet && u.Capacity > 0).ToList();
        var aboard = transports.SelectMany(_session.CargoOf).ToList();
        var regiments = Army.Where(u => !u.IsAboard && u.Battalions.Count > 0).ToList();
        int wanted = Math.Min(MaxInvasionRegiments, (regiments.Count + aboard.Count + 1) / 2);

        // The force: whoever is aboard or already in the port, topped up with the nearest idle regiments.
        var force = regiments.Where(u => u.ProvinceId == port.Id || u.IsMoving && u.Path[^1] == port.Id).ToList();
        foreach (var unit in regiments.Except(force).Where(u => !u.IsMoving && u.AttackingProvinceId is null)
                     .OrderBy(u => Map.DistanceKm(Map.Provinces[u.ProvinceId], port)))
        {
            if (force.Count + aboard.Count >= wanted) break;
            if (_session.MoveUnit(_player.Id, unit.Id, port.Id).Ok) force.Add(unit);
        }

        // Enough ships for everyone: one more transport at a time while they fall short.
        int men = force.Sum(u => u.Citizens) + aboard.Sum(u => u.Citizens);
        bool building = port.Training.Any(o => o.Battalion is BattalionType t && t.Info().Naval && t.Info().Capacity > 0);
        if (transports.Sum(f => f.Capacity) < men && !building && TransportType(port) is BattalionType ship && Spare(ship.Info().Cost))
            _session.Train(_player.Id, port.Id, ship);

        foreach (var fleet in transports.Where(f => !f.IsMoving))
        {
            var cargo = _session.CargoOf(fleet).ToList();
            if (cargo.Count == 0)
            {
                if (fleet.ProvinceId != port.Id) _session.MoveUnit(_player.Id, fleet.Id, port.Id);
                else
                    foreach (var unit in force.Where(u => u.ProvinceId == port.Id && !u.IsMoving && !u.IsAboard).ToList())
                        if (_session.Embark(_player.Id, unit.Id, fleet.Id).Ok) force.Remove(unit);
                continue;
            }
            if (fleet.ProvinceId == port.Id)
            {
                // Room left and more troops on the way: wait for them.
                foreach (var unit in force.Where(u => u.ProvinceId == port.Id && !u.IsMoving && !u.IsAboard).ToList())
                    if (_session.Embark(_player.Id, unit.Id, fleet.Id).Ok) force.Remove(unit);
                if (force.Any(u => u.IsMoving) && _session.CargoMen(fleet) < fleet.Capacity) continue;
                // No way to any enemy coast: the troops go ashore again.
                if (!SailToLanding(fleet, enemies))
                    foreach (var unit in _session.CargoOf(fleet).ToList()) _session.Disembark(_player.Id, unit.Id, port.Id);
                continue;
            }
            if (Map.Provinces[fleet.ProvinceId].IsWater && !Land(fleet, cargo, enemies) && !SailToLanding(fleet, enemies))
                _session.MoveUnit(_player.Id, fleet.Id, port.Id);
        }
    }

    /// <summary>
    /// Lands everything aboard on the enemy coast next to the fleet with no enemy troops, the most valuable first.
    /// False when there is none.
    /// </summary>
    private bool Land(Unit fleet, List<Unit> cargo, HashSet<int> enemies)
    {
        var beach = Map.Provinces[fleet.ProvinceId].Neighbors.Select(n => Map.Provinces[n])
            .Where(p => !p.IsWater && p.IsOwned && enemies.Contains(p.ControllerId) && !_session.EnemyRegimentsIn(p.Id, _player.Id).Any())
            .MaxBy(p => _session.ProvinceValue(p));
        if (beach == null) return false;
        foreach (var unit in cargo) _session.Disembark(_player.Id, unit.Id, beach.Id);
        return true;
    }

    /// <summary>Sails for the sea next to the enemy's least defended coast, the nearest first among equals. False when it finds no way there.</summary>
    private bool SailToLanding(Unit fleet, HashSet<int> enemies)
    {
        var from = Map.Provinces[fleet.ProvinceId];
        var targets = Map.Provinces
            .Where(p => !p.IsWater && p.IsOwned && enemies.Contains(p.ControllerId) && IsCoast(p))
            .OrderBy(p => _session.EnemyRegimentsIn(p.Id, _player.Id).Sum(GameSession.RegimentPower))
            .ThenBy(p => Map.DistanceKm(from, p))
            .Take(5);
        foreach (var target in targets)
            foreach (int sea in target.Neighbors.Where(n => Map.Provinces[n].IsWater))
                if (_session.MoveUnit(_player.Id, fleet.Id, sea).Ok) return true;
        return false;
    }
}
