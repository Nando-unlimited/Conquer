using Conquer.Game.AI;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

public readonly record struct CommandResult(bool Ok, string Message)
{
    public static CommandResult Success(string message = "") => new(true, message);
    public static CommandResult Fail(string message) => new(false, message);
}

/// <param name="Settled">Citizens living in the nation's provinces.</param>
/// <param name="InUnits">Citizens serving in units.</param>
/// <param name="Migrating">Citizens on the road to a new home.</param>
/// <param name="PopulationByMood">Settled citizens at each mood level, worst first (see <see cref="GameRules.MoodNames"/>).</param>
public sealed record NationStats(
    double Settled, int InUnits, int Migrating, int Provinces, int Cities, int Units,
    double AverageMood, double AverageFertility, double[] PopulationByMood)
{
    public double Total => Settled + InUnits + Migrating;
}

/// <summary>
/// One running game. Time advances in fixed one-hour steps through <see cref="Step"/>; the economy
/// and migration run once per in-game day, at midnight. Player 0 is the human.
/// </summary>
public sealed class GameSession
{
    public const int HumanPlayerId = 0;

    private readonly Random _random;
    private readonly List<AiPlayer> _ais = [];
    private readonly Dictionary<int, Unit> _unitsById = [];
    private readonly HashSet<string> _usedCityNames = [];
    private readonly Dictionary<int, double> _emigrationCarry = [];
    private int _nextUnitId, _nextCityId, _nextMigrationId;

    public WorldMap Map { get; }
    public Pathfinder Pathfinder { get; }
    public List<Player> Players { get; } = [];
    public List<Unit> Units { get; } = [];
    public List<City> Cities { get; } = [];
    public List<Migration> Migrations { get; } = [];
    public List<Notification> Notifications { get; } = [];
    public GameDate Date { get; private set; }

    public Player Human => Players[HumanPlayerId];

    private GameSession(WorldMap map, int seed)
    {
        Map = map;
        Pathfinder = new Pathfinder(map);
        _random = new Random(seed);
    }

    /// <summary>
    /// Sets up a new game: nobody owns any land; each player gets one band of settlers on a
    /// habitable province, spread as far apart as possible, plus the starting stockpile.
    /// </summary>
    public static GameSession Create(WorldMap map, int playerCount, int seed)
    {
        foreach (var p in map.Provinces)
        {
            p.OwnerId = -1;
            p.Population = 0;
            p.CityId = null;
            p.Mood = GameRules.StartingMood;
            p.Fertility = 1;
        }
        var session = new GameSession(map, seed);
        var names = PlayerNames.Pick(playerCount, session._random);
        var starts = session.PickStartProvinces(playerCount);

        for (int i = 0; i < playerCount; i++)
        {
            var player = new Player(i, names[i], PlayerNames.Colors[i % PlayerNames.Colors.Length], i == HumanPlayerId);
            player.Stockpile[ResourceType.Food] = GameRules.StartingFood;
            player.Stockpile[ResourceType.Gold] = GameRules.StartingGold;
            player.Stockpile[ResourceType.Wood] = GameRules.StartingWood;
            session.Players.Add(player);
            session.AddUnit(i, UnitType.Settlers, starts[i], GameRules.StartingCitizens);
            if (!player.IsHuman) session._ais.Add(new AiPlayer(session, player, seed + 100 + i));
        }

        session.Notify(HumanPlayerId, "Tus colonos esperan órdenes. Busca una buena tierra y funda tu primera ciudad.");
        return session;
    }

    public Unit? UnitById(int id) => _unitsById.GetValueOrDefault(id);
    public City? CityById(int id) => Cities.FirstOrDefault(c => c.Id == id);
    public City? CityIn(Province p) => p.CityId is int id ? CityById(id) : null;

    /// <summary>Citizens a province's land can feed, including the bonus of a city.</summary>
    public double CapacityOf(Province p) => p.Capacity * (p.CityId.HasValue ? GameRules.CityCapacityMultiplier : 1);

    /// <summary>
    /// What pushes a province's mood up or down, as (reason, points). Their sum, clamped to 0..100,
    /// is the mood the province drifts toward.
    /// </summary>
    public List<(string Reason, double Points)> MoodFactors(Province p)
    {
        var factors = new List<(string, double)> { ("Base", GameRules.BaseMood) };
        if (p.CityId.HasValue) factors.Add(("Vida en la ciudad", GameRules.CityMood));
        if (p.OwnerId < 0) return factors;
        var owner = Players[p.OwnerId];
        if (owner.CapitalCityId is int capitalId && CityById(capitalId) is { } capital)
        {
            if (capital.ProvinceId == p.Id)
                factors.Add(("Capital", GameRules.CapitalMood));
            else
            {
                double km = Map.DistanceKm(Map.Provinces[capital.ProvinceId], p);
                factors.Add(("Lejos de la capital", -Math.Min(GameRules.MaxDistanceMoodPenalty, km / GameRules.KmPerMoodPoint)));
            }
        }
        if (CityIn(p) is { } city && city.HasFestival(Date.Hours)) factors.Add(("Fiestas", GameRules.FestivalMood));
        double reserve = GameRules.FoodReserveMood * Math.Min(1, owner.FoodReserveDays / GameRules.FoodReserveFullDays);
        if (reserve >= 0.5) factors.Add(("Reservas de comida", reserve));
        double capacity = CapacityOf(p);
        if (p.Population > capacity)
            factors.Add(("Hacinamiento", -GameRules.MaxOvercrowdingMoodPenalty * Math.Min(1, p.Population / capacity - 1)));
        if (owner.IsStarving) factors.Add(("Hambre", GameRules.StarvingMood));
        return factors;
    }

    public double TargetMood(Province p) => Math.Clamp(MoodFactors(p).Sum(f => f.Points), 0, 100);

    /// <summary>Totals and averages of a player's nation, for the nation screen.</summary>
    public NationStats Stats(Player player)
    {
        double settled = 0, mood = 0, fertility = 0;
        var byMood = new double[GameRules.MoodNames.Length];
        foreach (int id in player.Provinces)
        {
            var p = Map.Provinces[id];
            if (p.Population <= 0) continue;
            settled += p.Population;
            mood += p.Population * p.Mood;
            fertility += p.Population * p.Fertility;
            byMood[GameRules.MoodLevel(p.Mood)] += p.Population;
        }
        return new NationStats(
            settled,
            Units.Where(u => u.OwnerId == player.Id).Sum(u => u.Citizens),
            Migrations.Where(m => m.OwnerId == player.Id).Sum(m => m.People),
            player.Provinces.Count,
            Cities.Count(c => c.OwnerId == player.Id),
            Units.Count(u => u.OwnerId == player.Id),
            settled > 0 ? mood / settled : GameRules.StartingMood,
            settled > 0 ? fertility / settled : 1,
            byMood);
    }

    // ------------------------------------------------------------------ time

    /// <summary>Advances the game by one hour.</summary>
    public void Step()
    {
        Date = new GameDate(Date.Hours + 1);
        MoveUnits();
        ArriveMigrations();
        if (Date.Hour == 0)
        {
            foreach (var player in Players) DailyEconomy(player);
            foreach (var player in Players) DailyMigration(player);
        }
        if (Date.Hours % 6 == 0)
            foreach (var ai in _ais) ai.Think(dailyDecisions: Date.Hour == 0);
    }

    private void MoveUnits()
    {
        foreach (var unit in Units)
        {
            if (!unit.IsMoving) continue;
            unit.HoursToNext -= 1;
            while (unit.Path.Count > 0 && unit.HoursToNext <= 0)
            {
                double carry = unit.HoursToNext;
                unit.ProvinceId = unit.Path[0];
                unit.Path.RemoveAt(0);
                if (unit.Path.Count > 0)
                {
                    unit.StepHours = Pathfinder.StepHours(unit.ProvinceId, unit.Path[0]);
                    unit.HoursToNext = unit.StepHours + carry;
                }
                else
                {
                    unit.StepHours = unit.HoursToNext = 0;
                    if (unit.OwnerId == HumanPlayerId)
                        Notify(unit.OwnerId, $"{unit.Info.Name} han llegado a su destino.");
                }
            }
        }
    }

    private void ArriveMigrations()
    {
        for (int i = Migrations.Count - 1; i >= 0; i--)
        {
            var m = Migrations[i];
            if (m.ArriveHours > Date.Hours) continue;
            Migrations.RemoveAt(i);
            var target = Map.Provinces[m.ToProvinceId];
            if (target.OwnerId == m.OwnerId)
            {
                Settle(target, m.People, m.Mood);
                if (m.Forced && m.OwnerId == HumanPlayerId)
                    Notify(m.OwnerId, $"{m.People} ciudadanos han llegado a su nuevo hogar.");
            }
            else if (Players[m.OwnerId].CapitalCityId is int capital && CityById(capital) is { } city)
            {
                // The destination was lost on the way; the migrants settle in the capital instead.
                Settle(Map.Provinces[city.ProvinceId], m.People, m.Mood);
            }
        }
    }

    // ------------------------------------------------------------------ economy

    private void DailyEconomy(Player player)
    {
        var net = new double[Resources.All.Length];
        double population = 0;

        foreach (int id in player.Provinces)
        {
            var p = Map.Provinces[id];
            double pop = p.Population;
            if (pop <= 0) continue;
            population += pop;
            double capacity = CapacityOf(p);
            double worked = Math.Min(pop, capacity);
            double crowded = Math.Max(0, pop - capacity);
            double output = GameRules.MoodProductivity(p.Mood);
            net[(int)ResourceType.Food] += GameRules.FoodPerWorker * p.Info.FoodYield * (worked + crowded * GameRules.OvercrowdedFoodShare) * output;
            net[(int)ResourceType.Wood] += p.Info.WoodYield / 1000 * worked * output;
            if (p.Mood >= GameRules.UnrestMood) net[(int)ResourceType.Gold] += GameRules.TaxGoldPerCitizen * pop * output;
            double workforce = Math.Min(1, pop / GameRules.DepositFullWorkers);
            foreach (var r in Resources.Deposits) net[(int)r] += p.Deposits[(int)r] * workforce * output;
        }

        double eaters = population
                        + Units.Where(u => u.OwnerId == player.Id).Sum(u => u.Citizens)
                        + Migrations.Where(m => m.OwnerId == player.Id).Sum(m => m.People);
        net[(int)ResourceType.Food] -= GameRules.FoodPerCitizen * eaters;

        foreach (var r in Resources.All) player.Stockpile[r] += net[(int)r];
        Array.Copy(net, player.LastDayNet, net.Length);

        bool starving = player.Stockpile[ResourceType.Food] < 0;
        if (starving)
        {
            player.Stockpile[ResourceType.Food] = 0;
            if (!player.IsStarving && player.IsHuman)
                Notify(player.Id, "¡Se acabó la comida! La población empieza a morir de hambre.");
        }
        player.IsStarving = starving;
        double dailyFood = GameRules.FoodPerCitizen * eaters;
        player.FoodReserveDays = dailyFood > 0 ? player.Stockpile[ResourceType.Food] / dailyFood : 0;

        foreach (int id in player.Provinces)
        {
            var p = Map.Provinces[id];
            if (p.Population <= 0) continue;
            UpdateMoodAndFertility(p, starving);
            if (starving)
            {
                p.Population *= 1 - GameRules.StarvationRate;
            }
            else
            {
                double capacity = CapacityOf(p);
                double rate = GameRules.GrowthRate * p.Fertility * (p.CityId.HasValue ? GameRules.CityGrowthMultiplier : 1);
                if (p.Population < capacity)
                    p.Population += p.Population * rate * (1 - p.Population / capacity);
            }
        }
    }

    /// <summary>Mood closes part of the gap to its target each day; fertility follows mood and food, more slowly.</summary>
    private void UpdateMoodAndFertility(Province p, bool starving)
    {
        double before = p.Mood;
        p.Mood += (TargetMood(p) - p.Mood) * GameRules.MoodChangePerDay;
        p.Fertility += (GameRules.TargetFertility(p.Mood, starving) - p.Fertility) * GameRules.FertilityChangePerDay;

        if (p.OwnerId == HumanPlayerId && CityIn(p) is { } city)
        {
            if (before >= GameRules.UnrestMood && p.Mood < GameRules.UnrestMood)
                Notify(p.OwnerId, $"{city.Name} está descontenta y deja de pagar impuestos.");
            else if (before < GameRules.UnrestMood && p.Mood >= GameRules.UnrestMood)
                Notify(p.OwnerId, $"{city.Name} vuelve a estar en calma.");
        }
    }

    /// <summary>
    /// Each day, cities send part of their people to the player's provinces that are still thinly
    /// populated. Migrants walk at citizen speed and arrive after the real travel time.
    /// </summary>
    private void DailyMigration(Player player)
    {
        var sources = Cities
            .Where(c => c.OwnerId == player.Id && Map.Provinces[c.ProvinceId].Population > GameRules.MinEmigrationCityPopulation)
            .Select(c => c.ProvinceId)
            .ToList();
        if (sources.Count == 0) return;
        var unsettled = player.Provinces.Where(id => !Map.Provinces[id].CityId.HasValue).ToHashSet();
        if (unsettled.Count == 0) return;

        var (hours, nearest) = Pathfinder.FromSources(sources, maxHours: 24 * 60, unsettled);
        var incoming = Migrations.Where(m => m.OwnerId == player.Id)
            .GroupBy(m => m.ToProvinceId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.People));

        var targetsBySource = new Dictionary<int, List<(int Province, double Pull, double Deficit)>>();
        foreach (int id in player.Provinces)
        {
            var p = Map.Provinces[id];
            if (p.CityId.HasValue || nearest[id] < 0) continue;
            double wanted = CapacityOf(p) * GameRules.MigrationTargetShare;
            double deficit = wanted - p.Population - incoming.GetValueOrDefault(id);
            if (deficit < 1) continue;
            // Emptier and closer provinces pull harder, so new land fills from the inside out.
            double pull = deficit / wanted / (1 + hours[id] / 24);
            if (p.Population + incoming.GetValueOrDefault(id) < GameRules.SettledPopulation) pull *= 10;
            if (!targetsBySource.TryGetValue(nearest[id], out var list)) targetsBySource[nearest[id]] = list = [];
            list.Add((id, pull, deficit));
        }

        foreach (var (sourceId, targets) in targetsBySource)
        {
            var source = Map.Provinces[sourceId];
            // Fractions of a person carry over to the next day, so small cities still send someone now and then.
            double quota = _emigrationCarry.GetValueOrDefault(sourceId) + source.Population * GameRules.DailyEmigrationShare;
            int budget = (int)Math.Min(quota, source.Population - GameRules.MinEmigrationCityPopulation);
            _emigrationCarry[sourceId] = quota - Math.Max(budget, 0);
            if (budget < 1) continue;
            double totalPull = targets.Sum(t => t.Pull);
            // Share the day's emigrants by pull; small budgets go whole to the strongest pulls.
            foreach (var (target, pull, deficit) in targets.OrderByDescending(t => t.Pull))
            {
                if (budget < 1) break;
                int people = Math.Max(1, (int)Math.Round(budget * pull / totalPull));
                people = (int)Math.Min(people, Math.Min(budget, Math.Ceiling(deficit)));
                budget -= people;
                source.Population -= people;
                long arrive = Date.Hours + Math.Max(1, (long)Math.Ceiling(hours[target]));
                Migrations.Add(new Migration(_nextMigrationId++, player.Id, sourceId, target, people, Date.Hours, arrive, forced: false, source.Mood));
            }
        }
    }

    // ------------------------------------------------------------------ commands

    public CommandResult MoveUnit(int playerId, int unitId, int targetProvinceId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        if (targetProvinceId == unit.ProvinceId)
        {
            unit.Path.Clear();
            unit.StepHours = unit.HoursToNext = 0;
            return CommandResult.Success();
        }
        if (Pathfinder.FindPath(unit.ProvinceId, targetProvinceId) is not { } route)
            return CommandResult.Fail("No hay camino por tierra hasta allí.");
        var (path, hours) = route;
        unit.Path.Clear();
        unit.Path.AddRange(path);
        unit.StepHours = unit.HoursToNext = Pathfinder.StepHours(unit.ProvinceId, path[0]);
        return CommandResult.Success($"Llegada en {FormatHours(hours)}.");
    }

    public CommandResult CanFoundCity(Unit unit)
    {
        if (!unit.Info.CanFoundCity) return CommandResult.Fail("Solo los colonos pueden fundar ciudades.");
        if (unit.IsMoving) return CommandResult.Fail("La unidad está en marcha.");
        var p = Map.Provinces[unit.ProvinceId];
        if (!p.IsClaimable) return CommandResult.Fail("Aquí no se puede vivir.");
        if (p.OwnerId >= 0 && p.OwnerId != unit.OwnerId) return CommandResult.Fail("Esta provincia tiene dueño.");
        if (p.CityId.HasValue) return CommandResult.Fail("Ya hay una ciudad aquí.");
        if (p.Neighbors.Any(n => Map.Provinces[n].CityId.HasValue)) return CommandResult.Fail("Demasiado cerca de otra ciudad.");
        return CommandResult.Success();
    }

    /// <summary>The settlers claim their province (if nobody owns it) and become the population of a new city.</summary>
    public CommandResult FoundCity(int playerId, int unitId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        var check = CanFoundCity(unit);
        if (!check.Ok) return check;

        var player = Players[playerId];
        var p = Map.Provinces[unit.ProvinceId];
        SetOwner(p, playerId);
        var city = new City(_nextCityId++, CityNames.Next(_usedCityNames, _random), playerId, p.Id, Date.Hours);
        Cities.Add(city);
        p.CityId = city.Id;
        Settle(p, unit.Citizens, GameRules.StartingMood);
        RemoveUnit(unit);

        bool capital = player.CapitalCityId is null;
        if (capital) player.CapitalCityId = city.Id;
        Notify(playerId, capital ? $"Fundada {city.Name}, capital de {player.Name}." : $"Fundada la ciudad de {city.Name}.");
        return CommandResult.Success();
    }

    public CommandResult CanClaim(Unit unit)
    {
        if (!unit.Info.IsMilitary) return CommandResult.Fail("Solo las unidades militares reclaman territorio.");
        if (unit.IsMoving) return CommandResult.Fail("La unidad está en marcha.");
        var p = Map.Provinces[unit.ProvinceId];
        if (!p.IsClaimable) return CommandResult.Fail(p.IsWater ? "El océano no se puede reclamar." : "Los polos no se pueden reclamar.");
        if (p.OwnerId == unit.OwnerId) return CommandResult.Fail("Ya es tuya.");
        if (p.OwnerId >= 0) return CommandResult.Fail("Esta provincia tiene dueño.");
        return CommandResult.Success();
    }

    /// <summary>A military unit takes the unowned province it stands in.</summary>
    public CommandResult Claim(int playerId, int unitId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        var check = CanClaim(unit);
        if (!check.Ok) return check;
        SetOwner(Map.Provinces[unit.ProvinceId], playerId);
        if (Players[playerId].IsHuman) Notify(playerId, "Provincia reclamada. Los colonos de tus ciudades empezarán a llegar.");
        return CommandResult.Success();
    }

    public CommandResult CanRecruit(City city, UnitType type)
    {
        var info = type.Info();
        var p = Map.Provinces[city.ProvinceId];
        if (p.Population - info.Citizens < GameRules.MinCityPopulation)
            return CommandResult.Fail($"Hacen falta {info.Citizens + GameRules.MinCityPopulation} habitantes.");
        if (!Players[city.OwnerId].Stockpile.Has(info.Cost)) return CommandResult.Fail($"Cuesta {info.Cost}.");
        return CommandResult.Success();
    }

    public CommandResult Recruit(int playerId, int cityId, UnitType type)
    {
        if (CityById(cityId) is not { } city || city.OwnerId != playerId) return CommandResult.Fail("Ciudad no válida.");
        var check = CanRecruit(city, type);
        if (!check.Ok) return check;
        var info = type.Info();
        Players[playerId].Stockpile.TrySpend(info.Cost);
        Map.Provinces[city.ProvinceId].Population -= info.Citizens;
        AddUnit(playerId, type, city.ProvinceId, info.Citizens);
        return CommandResult.Success();
    }

    /// <summary>The unit's citizens settle in the (own) province it stands in.</summary>
    public CommandResult Disband(int playerId, int unitId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        var p = Map.Provinces[unit.ProvinceId];
        if (p.OwnerId != playerId) return CommandResult.Fail("Solo puede asentarse en una provincia propia.");
        Settle(p, unit.Citizens, GameRules.StartingMood);
        RemoveUnit(unit);
        return CommandResult.Success();
    }

    public CommandResult CanHoldFestival(City city)
    {
        if (city.HasFestival(Date.Hours)) return CommandResult.Fail($"Ya está de fiesta ({FormatHours(city.FestivalUntilHours - Date.Hours)} más).");
        double cost = GameRules.FestivalCost(Map.Provinces[city.ProvinceId].Population);
        if (Players[city.OwnerId].Stockpile[ResourceType.Gold] < cost) return CommandResult.Fail($"Cuesta {cost:0} de oro.");
        return CommandResult.Success();
    }

    /// <summary>The player pays gold for a festival that lifts the city's mood for <see cref="GameRules.FestivalDays"/> days.</summary>
    public CommandResult HoldFestival(int playerId, int cityId)
    {
        if (CityById(cityId) is not { } city || city.OwnerId != playerId) return CommandResult.Fail("Ciudad no válida.");
        var check = CanHoldFestival(city);
        if (!check.Ok) return check;
        Players[playerId].Stockpile[ResourceType.Gold] -= GameRules.FestivalCost(Map.Provinces[city.ProvinceId].Population);
        city.FestivalUntilHours = Date.Hours + GameRules.FestivalDays * 24;
        return CommandResult.Success($"{city.Name} celebra fiestas durante {GameRules.FestivalDays} días.");
    }

    public CommandResult CanForceMigration(int playerId, int fromId, int toId, int people)
    {
        var from = Map.Provinces[fromId];
        var to = Map.Provinces[toId];
        if (from.OwnerId != playerId || to.OwnerId != playerId) return CommandResult.Fail("Origen y destino deben ser tuyos.");
        if (fromId == toId) return CommandResult.Fail("Elige otra provincia de destino.");
        if (people < 1) return CommandResult.Fail("Elige cuántos ciudadanos.");
        int keep = from.CityId.HasValue ? GameRules.MinCityPopulation : 0;
        if (from.Population - people < keep) return CommandResult.Fail($"Solo pueden irse {Math.Max(0, (int)from.Population - keep)}.");
        double cost = GameRules.ForcedMigrationCost(people);
        if (Players[playerId].Stockpile[ResourceType.Gold] < cost) return CommandResult.Fail($"Cuesta {cost:0} de oro.");
        return CommandResult.Success();
    }

    /// <summary>The player pays gold to send a chosen number of citizens between two of their provinces.</summary>
    public CommandResult ForceMigration(int playerId, int fromId, int toId, int people)
    {
        var check = CanForceMigration(playerId, fromId, toId, people);
        if (!check.Ok) return check;
        if (Pathfinder.FindPath(fromId, toId) is not { } route) return CommandResult.Fail("No hay camino por tierra hasta allí.");
        double hours = route.Hours;
        Players[playerId].Stockpile[ResourceType.Gold] -= GameRules.ForcedMigrationCost(people);
        Map.Provinces[fromId].Population -= people;
        long arrive = Date.Hours + Math.Max(1, (long)Math.Ceiling(hours));
        double mood = Math.Max(0, Map.Provinces[fromId].Mood - GameRules.ForcedMigrantMoodPenalty);
        Migrations.Add(new Migration(_nextMigrationId++, playerId, fromId, toId, people, Date.Hours, arrive, forced: true, mood));
        return CommandResult.Success($"{people} ciudadanos en camino; llegarán en {FormatHours(hours)}.");
    }

    // ------------------------------------------------------------------ helpers

    private void SetOwner(Province p, int playerId)
    {
        if (p.OwnerId >= 0) Players[p.OwnerId].Provinces.Remove(p.Id);
        p.OwnerId = playerId;
        Players[playerId].Provinces.Add(p.Id);
        OwnershipChanged?.Invoke(p.Id);
    }

    /// <summary>Raised with the province id whenever a province changes hands (the client recolours the map).</summary>
    public event Action<int>? OwnershipChanged;

    internal Unit AddUnit(int ownerId, UnitType type, int provinceId, int citizens)
    {
        var unit = new Unit(_nextUnitId++, ownerId, type, provinceId, citizens);
        Units.Add(unit);
        _unitsById[unit.Id] = unit;
        return unit;
    }

    private void RemoveUnit(Unit unit)
    {
        Units.Remove(unit);
        _unitsById.Remove(unit.Id);
    }

    /// <summary>Moves people into a province, blending their mood with the residents' by headcount.</summary>
    private static void Settle(Province p, double people, double mood)
    {
        double total = p.Population + people;
        if (total <= 0) return;
        p.Mood = (p.Mood * p.Population + mood * people) / total;
        p.Population = total;
    }

    private void Notify(int playerId, string text) => Notifications.Add(new Notification(Date.Hours, playerId, text));

    public static string FormatHours(double hours)
    {
        if (hours < 24) return $"{Math.Ceiling(hours):0} h";
        int days = (int)(hours / 24);
        int rest = (int)Math.Ceiling(hours - days * 24);
        return rest == 0 ? $"{days} d" : $"{days} d {rest} h";
    }

    /// <summary>
    /// Habitable, fertile provinces as far from each other as the map allows, on landmasses big
    /// enough to expand over (land units can't cross the sea).
    /// </summary>
    private List<int> PickStartProvinces(int count)
    {
        const int MinLandmassProvinces = 200;
        var landmassSize = LandmassSizes();
        var candidates = Map.Provinces
            .Where(p => p.IsClaimable && p.Info.FoodYield >= 1.0 && p.Info.Carrying >= 6 && p.Neighbors.Length > 2)
            .Where(p => landmassSize[p.Id] >= MinLandmassProvinces)
            .Select(p => p.Id)
            .OrderBy(_ => _random.Next())
            .ToList();
        if (candidates.Count < count) throw new InvalidOperationException("El mapa no tiene suficiente tierra fértil.");

        for (double spacing = 4000; ; spacing *= 0.8)
        {
            var chosen = new List<int>();
            foreach (int c in candidates)
            {
                if (chosen.All(o => Map.DistanceKm(Map.Provinces[o], Map.Provinces[c]) >= spacing)) chosen.Add(c);
                if (chosen.Count == count) return chosen;
            }
        }
    }

    /// <summary>For each province, how many provinces its land-connected mass has (0 for water).</summary>
    private int[] LandmassSizes()
    {
        var size = new int[Map.Provinces.Count];
        var stack = new Stack<int>();
        var members = new List<int>();
        foreach (var start in Map.Provinces)
        {
            if (start.IsWater || size[start.Id] > 0) continue;
            members.Clear();
            size[start.Id] = -1;
            stack.Push(start.Id);
            while (stack.Count > 0)
            {
                int id = stack.Pop();
                members.Add(id);
                foreach (int n in Map.Provinces[id].Neighbors)
                {
                    if (size[n] != 0 || Map.Provinces[n].IsWater) continue;
                    size[n] = -1;
                    stack.Push(n);
                }
            }
            foreach (int id in members) size[id] = members.Count;
        }
        return size;
    }
}
