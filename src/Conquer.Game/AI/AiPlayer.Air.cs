using Conquer.Game.Buildings;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Game.AI;

/// <summary>
/// The computer's air force: its airfields form escuadrillas (fighters first, then dive, tactical and strategic
/// bombers), which it joins into bigger units and puts under divisiones aéreas; at war they fly where the fighting is.
/// </summary>
internal sealed partial class AiPlayer
{
    /// <summary>The aircraft it forms escuadrillas of, and makes the planes for.</summary>
    private static readonly BattalionType[] AirLines =
        [BattalionType.Fighters, BattalionType.CloseSupport, BattalionType.TacticalBombers, BattalionType.Bombers];

    private bool HasAirfield => _player.Provinces.Any(id => Map.Provinces[id].Has(BuildingType.Airfield));

    /// <summary>
    /// Forms an escuadrilla a day at an airfield with room while it has fewer than three plus one per city (36 at most):
    /// half of them fighters, a quarter dive bombers, a sixth tactical bombers and the rest strategic bombers. Then joins
    /// its units of a kind at each base and puts them under divisiones aéreas.
    /// </summary>
    private void BuildAirForce()
    {
        if (!HasAirfield) return;
        var mine = _session.AirUnits.Where(u => u.OwnerId == _player.Id).ToList();
        var airfields = _player.Provinces.Select(id => Map.Provinces[id]).Where(p => _session.AirfieldRoom(p, _player.Id) > 0).ToList();
        int forming = _player.Provinces.Sum(id => Map.Provinces[id].Training.Count(o => o.Battalion is BattalionType t && t.First().Flies));
        int wanted = Math.Min(36, 3 + _session.Cities.Count(c => c.OwnerId == _player.Id));
        if (airfields.Count > 0 && mine.Sum(u => u.Flights.Count) + forming < wanted && _player.LastDayNet[(int)Economy.ResourceType.Gold] >= 0)
        {
            int Count(BattalionType t) => mine.Where(u => u.Type == t).Sum(u => u.Flights.Count);
            var type = Count(BattalionType.Fighters) * 2 < wanted ? BattalionType.Fighters
                : Count(BattalionType.CloseSupport) * 4 < wanted ? BattalionType.CloseSupport
                : Count(BattalionType.TacticalBombers) * 6 < wanted ? BattalionType.TacticalBombers
                : BattalionType.Bombers;
            var airfield = airfields.MaxBy(p => p.Population)!;
            if (_session.CanTrain(airfield, type).Ok && Spare(GameSession.TrainedModel(_player, type).TrainingCost)) _session.Train(_player.Id, airfield.Id, type);
        }
        OrganiseAirForce();
    }

    /// <summary>
    /// Joins its air units of each kind at each base into one, up to an ala; forms a División aérea for every 6 escuadrillas
    /// and the Mando aéreo once it has two divisions; and puts each unattached unit under a division in range with room.
    /// </summary>
    private void OrganiseAirForce()
    {
        foreach (var group in _session.AirUnits.Where(u => u.OwnerId == _player.Id).GroupBy(u => (u.Type, u.BaseProvinceId, u.CarrierId)).ToList())
        {
            var units = group.OrderByDescending(u => u.Flights.Count).ToList();
            foreach (var other in units.Skip(1))
                if (_session.CanJoin(units[0], other).Ok) _session.JoinAirUnits(_player.Id, units[0].Id, other.Id);
        }

        var mine = _session.AirUnits.Where(u => u.OwnerId == _player.Id).ToList();
        var divisions = _session.AirHeadquarters.Where(h => h.OwnerId == _player.Id && !h.IsCommand).ToList();
        var airfield = _player.Provinces.Select(id => Map.Provinces[id]).Where(p => p.Has(BuildingType.Airfield)).MaxBy(p => p.Population);
        if (airfield != null && mine.Sum(u => u.Flights.Count) >= 6 * (divisions.Count + 1) && _session.CanRaiseAirHeadquarters(airfield, 1).Ok
            && Spare(GameSession.AirHeadquartersCost(1)))
            _session.RaiseAirHeadquarters(_player.Id, airfield.Id, 1);
        if (airfield != null && divisions.Count >= 2 && _session.AirCommandOf(_player) == null && _session.CanRaiseAirHeadquarters(airfield, 2).Ok
            && Spare(GameSession.AirHeadquartersCost(2)))
            _session.RaiseAirHeadquarters(_player.Id, airfield.Id, 2);

        foreach (var unit in mine.Where(u => u.CommanderId == null || !_session.InAirCommand(u)))
        {
            var division = _session.AirHeadquarters
                .Where(h => h.OwnerId == _player.Id && !h.IsCommand && _session.UnitsOf(h).Count() < MilitaryRules.MaxUnitsPerAirDivision
                            && Map.DistanceKm(Map.Provinces[h.BaseProvinceId], _session.BaseOf(unit)) <= MilitaryRules.AirDivisionRangeKm)
                .MinBy(h => Map.DistanceKm(Map.Provinces[h.BaseProvinceId], _session.BaseOf(unit)));
            if (division != null) _session.AttachAirUnit(_player.Id, unit.Id, division.Id);
        }
    }

    /// <summary>
    /// In war, each air unit flies over the fighting within its range: fighters over the nearest battle (else over its
    /// bombers' targets, else over its land nearest the enemy), dive bombers over the nearest battle, tactical bombers over
    /// a battle or else the nearest enemy city, strategic bombers over the nearest enemy city, naval aircraft over the
    /// nearest enemy fleet; in peace they all stay home.
    /// </summary>
    private void AirMissions(bool atWar)
    {
        var enemies = _session.EnemiesOf(_player.Id).Select(e => e.Id).ToHashSet();
        foreach (var unit in _session.AirUnits.Where(u => u.OwnerId == _player.Id).ToList())
        {
            var (mission, target) = atWar ? Mission(unit, enemies) : (AirMission.None, (Province?)null);
            if (unit.Mission == mission && unit.TargetProvinceId == target?.Id) continue;
            _session.SetAirMission(_player.Id, unit.Id, target == null ? AirMission.None : mission, target?.Id);
        }
    }

    /// <summary>Its province within the unit's range nearest to a city the enemy holds; null if it has none in range.</summary>
    private Province? FrontNearest(AirUnit unit, HashSet<int> enemies)
    {
        var enemyLand = _session.Cities.Where(c => enemies.Contains(c.OwnerId)).Select(c => Map.Provinces[c.ProvinceId]).ToList();
        if (enemyLand.Count == 0) return null;
        return _player.Provinces.Select(id => Map.Provinces[id]).Where(p => _session.InRange(unit, p))
            .MinBy(p => enemyLand.Min(e => Map.DistanceKm(p, e)));
    }

    private (AirMission, Province?) Mission(AirUnit unit, HashSet<int> enemies)
    {
        var home = _session.BaseOf(unit);
        Province? Nearest(IEnumerable<Province> places) => places.Where(p => _session.InRange(unit, p)).MinBy(p => Map.DistanceKm(p, home));
        var battles = _session.Battles.Where(b => b.AttackerId == _player.Id || b.DefenderId == _player.Id).Select(b => Map.Provinces[b.ProvinceId]);
        var enemyCities = _session.Cities.Where(c => enemies.Contains(c.OwnerId)).Select(c => Map.Provinces[c.ProvinceId]);
        return unit.Type switch
        {
            // Fighters over the battles, else over its bombers' targets, else over its land nearest the enemy.
            BattalionType.Fighters => (AirMission.AirSuperiority, Nearest(battles)
                ?? Nearest(_session.AirUnits.Where(u => u.OwnerId == _player.Id && u.Mission == AirMission.StrategicBombing && u.TargetProvinceId != null)
                    .Select(u => Map.Provinces[u.TargetProvinceId!.Value]))
                ?? FrontNearest(unit, enemies)),
            BattalionType.CloseSupport => (AirMission.CloseSupport, Nearest(battles)),
            BattalionType.TacticalBombers => Nearest(battles) is { } battle ? (AirMission.CloseSupport, battle) : (AirMission.StrategicBombing, Nearest(enemyCities)),
            BattalionType.Bombers => (AirMission.StrategicBombing, Nearest(enemyCities)),
            BattalionType.NavalBombers => (AirMission.NavalStrike, Nearest(_session.Units.Where(u => u.IsFleet && enemies.Contains(u.OwnerId)).Select(u => Map.Provinces[u.ProvinceId]))),
            _ => (AirMission.None, null),
        };
    }
}
