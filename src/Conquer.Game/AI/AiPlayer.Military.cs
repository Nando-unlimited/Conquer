using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Game.AI;

/// <summary>
/// The rival's army. In peace a few lone warriors claim land while the rest train and garrison;
/// regiments are merged and put under brigade, division and corps HQs. It declares war on a weaker
/// neighbour now and then, attacks the least defended enemy provinces, rushes to cities under attack
/// and makes peace when a war goes badly.
/// </summary>
internal sealed partial class AiPlayer
{
    /// <summary>No wars in the first years, while nations settle.</summary>
    private const double PeacefulDays = 2 * 365;
    /// <summary>Regiments attack once they have recovered this share of their organisation.</summary>
    private const double ReadyOrganisation = 0.6;
    private const int BattalionsPerRegiment = 4;
    /// <summary>The highest HQ level it raises: brigades, divisions and corps.</summary>
    private const int HighestHeadquarters = 3;

    /// <summary>Lone warriors that claim free land; every other regiment belongs to the army.</summary>
    private readonly HashSet<int> _claimers = [];
    private readonly HashSet<int> _knownRegiments = [];

    private IEnumerable<Unit> Army => _session.Units.Where(u => u.OwnerId == _player.Id && u.IsMilitary && !_claimers.Contains(u.Id));

    /// <summary>
    /// New regiments fill the claimer slots first if they are lone warriors (one per city, plus one);
    /// the rest join the army.
    /// </summary>
    private void ClassifyNewRegiments()
    {
        _claimers.RemoveWhere(id => _session.UnitById(id) is null);
        int wanted = 1 + _session.Cities.Count(c => c.OwnerId == _player.Id);
        foreach (var unit in _session.Units.Where(u => u.OwnerId == _player.Id && u.IsMilitary && _knownRegiments.Add(u.Id)))
            if (_claimers.Count < wanted && unit.Battalions.Count == 1 && unit.Battalions[0].Type == BattalionType.Warriors)
                _claimers.Add(unit.Id);
    }

    /// <summary>Trains the best battalion it can afford until the army reaches 2 battalions per city (4 at war).</summary>
    private void BuildArmy()
    {
        var cities = _session.Cities.Where(c => c.OwnerId == _player.Id).ToList();
        if (cities.Count == 0 || _session.Date.Days < 180) return;
        int target = cities.Count * (_session.EnemiesOf(_player.Id).Any() ? 4 : 2);
        int battalions = Army.Sum(u => u.Battalions.Count) + cities.Sum(c => c.Training.Count(o => o.Battalion is BattalionType t && t != BattalionType.Warriors));
        if (battalions >= target) return;

        var city = cities.OrderByDescending(c => Map.Provinces[c.ProvinceId].Population).First();
        if (Map.Provinces[city.ProvinceId].Population < 400) return;
        var best = Battalions.All
            .Where(t => _session.CanTrain(city, t).Ok && Spare(t.Info().Cost))
            .OrderByDescending(t => t.Info().Attack + t.Info().Defense)
            .Cast<BattalionType?>().FirstOrDefault();
        if (best is BattalionType type) _session.Train(_player.Id, city.Id, type);
    }

    /// <summary>Whether it can pay and still keep enough wood and gold for settlers and warriors.</summary>
    private bool Spare(ResourceCost cost) => cost.Items.All(i =>
        _player.Stockpile[i.Type] - i.Amount >= (i.Type == ResourceType.Wood ? WoodKeptForRecruiting : i.Type == ResourceType.Gold ? GoldKeptForRecruiting : 0));

    /// <summary>Merges small regiments, raises brigade, division and corps HQs as the army grows, and attaches everyone.</summary>
    private void OrganiseArmy()
    {
        foreach (var group in Army.Where(u => !u.IsMoving && !_session.InBattle(u)).GroupBy(u => u.ProvinceId))
        {
            var regiments = group.OrderByDescending(u => u.Battalions.Count).ToList();
            foreach (var small in regiments.Where(u => u.Battalions.Count < BattalionsPerRegiment).ToList())
            {
                var host = regiments.FirstOrDefault(u => u != small && _session.UnitById(u.Id) != null && u.Battalions.Count + small.Battalions.Count <= BattalionsPerRegiment);
                if (host != null && _session.UnitById(small.Id) != null) _session.Merge(_player.Id, host.Id, small.Id);
            }
        }

        var army = Army.ToList();
        var hqs = _session.Units.Where(u => u.OwnerId == _player.Id && u.IsHeadquarters).ToList();
        for (int level = 1; level <= HighestHeadquarters; level++)
        {
            var below = level == 1 ? army : hqs.Where(h => h.HeadquartersLevel == level - 1).ToList();
            RaiseAndAttach(below, hqs.Where(h => h.HeadquartersLevel == level).ToList(), level);
        }
    }

    /// <summary>Attaches unattached subordinates to the nearest HQ of the level with room, raising a new HQ when all are full.</summary>
    private void RaiseAndAttach(List<Unit> subordinates, List<Unit> hqs, int level)
    {
        var info = CommandLevels.Info(level);
        foreach (var unit in subordinates.Where(u => u.CommanderId is null))
        {
            var hq = hqs.Where(h => _session.SubordinatesOf(h).Count() < info.MaxSubordinates)
                .OrderBy(h => Map.DistanceKm(Map.Provinces[h.ProvinceId], Map.Provinces[unit.ProvinceId])).FirstOrDefault();
            if (hq != null) _session.Attach(_player.Id, unit.Id, hq.Id);
        }
        bool needed = subordinates.Count(u => u.CommanderId is null) >= 2 || (level >= 2 && subordinates.Count >= 2 && hqs.Count == 0);
        bool forming = _session.Cities.Any(c => c.OwnerId == _player.Id && c.Training.Any(o => o.HeadquartersLevel == level));
        if (!needed || forming || _player.CapitalCityId is not int capital || !Spare(info.Cost)) return;
        _session.RaiseHeadquarters(_player.Id, capital, level);
    }

    /// <summary>An HQ walks to where most of its subordinates are, so they stay within its range.</summary>
    private void FollowTroops(Unit hq)
    {
        var subs = _session.SubordinatesOf(hq).ToList();
        if (subs.Count == 0 || subs.All(_session.InCommandRange)) return;
        int target = subs.GroupBy(u => u.ProvinceId).OrderByDescending(g => g.Count()).First().Key;
        if (target != hq.ProvinceId) _session.MoveUnit(_player.Id, hq.Id, target);
    }

    /// <summary>
    /// At war: rush to a city of its own under attack, else attack the weakest enemy province next door,
    /// else march towards the nearest enemy land its supply reaches. Tired regiments rest first, and
    /// those cut off from supply head home.
    /// </summary>
    private void GuideSoldier(Unit unit)
    {
        if (GoHomeIfCutOff(unit) || unit.OrganisationShare < ReadyOrganisation) return;
        var threatened = _session.Battles.Where(b => b.DefenderId == _player.Id && Map.Provinces[b.ProvinceId].CityId.HasValue)
            .OrderBy(b => Map.DistanceKm(Map.Provinces[b.ProvinceId], Map.Provinces[unit.ProvinceId])).FirstOrDefault();
        if (threatened != null && threatened.ProvinceId != unit.ProvinceId
            && Map.DistanceKm(Map.Provinces[threatened.ProvinceId], Map.Provinces[unit.ProvinceId]) < 600)
        {
            _session.MoveUnit(_player.Id, unit.Id, threatened.ProvinceId);
            return;
        }

        double here = _session.Units.Where(u => u.OwnerId == _player.Id && u.IsMilitary && u.ProvinceId == unit.ProvinceId).Sum(GameSession.RegimentPower);
        var target = Map.Provinces[unit.ProvinceId].Neighbors
            .Where(n => IsEnemyLand(n) && _session.CanUnitEnter(unit, n))
            .Select(n => (Province: n, Defence: _session.EnemyRegimentsIn(n, _player.Id).Sum(GameSession.RegimentPower)))
            .Where(t => t.Defence == 0 || here >= t.Defence * 1.3)
            .OrderBy(t => t.Defence).ThenByDescending(t => Map.Provinces[t.Province].CityId.HasValue)
            .Select(t => (int?)t.Province).FirstOrDefault();
        if (target is int attack)
        {
            _session.MoveUnit(_player.Id, unit.Id, attack);
            return;
        }

        // Only enemy land our supply reaches: marching further would starve the army.
        var enemyLand = Map.Provinces.Where(p => IsEnemyLand(p.Id) && _session.IsSupplied(_player.Id, p.Id)).Select(p => p.Id).ToHashSet();
        if (enemyLand.Count == 0) return;
        var (hours, _) = _session.Pathfinder.FromSources([unit.ProvinceId], 24 * 60, enemyLand, id => _session.CanUnitEnter(unit, id));
        int nearest = enemyLand.Where(id => !double.IsPositiveInfinity(hours[id])).OrderBy(id => hours[id]).Select(id => (int?)id).FirstOrDefault() ?? -1;
        if (nearest >= 0) _session.MoveUnit(_player.Id, unit.Id, nearest);
    }

    /// <summary>A regiment out of supply walks back to the capital before hunger and desertion finish it.</summary>
    private bool GoHomeIfCutOff(Unit unit)
    {
        if (_session.IsInSupply(unit) || _player.CapitalCityId is not int capital || _session.CityById(capital) is not { } city) return false;
        return _session.MoveUnit(_player.Id, unit.Id, city.ProvinceId).Ok;
    }

    private bool IsEnemyLand(int provinceId)
    {
        var p = Map.Provinces[provinceId];
        return p.IsOwned && _session.AtWar(_player.Id, p.ControllerId);
    }

    /// <summary>
    /// After the peaceful years, now and then declares war on a much weaker neighbour; offers peace to
    /// rivals when a war drags on and goes badly.
    /// </summary>
    private void Diplomacy()
    {
        foreach (var enemy in _session.EnemiesOf(_player.Id).ToList())
            if (_session.WarDays(_player.Id, enemy.Id) >= 120 && !enemy.IsHuman && !Winning(enemy.Id))
                _session.ProposePeace(_player.Id, enemy.Id);

        if (_session.EnemiesOf(_player.Id).Any() || _session.Date.Days < PeacefulDays || Army.Sum(u => u.Battalions.Count) < 4) return;
        if (_random.Next(90) != 0) return;
        double power = _session.MilitaryPower(_player.Id);
        var victim = Neighbours().Where(n => _session.MilitaryPower(n) < power * 0.6)
            .OrderBy(_session.MilitaryPower).Select(n => (int?)n).FirstOrDefault();
        if (victim is int target) _session.DeclareWar(_player.Id, target);
    }

    /// <summary>Accepts peace once a war has lasted a while and it is not clearly winning, or after a year regardless.</summary>
    public bool WouldAcceptPeace(int otherId) =>
        _session.WarDays(_player.Id, otherId) >= 60 && (!Winning(otherId) || _session.WarDays(_player.Id, otherId) >= 365);

    /// <summary>Stronger army, and holding more of the enemy's land than it holds of ours.</summary>
    private bool Winning(int otherId)
    {
        int taken = Map.Provinces.Count(p => p.OwnerId == otherId && p.ControllerId == _player.Id);
        int lost = Map.Provinces.Count(p => p.OwnerId == _player.Id && p.ControllerId == otherId);
        return _session.MilitaryPower(_player.Id) > _session.MilitaryPower(otherId) * 1.2 && taken >= lost;
    }

    /// <summary>Nations whose land touches ours.</summary>
    private IEnumerable<int> Neighbours() =>
        _player.Provinces.SelectMany(id => Map.Provinces[id].Neighbors)
            .Select(n => Map.Provinces[n].OwnerId)
            .Where(o => o >= 0 && o != _player.Id)
            .Distinct();
}
