using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Game.AI;

/// <summary>
/// A computer rival. It settles a good site quickly, then keeps a few warriors claiming the best
/// free land along its borders, sends out new settlers once its cities have grown, pays for
/// festivals when a city grows restless, researches advances and puts up buildings in a fixed order of
/// preference. Its army (AiPlayer.Military.cs) trains, organises itself, declares wars it can win and
/// makes peace when they go badly.
/// Deterministic: all choices come from its own seeded Random.
/// </summary>
internal sealed partial class AiPlayer
{
    private const int MaxCities = 8;
    /// <summary>Cities below this mood get a festival.</summary>
    private const double FestivalMood = 45;
    private const double GoldKeptForRecruiting = 30;
    private const double WoodKeptForRecruiting = 50;
    /// <summary>Buildings only go where at least this many people live.</summary>
    private const double MinWorkersForBuilding = 200;
    private const double BigCityPopulation = 2000;
    private static readonly BuildingType[] BuildOrder =
    [
        BuildingType.Farm, BuildingType.Granary, BuildingType.Temple, BuildingType.Library, BuildingType.Market,
        BuildingType.Mine, BuildingType.Sawmill, BuildingType.Aqueduct, BuildingType.HerbalistHut, BuildingType.Amphitheatre,
        BuildingType.Road, BuildingType.Walls, BuildingType.University, BuildingType.Bank, BuildingType.Castle,
        BuildingType.Factory, BuildingType.Hospital, BuildingType.Railway, BuildingType.PowerPlant,
    ];
    /// <summary>Within each branch, the first of these it can research: food, then the advances that pay for themselves.</summary>
    private static readonly Tech[] ResearchOrder =
    [
        Tech.Agriculture, Tech.Writing, Tech.Archery, Tech.Carpentry, Tech.Mythology, Tech.HorsebackRiding, Tech.Irrigation,
        Tech.Mining, Tech.Pottery, Tech.Medicine, Tech.BronzeWorking, Tech.TheWheel, Tech.Currency, Tech.CodeOfLaws, Tech.IronWorking,
        Tech.Mathematics, Tech.MilitaryTactics, Tech.Trade, Tech.Philosophy, Tech.Construction, Tech.Engineering, Tech.SiegeEngines,
        Tech.Administration, Tech.DramaAndPoetry, Tech.Fortifications, Tech.HeavyCavalry, Tech.Navigation,
        Tech.CropRotation, Tech.Education, Tech.Machinery, Tech.Guilds, Tech.Theology, Tech.Stirrup, Tech.Banking, Tech.Astronomy,
        Tech.Castles, Tech.PrintingPress, Tech.Gunpowder, Tech.Economics, Tech.Anatomy, Tech.DeepMining, Tech.Metallurgy,
        Tech.ScientificMethod, Tech.MilitaryScience, Tech.Cartography,
        Tech.Rifling, Tech.SteamEngine, Tech.PublicEducation, Tech.Industrialization, Tech.Sanitation, Tech.Chemistry, Tech.Steel,
        Tech.Railroad, Tech.MachineGuns,
        Tech.Electricity, Tech.Fertilizers, Tech.OilRefining, Tech.Combustion, Tech.Antibiotics, Tech.HeavyArtillery, Tech.Electronics,
        Tech.Armour, Tech.AssemblyLine, Tech.Aviation,
    ];
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
    public int PlayerId => _player.Id;

    public void Think(bool dailyDecisions)
    {
        ClassifyNewRegiments();
        bool atWar = _session.EnemiesOf(_player.Id).Any();
        // Fleets keep to their ports (see BuildNavy); troops aboard wait for their ships.
        var units = _session.Units.Where(u => u.OwnerId == _player.Id && !u.IsFleet && !u.IsAboard && !u.IsMoving && !u.AttackingProvinceId.HasValue).ToList();
        // Hungry soldiers go home and farm (never while at war).
        if (_player.IsStarving && !atWar && units.FirstOrDefault(u => u.IsMilitary && Map.Provinces[u.ProvinceId].OwnerId == _player.Id) is { } idle)
        {
            _session.Disband(_player.Id, idle.Id);
            units.Remove(idle);
        }
        foreach (var unit in units)
        {
            if (unit.Type == UnitType.Settlers) GuideSettlers(unit);
            else if (unit.IsHeadquarters) FollowTroops(unit);
            else if (atWar) GuideSoldier(unit);
            else if (_claimers.Contains(unit.Id)) GuideWarriors(unit);
            else if (unit.IsMilitary) GoHomeIfCutOff(unit);
        }
        foreach (int id in _targets.Keys.Where(id => _session.UnitById(id) is null).ToList()) _targets.Remove(id);

        if (dailyDecisions)
        {
            HoldFestivals();
            SetResearchPriorities();
            ChooseResearch();
            AdoptInstitutions();
            Recruit();
            BuildArmy();
            BuildNavy();
            OrganiseArmy();
            Diplomacy();
            Construct();
        }
    }

    /// <summary>
    /// Picks its most wanted building (cities first, then by population and <see cref="BuildOrder"/>)
    /// and starts it once it can pay while keeping enough wood and gold to recruit. Until then it
    /// saves up rather than spending on something cheaper.
    /// </summary>
    private void Construct()
    {
        if (BuildCityIfWorthIt()) return;
        var provinces = _player.Provinces.Select(id => Map.Provinces[id])
            .Where(p => p.Population >= GameRules.SettledPopulation && !p.Constructing.HasValue && p.PlannedCityName == null)
            .OrderByDescending(p => p.CityId.HasValue).ThenByDescending(p => p.Population);
        foreach (var p in provinces)
        foreach (var type in BuildOrder)
        {
            if (p.Buildings.Contains(type) || !_session.IsBuildingAvailable(p, type).Ok || !WorthBuilding(p, type)) continue;
            bool spare = type.Info().Cost.Items.All(i =>
                _player.Stockpile[i.Type] - i.Amount >= (i.Type == ResourceType.Wood ? WoodKeptForRecruiting : GoldKeptForRecruiting));
            if (spare) _session.Build(_player.Id, p.Id, type);
            return;
        }
    }

    /// <summary>
    /// Below <see cref="MaxCities"/>, turns its most populous province without a city into one once it
    /// can pay while keeping enough to recruit. Returns whether it started (or is saving up for) one.
    /// </summary>
    private bool BuildCityIfWorthIt()
    {
        int cities = _session.Cities.Count(c => c.OwnerId == _player.Id) + _player.Provinces.Count(id => Map.Provinces[id].PlannedCityName != null);
        if (cities >= MaxCities) return false;
        var site = _player.Provinces.Select(id => Map.Provinces[id])
            .Where(p => p.Population >= 2 * GameRules.CityBuildingPopulation && _session.IsCitySite(_player.Id, p).Ok)
            .MaxBy(p => p.Population);
        if (site == null) return false;
        bool spare = GameRules.CityCost.Items.All(i =>
            _player.Stockpile[i.Type] - i.Amount >= (i.Type == ResourceType.Wood ? WoodKeptForRecruiting : GoldKeptForRecruiting));
        if (spare) _session.BuildCity(_player.Id, site.Id, _session.NextCityName());
        return true;
    }

    /// <summary>Whether a building would pay off here: enough people to benefit, wooded land for sawmills, restless people for temples.</summary>
    private bool WorthBuilding(Province p, BuildingType type) => p.Population >= MinWorkersForBuilding && type switch
    {
        BuildingType.Sawmill => p.Info.WoodYield >= 1,
        BuildingType.Temple or BuildingType.Amphitheatre => p.Mood < FestivalMood + 15,
        BuildingType.Aqueduct => p.Population > 0.6 * _session.CapacityOf(p),
        // Roads where the armies gather, walls around the big cities.
        BuildingType.Road or BuildingType.Railway => p.CityId.HasValue,
        BuildingType.Walls or BuildingType.Castle => p.Population >= BigCityPopulation,
        _ => true,
    };

    /// <summary>Leans its science toward the economy in peace (more while starving) and toward the military at war.</summary>
    private void SetResearchPriorities()
    {
        bool atWar = _session.EnemiesOf(_player.Id).Any();
        int[] priorities = atWar ? [1, 1, 3] : _player.IsStarving ? [3, 1, 1] : [2, 1, 1];
        foreach (var branch in Techs.Branches) _session.SetResearchPriority(_player.Id, branch, priorities[(int)branch]);
    }

    /// <summary>Buys an institution that has reached it when the gold is there after keeping enough to recruit.</summary>
    private void AdoptInstitutions()
    {
        foreach (var institution in Institutions.All.Where(i => _session.CanAdopt(_player, i).Ok))
            if (_player.Stockpile[ResourceType.Gold] - _session.AdoptionCost(_player, institution) >= GoldKeptForRecruiting)
                _session.Adopt(_player.Id, institution);
    }

    /// <summary>Every branch without research picks the first advance of its order of preference that it can research.</summary>
    private void ChooseResearch()
    {
        if (_player.CapitalCityId is null) return;
        foreach (var branch in Techs.Branches.Where(b => _player.Researching[(int)b] is null))
            foreach (var tech in ResearchOrder.Where(t => t.Info().Branch == branch))
                if (_session.Research(_player.Id, tech).Ok) break;
    }

    /// <summary>Restless cities get a festival when there is gold to spare after keeping enough to recruit.</summary>
    private void HoldFestivals()
    {
        foreach (var city in _session.Cities.Where(c => c.OwnerId == _player.Id).ToList())
        {
            var p = Map.Provinces[city.ProvinceId];
            if (p.Mood >= FestivalMood) continue;
            if (_player.Stockpile[ResourceType.Gold] - GameRules.FestivalCost(p.Population) >= GoldKeptForRecruiting)
                _session.HoldFestival(_player.Id, city.Id);
        }
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
        var (hours, _) = _session.Pathfinder.FromSources([unit.ProvinceId], maxHours: first ? 72 : 24 * 12, canEnter: id => _session.CanUnitEnter(unit, id));
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

    /// <summary>Sends out settlers once its cities have grown, and trains warriors to claim land (see <see cref="ClassifyNewRegiments"/>).</summary>
    private void Recruit()
    {
        var cities = _session.Cities.Where(c => c.OwnerId == _player.Id).ToList();
        if (cities.Count == 0) return;
        int settlers = _session.Units.Count(u => u.OwnerId == _player.Id && u.Type == UnitType.Settlers);
        int claimers = _claimers.Count + cities.Sum(c => c.Training.Count(o => o.Battalion == BattalionType.Warriors));

        foreach (var city in cities.OrderByDescending(c => Map.Provinces[c.ProvinceId].Population))
        {
            double pop = Map.Provinces[city.ProvinceId].Population;
            if (settlers == 0 && cities.Count < MaxCities && pop > 400 && TotalPopulation() > 1500
                && _player.Stockpile[ResourceType.Food] > 300
                && _session.RecruitSettlers(_player.Id, city.Id).Ok)
            {
                settlers++;
                continue;
            }
            bool foodToSpare = _player.LastDayNet[(int)ResourceType.Food] > 3 || _player.Stockpile[ResourceType.Food] > 500;
            if (claimers < 1 + cities.Count && pop > 250 && foodToSpare && _session.Train(_player.Id, city.Id, BattalionType.Warriors).Ok)
                claimers++;
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

    /// <summary>How good a province is to live in: food it can grow, plus known deposits and sea access.</summary>
    private double SiteScore(Province p)
    {
        double food = Math.Min(p.Capacity, 40000) * p.FoodYield / 100;
        double deposits = Resources.Deposits.Where(r => p.HasDeposit(r) && _player.Knows(r)).Sum(r => p.Deposits[(int)r]) * 10;
        double coast = p.Neighbors.Any(n => Map.Provinces[n].IsWater) ? 20 : 0;
        return food + deposits + coast;
    }

    private double NearestCityDistanceKm(Province p) =>
        _session.Cities.Count == 0 ? double.MaxValue : _session.Cities.Min(c => Map.DistanceKm(Map.Provinces[c.ProvinceId], p));
}
