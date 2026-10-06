using Conquer.Game.Simulation;
using Conquer.Game.Buildings;
using Conquer.Game.Military;
using Conquer.Game.World;

namespace Conquer.Game.AI;

/// <summary>The computer's air force: its airfields form fighters first, then attack aircraft and bombers; at war they fly where the fighting is.</summary>
internal sealed partial class AiPlayer
{
    /// <summary>The aircraft it forms wings of, and makes the planes for.</summary>
    private static readonly BattalionType[] AirLines = [BattalionType.Fighters, BattalionType.CloseSupport, BattalionType.Bombers];

    private bool HasAirfield => _player.Provinces.Any(id => Map.Provinces[id].Has(BuildingType.Airfield));

    /// <summary>
    /// Forms a wing a day at an airfield with room while it has fewer than two plus one per three cities: half of them
    /// fighters, a quarter attack aircraft, the rest bombers.
    /// </summary>
    private void BuildAirForce()
    {
        if (!HasAirfield || _player.LastDayNet[(int)Economy.ResourceType.Gold] < 0) return;
        var mine = _session.Wings.Where(w => w.OwnerId == _player.Id).ToList();
        var airfields = _player.Provinces.Select(id => Map.Provinces[id]).Where(p => _session.AirfieldRoom(p, _player.Id) > 0).ToList();
        int forming = airfields.Sum(p => p.Training.Count(o => o.Battalion is BattalionType t && t.First().Flies));
        int wanted = Math.Min(12, 2 + _session.Cities.Count(c => c.OwnerId == _player.Id) / 3);
        if (airfields.Count == 0 || mine.Count + forming >= wanted) return;
        int Count(BattalionType t) => mine.Count(w => w.Type == t);
        var type = Count(BattalionType.Fighters) * 2 < wanted ? BattalionType.Fighters
            : Count(BattalionType.CloseSupport) * 4 < wanted ? BattalionType.CloseSupport
            : BattalionType.Bombers;
        var airfield = airfields.MaxBy(p => p.Population)!;
        if (_session.CanTrain(airfield, type).Ok && Spare(GameSession.TrainedModel(_player, type).TrainingCost)) _session.Train(_player.Id, airfield.Id, type);
    }

    /// <summary>
    /// In war, each wing flies over the fighting within its range: fighters and attack aircraft over the nearest battle
    /// (fighters otherwise over the border nearest the enemy), bombers over the nearest enemy city, naval aircraft over the
    /// nearest enemy fleet; in peace they all stay home.
    /// </summary>
    private void AirMissions(bool atWar)
    {
        var enemies = _session.EnemiesOf(_player.Id).Select(e => e.Id).ToHashSet();
        foreach (var wing in _session.Wings.Where(w => w.OwnerId == _player.Id).ToList())
        {
            var (mission, target) = atWar ? Mission(wing, enemies) : (AirMission.None, (Province?)null);
            if (wing.Mission == mission && wing.TargetProvinceId == target?.Id) continue;
            _session.SetAirMission(_player.Id, wing.Id, target == null ? AirMission.None : mission, target?.Id);
        }
    }

    /// <summary>Its province within the wing's range nearest to land the enemy holds; null if it has none in range.</summary>
    private Province? FrontNearest(AirWing wing, HashSet<int> enemies)
    {
        var enemyLand = _session.Cities.Where(c => enemies.Contains(c.OwnerId)).Select(c => Map.Provinces[c.ProvinceId]).ToList();
        if (enemyLand.Count == 0) return null;
        return _player.Provinces.Select(id => Map.Provinces[id]).Where(p => _session.InRange(wing, p))
            .MinBy(p => enemyLand.Min(e => Map.DistanceKm(p, e)));
    }

    private (AirMission, Province?) Mission(AirWing wing, HashSet<int> enemies)
    {
        var home = _session.BaseOf(wing);
        Province? Nearest(IEnumerable<Province> places) => places.Where(p => _session.InRange(wing, p)).MinBy(p => Map.DistanceKm(p, home));
        var battles = _session.Battles.Where(b => b.AttackerId == _player.Id || b.DefenderId == _player.Id).Select(b => Map.Provinces[b.ProvinceId]);
        return wing.Type switch
        {
            // Fighters over the battles, else over its bombers' targets, else over its land nearest the enemy.
            BattalionType.Fighters => (AirMission.AirSuperiority, Nearest(battles)
                ?? Nearest(_session.Wings.Where(w => w.OwnerId == _player.Id && w.Mission == AirMission.StrategicBombing && w.TargetProvinceId != null)
                    .Select(w => Map.Provinces[w.TargetProvinceId!.Value]))
                ?? FrontNearest(wing, enemies)),
            BattalionType.CloseSupport => (AirMission.CloseSupport, Nearest(battles)),
            BattalionType.Bombers => (AirMission.StrategicBombing, Nearest(_session.Cities.Where(c => enemies.Contains(c.OwnerId)).Select(c => Map.Provinces[c.ProvinceId]))),
            BattalionType.NavalBombers => (AirMission.NavalStrike, Nearest(_session.Units.Where(u => u.IsFleet && enemies.Contains(u.OwnerId)).Select(u => Map.Provinces[u.ProvinceId]))),
            _ => (AirMission.None, null),
        };
    }
}
