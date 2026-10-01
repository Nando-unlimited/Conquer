using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Game.AI;

/// <summary>
/// The rival's army. In peace a few lone scouts claim land while the rest train and garrison (engineers build instead, see AiPlayer.cs);
/// regiments are merged, put under corps and army HQs and given officers. It declares war on a weaker
/// neighbour now and then, attacks the least defended enemy provinces, rushes to cities under attack
/// and makes peace when a war goes badly.
/// </summary>
internal sealed partial class AiPlayer
{
    /// <summary>No wars in the first years, while nations settle.</summary>
    private const double PeacefulDays = 2 * 365;
    /// <summary>Units attack once they have recovered this share of their organisation.</summary>
    private const double ReadyOrganisation = 0.6;
    /// <summary>It builds its combat units up to brigades.</summary>
    private const int BattalionsPerUnit = 6;
    /// <summary>The highest HQ level it raises: corps and armies.</summary>
    private const int HighestHeadquarters = 2;

    /// <summary>Lone scouts (or warriors, in older games) that claim free land; every other regiment but the engineers belongs to the army.</summary>
    private readonly HashSet<int> _claimers = [];
    private readonly HashSet<int> _knownRegiments = [];
    private RegimentTemplate? _armyTemplate;

    internal AiSave ToSave() => new(_player.Id, new Dictionary<int, int>(_targets), [.. _claimers], [.. _knownRegiments], _armyTemplate?.Id);

    internal void Restore(AiSave save)
    {
        foreach (var (unit, target) in save.Targets) _targets[unit] = target;
        _claimers.UnionWith(save.Claimers);
        _knownRegiments.UnionWith(save.KnownRegiments);
        _armyTemplate = _player.Templates.FirstOrDefault(t => t.Id == save.ArmyTemplateId);
    }

    private IEnumerable<Unit> Army => _session.Units.Where(u => u.OwnerId == _player.Id && u.IsMilitary && !_claimers.Contains(u.Id) && !IsEngineerUnit(u));

    /// <summary>Battalions that stay out of its fighting army: scouts claim land and engineers build.</summary>
    private static bool Auxiliary(BattalionType type) => type is BattalionType.Scouts or BattalionType.Engineers;

    /// <summary>
    /// New regiments fill the claimer slots first if they are lone scouts (one per city, plus one), since only
    /// scouts claim land; the rest join the army.
    /// </summary>
    private void ClassifyNewRegiments()
    {
        // Gone, or without scouts (claimers from saves before only scouts claimed): they join the army.
        _claimers.RemoveWhere(id => _session.UnitById(id) is not { HasScouts: true });
        int wanted = 1 + _session.Cities.Count(c => c.OwnerId == _player.Id);
        foreach (var unit in _session.Units.Where(u => u.OwnerId == _player.Id && u.IsMilitary && _knownRegiments.Add(u.Id)))
            if (_claimers.Count < wanted && unit.Battalions.Count == 1 && unit.Battalions[0].Type == BattalionType.Scouts)
                _claimers.Add(unit.Id);
    }

    /// <summary>
    /// Until the army reaches 2 battalions per city (4 at war), trains whole regiments from its template
    /// when it can afford one, and otherwise the best single battalion it can, in its biggest city with barracks.
    /// </summary>
    private void BuildArmy()
    {
        // An army it could not keep: no new troops while its gold is falling.
        if (_player.LastDayNet[(int)ResourceType.Gold] < 0) return;
        var cities = _session.Cities.Where(c => c.OwnerId == _player.Id).ToList();
        if (cities.Count == 0 || _session.Date.Days < 180) return;
        int target = cities.Count * (_session.EnemiesOf(_player.Id).Any() ? 4 : 2);
        int battalions = Army.Sum(u => u.Battalions.Count)
            + cities.Sum(c => c.Training.Sum(o => o.TemplateBattalions.Count + (o.Battalion is BattalionType t && !Auxiliary(t) ? 1 : 0)));
        if (battalions >= target) return;

        // Only cities with barracks train troops.
        var city = cities.Where(c => Map.Provinces[c.ProvinceId].Buildings.Contains(BuildingType.Barracks))
            .OrderByDescending(c => Map.Provinces[c.ProvinceId].Population).FirstOrDefault();
        if (city == null || Map.Provinces[city.ProvinceId].Population < 400) return;
        // A unit as big as the city can spare (keeping 100 people beyond the minimum), from 2 to 6 battalions.
        int size = Math.Clamp((int)((Map.Provinces[city.ProvinceId].Population - GameRules.MinCityPopulation - 100) / 100), 0, BattalionsPerUnit);
        if (size >= 2 && ArmyTemplate(size) is var template && Spare(template.Cost)
            && _session.TrainTemplate(_player.Id, city.Id, template.Id).Ok) return;
        var best = Battalions.All
            .Where(t => !t.Info().Naval && !Auxiliary(t) && _session.CanTrain(city, t).Ok && Spare(t.Info().Cost))
            .OrderByDescending(t => t.Info().Attack + t.Info().Defense)
            .Cast<BattalionType?>().FirstOrDefault();
        if (best is BattalionType type) _session.Train(_player.Id, city.Id, type);
    }

    /// <summary>
    /// A small navy to guard the coast: a warship fleet for every three ports, the hardest-hitting ship it
    /// can build in its biggest port. The fleets stay in port.
    /// </summary>
    private void BuildNavy()
    {
        if (_player.LastDayNet[(int)ResourceType.Gold] < 0) return;
        var ports = _session.Cities.Where(c => c.OwnerId == _player.Id && _session.IsPort(Map.Provinces[c.ProvinceId], _player.Id)).ToList();
        if (ports.Count == 0) return;
        int fleets = _session.Units.Count(u => u.IsFleet && u.OwnerId == _player.Id)
                     + ports.Sum(c => c.Training.Count(o => o.Battalion is BattalionType t && t.Info().Naval));
        if (fleets >= (ports.Count + 2) / 3) return;
        var port = ports.MaxBy(c => Map.Provinces[c.ProvinceId].Population)!;
        var warship = Battalions.All
            .Where(t => t.Info().Naval && t.Info().Capacity < t.Info().Men && _session.CanTrain(port, t).Ok && Spare(t.Info().Cost))
            .Cast<BattalionType?>().MaxBy(t => t!.Value.Info().Attack);
        if (warship is BattalionType type) _session.Train(_player.Id, port.Id, type);
    }

    /// <summary>
    /// Its unit design, kept up to date with what it knows and can supply: its sturdiest infantry, with its
    /// hardest-hitting troop as the third and fifth battalions, cut down to the size the city can spare.
    /// </summary>
    private RegimentTemplate ArmyTemplate(int size)
    {
        var known = Battalions.All.Where(t => !t.Info().Naval && !Auxiliary(t) && t.Info().Requires.All(_player.Techs.Contains) && CanSupply(t)).ToList();
        var infantry = known.Where(t => !t.Info().Mounted).MaxBy(t => t.Info().Defense);
        var striker = known.MaxBy(t => t.Info().Attack);
        BattalionType[] design = [.. new[] { infantry, infantry, striker, infantry, striker, infantry }.Take(size)];
        _armyTemplate ??= _session.AddTemplate(_player, design);
        if (!_armyTemplate.Battalions.SequenceEqual(design))
        {
            _armyTemplate.Battalions.Clear();
            _armyTemplate.Battalions.AddRange(design);
        }
        return _armyTemplate;
    }

    /// <summary>Whether it has, or produces, the materials beyond wood and gold that a battalion needs (copper, iron…): twice the amount in store, or enough within 90 days.</summary>
    private bool CanSupply(BattalionType type) => type.Info().Cost.Items
        .Where(i => i.Type is not (ResourceType.Wood or ResourceType.Gold))
        .All(i => _player.Stockpile[i.Type] >= i.Amount * 2 || _player.LastDayNet[(int)i.Type] * 90 >= i.Amount);

    /// <summary>Whether it can pay and still keep enough wood and gold for settlers and warriors.</summary>
    private bool Spare(ResourceCost cost) => cost.Items.All(i =>
        _player.Stockpile[i.Type] - i.Amount >= (i.Type == ResourceType.Wood ? WoodKeptForRecruiting : i.Type == ResourceType.Gold ? GoldKeptForRecruiting : 0));

    /// <summary>Merges small units into brigades, raises corps and army HQs as the army grows, and attaches everyone.</summary>
    private void OrganiseArmy()
    {
        foreach (var group in Army.Where(u => !u.IsMoving && !_session.InBattle(u)).GroupBy(u => u.ProvinceId))
        {
            var regiments = group.OrderByDescending(u => u.Battalions.Count).ToList();
            foreach (var small in regiments.Where(u => u.Battalions.Count < BattalionsPerUnit).ToList())
            {
                var host = regiments.FirstOrDefault(u => u != small && _session.UnitById(u.Id) != null && u.Battalions.Count + small.Battalions.Count <= BattalionsPerUnit);
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
        StaffArmy();
    }

    /// <summary>
    /// Puts an officer at the head of the biggest unit without one: the best in the reserve (most virtues and stars,
    /// fewest flaws), or a newly recruited one when the reserve is empty and it can spare the gold. One a day.
    /// </summary>
    private void StaffArmy()
    {
        if (Army.Where(u => u.Officer == null).OrderByDescending(u => u.Battalions.Count).FirstOrDefault() is not { } unit) return;
        if (_player.OfficerReserve.Count == 0)
        {
            if (_player.Stockpile[ResourceType.Gold] - MilitaryRules.OfficerCost < GoldKeptForRecruiting) return;
            _session.RecruitOfficer(_player.Id);
        }
        var best = _player.OfficerReserve.OrderByDescending(o => o.Traits.Count(t => !Officer.IsFlaw(t)) * o.Skill - 2 * o.Traits.Count(Officer.IsFlaw)).First();
        _session.AssignOfficer(_player.Id, unit.Id, best.Id);
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
