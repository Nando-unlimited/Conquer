using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Game.AI;

/// <summary>
/// A computer rival. It settles a good site quickly, then keeps a few warriors claiming the best
/// free land along its borders, and sends out new settlers once its cities have grown.
/// Deterministic: all choices come from its own seeded Random.
/// </summary>
internal sealed class AiPlayer
{
    private const int MaxCities = 8;
    private readonly GameSession _session;
    private readonly Player _player;
    private readonly Random _random;
    /// <summary>Where each unit is heading and why; cleared when the plan fails.</summary>
    private readonly Dictionary<int, int> _targets = [];

    public AiPlayer(GameSession session, Player player, int seed)
    {
        _session = session;
        _player = player;
        _random = new Random(seed);
    }

    private WorldMap Map => _session.Map;

    public void Think(bool dailyDecisions)
    {
        var units = _session.Units.Where(u => u.OwnerId == _player.Id && !u.IsMoving).ToList();
        // Hungry soldiers go home and farm.
        if (_player.IsStarving && units.FirstOrDefault(u => u.Info.IsMilitary && Map.Provinces[u.ProvinceId].OwnerId == _player.Id) is { } idle)
        {
            _session.Disband(_player.Id, idle.Id);
            units.Remove(idle);
        }
        foreach (var unit in units)
        {
            if (unit.Type == UnitType.Settlers) GuideSettlers(unit);
            else GuideWarriors(unit);
        }
        foreach (int id in _targets.Keys.Where(id => _session.UnitById(id) is null).ToList()) _targets.Remove(id);

        if (dailyDecisions) Recruit();
    }

    private void GuideSettlers(Unit unit)
    {
        if (_targets.TryGetValue(unit.Id, out int target) && target == unit.ProvinceId)
        {
            _targets.Remove(unit.Id);
            if (_session.FoundCity(_player.Id, unit.Id).Ok) return;
        }

        // The first city is urgent (food runs out); later ones may travel further for a better site.
        bool first = _player.CapitalCityId is null;
        var (hours, _) = _session.Pathfinder.FromSources([unit.ProvinceId], maxHours: first ? 72 : 24 * 12);
        int best = -1;
        double bestScore = double.MinValue;
        for (int id = 0; id < hours.Length; id++)
        {
            if (double.IsPositiveInfinity(hours[id])) continue;
            var p = Map.Provinces[id];
            if (!CanSettle(p) || _targets.ContainsValue(id)) continue;
            if (!first && NearestCityDistanceKm(p) < 250) continue;
            double score = SiteScore(p) / (1 + hours[id] / (first ? 24 : 96)) + _random.NextDouble();
            if (score > bestScore) (best, bestScore) = (id, score);
        }
        if (best < 0) return;
        _targets[unit.Id] = best;
        if (best == unit.ProvinceId) _session.FoundCity(_player.Id, unit.Id);
        else _session.MoveUnit(_player.Id, unit.Id, best);
    }

    private void GuideWarriors(Unit unit)
    {
        if (_session.CanClaim(unit).Ok)
        {
            _session.Claim(_player.Id, unit.Id);
            _targets.Remove(unit.Id);
            return;
        }

        // Claim no faster than migrants can fill the land.
        int empty = _player.Provinces.Count(id => Map.Provinces[id].Population < GameRules.SettledPopulation);
        if (empty > 1 + _session.Cities.Count(c => c.OwnerId == _player.Id)) return;

        var here = Map.Provinces[unit.ProvinceId];
        int best = -1;
        double bestScore = double.MinValue;
        foreach (int frontier in FreeBorderProvinces())
        {
            if (_targets.ContainsValue(frontier)) continue;
            var p = Map.Provinces[frontier];
            double km = Map.DistanceKm(here, p);
            double score = SiteScore(p) / (1 + km / 300) + _random.NextDouble();
            if (score > bestScore) (best, bestScore) = (frontier, score);
        }
        if (best < 0) return;
        _targets[unit.Id] = best;
        _session.MoveUnit(_player.Id, unit.Id, best);
    }

    private void Recruit()
    {
        var cities = _session.Cities.Where(c => c.OwnerId == _player.Id).ToList();
        if (cities.Count == 0) return;
        int warriors = _session.Units.Count(u => u.OwnerId == _player.Id && u.Type == UnitType.Warriors);
        int settlers = _session.Units.Count(u => u.OwnerId == _player.Id && u.Type == UnitType.Settlers);

        foreach (var city in cities.OrderByDescending(c => Map.Provinces[c.ProvinceId].Population))
        {
            double pop = Map.Provinces[city.ProvinceId].Population;
            if (settlers == 0 && cities.Count < MaxCities && pop > 400 && TotalPopulation() > 1500
                && _player.Stockpile[ResourceType.Food] > 300
                && _session.Recruit(_player.Id, city.Id, UnitType.Settlers).Ok)
            {
                settlers++;
                continue;
            }
            bool foodToSpare = _player.LastDayNet[(int)ResourceType.Food] > 3 || _player.Stockpile[ResourceType.Food] > 500;
            if (warriors < 1 + cities.Count && pop > 250 && foodToSpare && _session.Recruit(_player.Id, city.Id, UnitType.Warriors).Ok)
                warriors++;
        }
    }

    private double TotalPopulation() => _player.Provinces.Sum(id => Map.Provinces[id].Population);

    private IEnumerable<int> FreeBorderProvinces()
    {
        var seen = new HashSet<int>();
        foreach (int id in _player.Provinces)
        foreach (int n in Map.Provinces[id].Neighbors)
        {
            var p = Map.Provinces[n];
            if (!p.IsOwned && p.IsClaimable && seen.Add(n)) yield return n;
        }
    }

    private bool CanSettle(Province p) =>
        p.IsClaimable && (p.OwnerId < 0 || p.OwnerId == _player.Id) && !p.CityId.HasValue &&
        !p.Neighbors.Any(n => Map.Provinces[n].CityId.HasValue);

    /// <summary>How good a province is to live in: food it can grow, plus deposits and sea access.</summary>
    private double SiteScore(Province p)
    {
        double food = Math.Min(p.Capacity, 40000) * p.Info.FoodYield / 100;
        double deposits = Resources.Deposits.Sum(r => p.Deposits[(int)r]) * 10;
        double coast = p.Neighbors.Any(n => Map.Provinces[n].IsWater) ? 20 : 0;
        return food + deposits + coast;
    }

    private double NearestCityDistanceKm(Province p) =>
        _session.Cities.Count == 0 ? double.MaxValue : _session.Cities.Min(c => Map.DistanceKm(Map.Provinces[c.ProvinceId], p));
}
