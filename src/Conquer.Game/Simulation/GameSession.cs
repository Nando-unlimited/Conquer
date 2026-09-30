using Conquer.Game.AI;
using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.Science;
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
/// <param name="Reserves">What is left in the deposits of the nation's provinces, by resource.</param>
public sealed record NationStats(
    double Settled, int InUnits, int Migrating, int Provinces, int Cities, int Units,
    double AverageMood, double AverageFertility, double[] PopulationByMood, double[] Reserves)
{
    public double Total => Settled + InUnits + Migrating;
}

/// <summary>
/// One running game. Time advances in fixed one-hour steps through <see cref="Step"/>; the economy
/// and migration run once per in-game day, at midnight. Player 0 is the human.
/// </summary>
public sealed partial class GameSession
{
    public const int HumanPlayerId = 0;

    private readonly Random _random;
    private readonly int _seed;
    private bool _computerRivals;
    private readonly List<AiPlayer> _ais = [];
    private readonly Dictionary<int, Unit> _unitsById = [];
    private readonly HashSet<string> _usedCityNames = [];
    private readonly HashSet<string> _usedProvinceNames = [];
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
    /// <summary>Chosen with the map, so it travels in its settings.</summary>
    public Difficulty Difficulty => Map.Settings?.Difficulty ?? Difficulty.Normal;

    /// <summary>What a player's land and cities yield: computer rivals get the difficulty's multiplier.</summary>
    private double OutputMultiplier(Player player) => !player.IsHuman && _computerRivals ? Difficulty.Info().ComputerOutput : 1;

    private GameSession(WorldMap map, int seed)
    {
        Map = map;
        Pathfinder = new Pathfinder(map);
        _seed = seed;
        _random = new Random(seed);
    }

    /// <summary>
    /// Sets up a new game: nobody owns any land; each player gets one band of settlers on a
    /// habitable province, spread as far apart as possible, plus the starting stockpile. Tests can turn
    /// the computer rivals off so nobody else moves their units.
    /// </summary>
    public static GameSession Create(WorldMap map, int playerCount, int seed, bool computerRivals = true)
    {
        ResetProvinces(map);
        var session = new GameSession(map, seed) { _computerRivals = computerRivals };
        var names = PlayerNames.Pick(playerCount, session._random);
        var starts = session.PickStartProvinces(playerCount);

        for (int i = 0; i < playerCount; i++)
        {
            var player = new Player(i, names[i], PlayerNames.Colors[i % PlayerNames.Colors.Length], i == HumanPlayerId);
            // The difficulty sets how much the human starts with; rivals always start with the standard stockpile.
            double start = player.IsHuman ? session.Difficulty.Info().StartingResources : 1;
            player.Stockpile[ResourceType.Food] = GameRules.StartingFood * start;
            player.Stockpile[ResourceType.Gold] = GameRules.StartingGold * start;
            player.Stockpile[ResourceType.Wood] = GameRules.StartingWood * start;
            session.Players.Add(player);
            session.AddUnit(i, UnitType.Settlers, starts[i], GameRules.StartingCitizens);
            session.AddTemplate(player, [BattalionType.Warriors, BattalionType.Warriors]);
            if (!player.IsHuman && computerRivals) session._ais.Add(new AiPlayer(session, player, seed + 100 + i));
        }

        session.Notify(HumanPlayerId, "Tus colonos esperan órdenes. Busca una buena tierra y funda tu primera ciudad.");
        session.Notify(HumanPlayerId, "Tus ciudades investigarán en tres ramas: Economía, Sociedad y Militar. Elige qué investigar en cada una en la pantalla de la nación (N).");
        return session;
    }

    /// <summary>Leaves every province unowned, empty and with full deposits, as at the start of a game.</summary>
    private static void ResetProvinces(WorldMap map)
    {
        foreach (var p in map.Provinces)
        {
            p.OwnerId = -1;
            p.ControllerId = -1;
            p.Population = 0;
            p.CityId = null;
            p.Mood = GameRules.StartingMood;
            p.Fertility = 1;
            p.Institutions.Clear();
            p.Name = "";
            p.ClearBuildings();
            foreach (var r in Resources.Deposits)
                p.Reserves[(int)r] = p.DepositSizes[(int)r] * GameRules.DepositSizeMultiplier;
        }
    }

    public Unit? UnitById(int id) => _unitsById.GetValueOrDefault(id);
    public City? CityById(int id) => Cities.FirstOrDefault(c => c.Id == id);
    public City? CityIn(Province p) => p.CityId is int id ? CityById(id) : null;

    /// <summary>Citizens a province's land can feed, including the bonus of a city.</summary>
    public double CapacityOf(Province p) =>
        p.Capacity * (p.CityId.HasValue ? GameRules.CityCapacityMultiplier : 1) * (1 + BonusesOf(p).Capacity);

    /// <summary>What improves a province: its owner's advances plus its own buildings.</summary>
    public Modifiers BonusesOf(Province p) => p.OwnerId >= 0 ? Players[p.OwnerId].Bonuses + p.BuildingBonuses : p.BuildingBonuses;

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
                // Administration spares part of it.
                factors.Add(("Lejos de la capital", -Math.Min(GameRules.MaxDistanceMoodPenalty, km / GameRules.KmPerMoodPoint) * (1 - owner.Bonuses.DistanceMood)));
            }
        }
        if (CityIn(p) is { } city && city.HasFestival(Date.Hours)) factors.Add(("Fiestas", GameRules.FestivalMood));
        double reserve = GameRules.FoodReserveMood * Math.Min(1, owner.FoodReserveDays / GameRules.FoodReserveFullDays);
        if (reserve >= 0.5) factors.Add(("Reservas de comida", reserve));
        double capacity = CapacityOf(p);
        if (p.Population > capacity)
            factors.Add(("Hacinamiento", -GameRules.MaxOvercrowdingMoodPenalty * Math.Min(1, p.Population / capacity - 1)));
        if (owner.IsStarving) factors.Add(("Hambre", GameRules.StarvingMood));
        if (p.IsOccupied) factors.Add(("Ocupada por el enemigo", MilitaryRules.OccupiedMood));
        foreach (var tech in owner.Techs.Where(t => t.Info().Effects.Mood != 0))
            factors.Add((tech.Info().Name, tech.Info().Effects.Mood));
        foreach (var building in p.Buildings.Where(b => b.Info().Effects.Mood != 0))
            factors.Add((building.Info().Name, building.Info().Effects.Mood));
        return factors;
    }

    public double TargetMood(Province p) => Math.Clamp(MoodFactors(p).Sum(f => f.Points), 0, 100);

    /// <summary>Totals and averages of a player's nation, for the nation screen.</summary>
    public NationStats Stats(Player player)
    {
        double settled = 0, mood = 0, fertility = 0;
        var byMood = new double[GameRules.MoodNames.Length];
        var reserves = new double[Resources.All.Length];
        foreach (int id in player.Provinces)
        {
            var p = Map.Provinces[id];
            foreach (var r in Resources.Deposits) reserves[(int)r] += p.Reserves[(int)r];
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
            byMood,
            reserves);
    }

    // ------------------------------------------------------------------ time

    /// <summary>Advances the game by one hour.</summary>
    public void Step()
    {
        Date = new GameDate(Date.Hours + 1);
        MoveUnits();
        ResolveBattles();
        ResolveNavalBattles();
        ArriveMigrations();
        if (Date.Hour == 0)
        {
            foreach (var player in Players) DailyEconomy(player);
            foreach (var player in Players) DailyMigration(player);
            foreach (var player in Players) DailyScience(player);
            DailyInstitutions();
            foreach (var player in Players) DailyConstruction(player);
            foreach (var player in Players) DailyMilitary(player);
        }
        if (Date.Hours % 6 == 0)
            foreach (var ai in _ais) ai.Think(dailyDecisions: Date.Hour == 0);
    }

    private void ArriveMigrations()
    {
        for (int i = Migrations.Count - 1; i >= 0; i--)
        {
            var m = Migrations[i];
            if (m.ArriveHours > Date.Hours) continue;
            Migrations.RemoveAt(i);
            var target = Map.Provinces[m.ToProvinceId];
            if (target.OwnerId == m.OwnerId && !target.IsOccupied)
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
            // An occupied province neither works nor eats for its owner.
            if (pop <= 0 || p.IsOccupied) continue;
            population += pop;
            double capacity = CapacityOf(p);
            double worked = Math.Min(pop, capacity);
            double crowded = Math.Max(0, pop - capacity);
            double output = GameRules.MoodProductivity(p.Mood);
            var bonus = BonusesOf(p);
            net[(int)ResourceType.Food] += GameRules.FoodPerWorker * p.FoodYield * (worked + crowded * GameRules.OvercrowdedFoodShare) * output * (1 + bonus.Food);
            net[(int)ResourceType.Wood] += p.Info.WoodYield / 1000 * worked * output * (1 + bonus.Wood);
            if (p.Mood >= GameRules.UnrestMood) net[(int)ResourceType.Gold] += GameRules.TaxGoldPerCitizen * pop * output * (1 + bonus.Taxes);
            double workforce = Math.Min(1, pop / GameRules.DepositFullWorkers);
            foreach (var r in Resources.Deposits)
                if (p.HasDeposit(r) && player.Knows(r)) net[(int)r] += Extract(p, r, p.Deposits[(int)r] * workforce * output * (1 + bonus.Deposits));
        }
        // Applied after extracting, so a rival's extra output does not empty its deposits faster.
        double multiplier = OutputMultiplier(player);
        for (int i = 0; i < net.Length; i++) net[i] *= multiplier;

        double eaters = population
                        + Units.Where(u => u.OwnerId == player.Id).Sum(u => u.Citizens)
                        + Migrations.Where(m => m.OwnerId == player.Id).Sum(m => m.People);
        net[(int)ResourceType.Food] -= GameRules.FoodPerCitizen * eaters;
        var upkeep = Upkeep(player);
        for (int i = 0; i < net.Length; i++) net[i] -= upkeep[i];

        foreach (var r in Resources.All) player.Stockpile[r] += net[(int)r];
        Array.Copy(net, player.LastDayNet, net.Length);

        // An army the nation cannot pay loses heart and men (see DailyMilitary); what is owed is forgiven.
        bool unpaid = Resources.All.Any(r => r != ResourceType.Food && upkeep[(int)r] > 0 && player.Stockpile[r] < 0);
        foreach (var r in Resources.All.Where(r => r != ResourceType.Food)) player.Stockpile[r] = Math.Max(0, player.Stockpile[r]);
        if (unpaid && !player.ArmyUnpaid && player.IsHuman)
            Notify(player.Id, "No hay con qué pagar al ejército: las tropas pierden organización y desertan. Licencia unidades o consigue más oro.");
        player.ArmyUnpaid = unpaid;

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
            UpdateMoodAndFertility(p, player, starving);
            if (starving)
            {
                // The nation's advances and the province's granary each spare their share of those who would die.
                p.Population *= 1 - GameRules.StarvationRate * (1 - player.Bonuses.FamineSurvival) * (1 - p.BuildingBonuses.FamineSurvival);
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

    /// <summary>Fertility a populated province tends to, with its owner's advances.</summary>
    public double TargetFertility(Province p, Player owner, bool starving) =>
        GameRules.TargetFertility(p.Mood, starving) * (1 + owner.Bonuses.Fertility + p.BuildingBonuses.Fertility);

    // ------------------------------------------------------------------ science

    /// <summary>Science points a player's cities produce per day, scaled by their mood and advances (and the difficulty for rivals).</summary>
    public double SciencePerDay(Player player)
    {
        double points = 0;
        foreach (var city in Cities.Where(c => c.OwnerId == player.Id && !Map.Provinces[c.ProvinceId].IsOccupied))
        {
            var p = Map.Provinces[city.ProvinceId];
            points += (GameRules.ScienceBasePerCity + p.Population * GameRules.SciencePerCityCitizen) * GameRules.MoodProductivity(p.Mood)
                      * (1 + player.Bonuses.Science + p.BuildingBonuses.Science);
        }
        return points * OutputMultiplier(player);
    }

    /// <summary>
    /// The day's science (plus what was left over) is shared among the branches researching something, by their
    /// priorities. A branch takes no more than its advance still needs; the rest goes to the others, and what nobody
    /// can take (nothing chosen) is kept for the next day. A finished advance is learned and its branch waits for a new choice.
    /// </summary>
    private void DailyScience(Player player)
    {
        double points = SciencePerDay(player);
        player.LastDayScience = points;
        double pool = points + player.SpareScience;
        player.SpareScience = 0;
        var neighbours = NeighbourNations(player);

        // Each round hands out what the last one could not place; one branch fills up per round at least.
        for (int round = 0; round < Techs.Branches.Length && pool > 0; round++)
        {
            var open = player.Researching.OfType<Tech>().Where(t => player.ResearchProgress[(int)t] < ResearchCost(player, t, neighbours)).ToList();
            if (open.Count == 0) break;
            double shares = open.Sum(t => player.ScienceShare(t.Info().Branch));
            double left = 0;
            foreach (var tech in open)
            {
                double offered = pool * (shares > 0 ? player.ScienceShare(tech.Info().Branch) / shares : 1.0 / open.Count);
                double taken = Math.Min(offered, ResearchCost(player, tech, neighbours) - player.ResearchProgress[(int)tech]);
                player.ResearchProgress[(int)tech] += taken;
                left += offered - taken;
            }
            pool = left;
        }
        player.SpareScience += pool;

        foreach (var branch in Techs.Branches)
        {
            if (player.Researching[(int)branch] is not Tech tech || player.ResearchProgress[(int)tech] < ResearchCost(player, tech, neighbours)) continue;
            player.Learn(tech);
            player.Researching[(int)branch] = null;
            if (player.IsHuman)
                Notify(player.Id, $"Descubrimiento: {tech.Info().Name}. {tech.Info().Description} Elige el siguiente avance de {branch.Name()} (N).");
        }
    }

    /// <summary>Whether a level of a branch is open: the first always, the rest once enough of the level below is known.</summary>
    public static bool IsLevelOpen(Player player, TechBranch branch, int level) =>
        level <= 1 || Techs.InLevel(branch, level - 1).Count(player.Techs.Contains) >= Techs.NeededToOpenNext(branch, level - 1);

    public static CommandResult CanResearch(Player player, Tech tech)
    {
        var info = tech.Info();
        if (player.Techs.Contains(tech)) return CommandResult.Fail("Ya lo conoces.");
        if (!IsLevelOpen(player, info.Branch, info.Level))
        {
            int needed = Techs.NeededToOpenNext(info.Branch, info.Level - 1);
            return CommandResult.Fail($"Requiere {needed} {(needed == 1 ? "avance" : "avances")} del nivel {info.Level - 1} de {info.Branch.Name()}.");
        }
        if (MissingRequirements(player, tech).Count > 0) return CommandResult.Fail($"Requiere {RequirementList(player, tech)}.");
        return CommandResult.Success();
    }

    /// <summary>Points its branch's science at an advance, instead of the one it was researching. Saved science goes into it at once.</summary>
    public CommandResult Research(int playerId, Tech tech)
    {
        var player = Players[playerId];
        var check = CanResearch(player, tech);
        if (!check.Ok) return check;
        player.Researching[(int)tech.Info().Branch] = tech;
        player.ResearchProgress[(int)tech] += player.SpareScience;
        player.SpareScience = 0;
        return CommandResult.Success();
    }

    /// <summary>Advances it needs that the player does not know yet.</summary>
    public static List<Tech> MissingRequirements(Player player, Tech tech) => tech.Info().Requires.Where(t => !player.Techs.Contains(t)).ToList();

    public static string RequirementList(Player player, Tech tech) =>
        string.Join(" y ", MissingRequirements(player, tech).Select(t => t.Info().Name.ToLowerInvariant()));

    /// <summary>Nations owning a province next to one of the player's.</summary>
    public HashSet<int> NeighbourNations(Player player)
    {
        var nations = new HashSet<int>();
        foreach (int id in player.Provinces)
            foreach (int n in Map.Provinces[id].Neighbors)
                if (Map.Provinces[n].OwnerId is int owner and >= 0 && owner != player.Id) nations.Add(owner);
        return nations;
    }

    /// <summary>What an advance costs the player: cheaper for each bordering nation that already knows it, dearer while its age's institution is not adopted.</summary>
    public double ResearchCost(Player player, Tech tech, IReadOnlySet<int>? neighbours = null)
    {
        neighbours ??= NeighbourNations(player);
        int knowers = Math.Min(GameRules.MaxNeighbourDiscounts, neighbours.Count(id => Players[id].Techs.Contains(tech)));
        return tech.Info().Cost * (1 - GameRules.NeighbourResearchDiscount * knowers) * EraCostMultiplier(player, tech.Info().Era);
    }

    /// <summary>How much of the nation's science a branch gets, relative to the other two.</summary>
    public CommandResult SetResearchPriority(int playerId, TechBranch branch, int priority)
    {
        if (priority < 0 || priority > GameRules.MaxResearchPriority)
            return CommandResult.Fail($"La prioridad va de 0 a {GameRules.MaxResearchPriority}.");
        Players[playerId].ResearchPriorities[(int)branch] = priority;
        return CommandResult.Success();
    }

    // ------------------------------------------------------------------ buildings

    /// <summary>
    /// Whether the building could go up in this province one day, ignoring cost and ongoing work:
    /// its advance is known, and the province has a city or a deposit if the building needs one.
    /// </summary>
    public CommandResult IsBuildingAvailable(Province p, BuildingType type)
    {
        var info = type.Info();
        if (p.OwnerId < 0) return CommandResult.Fail("La provincia no es de nadie.");
        if (info.RequiresTech is Tech tech && !Players[p.OwnerId].Techs.Contains(tech))
            return CommandResult.Fail($"Requiere {tech.Info().Name.ToLowerInvariant()}.");
        if (info.CityOnly && !p.CityId.HasValue) return CommandResult.Fail("Solo en provincias con ciudad.");
        if (info.NeedsCoast && !p.Neighbors.Any(n => Map.Provinces[n].IsWater)) return CommandResult.Fail("Solo en provincias con costa.");
        if (info.RequiresBuilding is BuildingType first && !p.Buildings.Contains(first))
            return CommandResult.Fail($"Requiere {first.Info().Name.ToLowerInvariant()}.");
        if (info.NeedsDeposit && !Resources.Deposits.Any(r => p.HasDeposit(r) && Players[p.OwnerId].Knows(r)))
            return CommandResult.Fail("Requiere un yacimiento conocido y sin agotar.");
        return CommandResult.Success();
    }

    public CommandResult CanBuild(int playerId, Province p, BuildingType type)
    {
        if (p.OwnerId != playerId) return CommandResult.Fail("La provincia no es tuya.");
        if (p.IsOccupied) return CommandResult.Fail("La provincia está ocupada por el enemigo.");
        if (p.Buildings.Contains(type)) return CommandResult.Fail("Ya está construido.");
        var available = IsBuildingAvailable(p, type);
        if (!available.Ok) return available;
        if (Busy(p) is { } busy) return busy;
        if (p.Population < GameRules.SettledPopulation) return CommandResult.Fail($"Hacen falta al menos {GameRules.SettledPopulation} habitantes.");
        if (!Players[playerId].Stockpile.Has(type.Info().Cost)) return CommandResult.Fail($"Cuesta {type.Info().Cost}.");
        return CommandResult.Success();
    }

    /// <summary>Days a work takes the player: its normal days, fewer with advances that speed building up.</summary>
    public static int BuildDays(Player player, int days) => (int)Math.Ceiling(days / (1 + player.Bonuses.BuildSpeed));

    /// <summary>Pays for a building and starts its construction; it takes <see cref="BuildingInfo.Days"/> days (see <see cref="BuildDays"/>).</summary>
    public CommandResult Build(int playerId, int provinceId, BuildingType type)
    {
        var p = Map.Provinces[provinceId];
        var check = CanBuild(playerId, p, type);
        if (!check.Ok) return check;
        Players[playerId].Stockpile.TrySpend(type.Info().Cost);
        p.Constructing = type;
        p.ConstructionDaysLeft = BuildDays(Players[playerId], type.Info().Days);
        return CommandResult.Success($"{type.Info().Name} en obras: {p.ConstructionDaysLeft} días.");
    }

    /// <summary>Why nothing new can start here: a building or a city already under construction.</summary>
    private static CommandResult? Busy(Province p) =>
        p.Constructing is BuildingType building ? CommandResult.Fail($"Ya se está construyendo {building.Info().Name.ToLowerInvariant()}.")
        : p.PlannedCityName != null ? CommandResult.Fail($"Ya se está construyendo la ciudad de {p.PlannedCityName}.")
        : null;

    /// <summary>
    /// Whether the citizens of a province of the player's could build themselves a city, cost aside: enough
    /// of them, no city in it or next to it (built or being built), and nothing else under construction.
    /// </summary>
    public CommandResult IsCitySite(int playerId, Province p)
    {
        if (p.OwnerId != playerId) return CommandResult.Fail("La provincia no es tuya.");
        if (p.IsOccupied) return CommandResult.Fail("La provincia está ocupada por el enemigo.");
        if (p.CityId.HasValue) return CommandResult.Fail("Ya hay una ciudad aquí.");
        if (Busy(p) is { } busy) return busy;
        if (p.Neighbors.Any(n => Map.Provinces[n].CityId.HasValue || Map.Provinces[n].PlannedCityName != null))
            return CommandResult.Fail("Demasiado cerca de otra ciudad.");
        if (p.Population < GameRules.CityBuildingPopulation)
            return CommandResult.Fail($"Hacen falta al menos {GameRules.CityBuildingPopulation} habitantes.");
        return CommandResult.Success();
    }

    public CommandResult CanBuildCity(int playerId, Province p)
    {
        var site = IsCitySite(playerId, p);
        if (!site.Ok) return site;
        if (!Players[playerId].Stockpile.Has(GameRules.CityCost)) return CommandResult.Fail($"Cuesta {GameRules.CityCost}.");
        return CommandResult.Success();
    }

    /// <summary>Pays for a city and starts building it; it is founded with this name after <see cref="GameRules.CityBuildingDays"/> days.</summary>
    public CommandResult BuildCity(int playerId, int provinceId, string name)
    {
        var p = Map.Provinces[provinceId];
        var check = CanBuildCity(playerId, p);
        if (!check.Ok) return check;
        var nameCheck = CheckCityName(name);
        if (!nameCheck.Ok) return nameCheck;
        Players[playerId].Stockpile.TrySpend(GameRules.CityCost);
        p.PlannedCityName = name.Trim();
        p.ConstructionDaysLeft = BuildDays(Players[playerId], GameRules.CityBuildingDays);
        return CommandResult.Success($"La ciudad de {p.PlannedCityName} en obras: {p.ConstructionDaysLeft} días.");
    }

    /// <summary>A city name must have letters, fit on the map and not belong to another city, built or planned.</summary>
    public CommandResult CheckCityName(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return CommandResult.Fail("Escribe un nombre para la ciudad.");
        if (name.Length > GameRules.MaxCityNameLength) return CommandResult.Fail($"El nombre es demasiado largo (máximo {GameRules.MaxCityNameLength} letras).");
        if (Cities.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            || Map.Provinces.Any(p => name.Equals(p.PlannedCityName, StringComparison.OrdinalIgnoreCase)))
            return CommandResult.Fail($"Ya hay una ciudad llamada {name}.");
        return CommandResult.Success();
    }

    /// <summary>A city name nobody uses yet, to offer the player; it is not reserved.</summary>
    public string SuggestCityName() => CityNames.Suggest(_usedCityNames, Random.Shared);

    /// <summary>A free city name from the game's own random numbers, so computer rivals stay deterministic.</summary>
    internal string NextCityName() => CityNames.Suggest(_usedCityNames, _random);

    /// <summary>What to call a place in messages: its city if it has one, otherwise the province.</summary>
    public string PlaceName(Province p) => CityIn(p)?.Name ?? p.DisplayName;

    /// <summary>Every construction advances a day; finished buildings start working at once and finished cities are founded.</summary>
    private void DailyConstruction(Player player)
    {
        foreach (int id in player.Provinces)
        {
            var p = Map.Provinces[id];
            if ((!p.Constructing.HasValue && p.PlannedCityName == null) || p.IsOccupied || --p.ConstructionDaysLeft > 0) continue;
            p.ConstructionDaysLeft = 0;
            if (p.Constructing is BuildingType type)
            {
                p.AddBuilding(type);
                p.Constructing = null;
                if (player.IsHuman) Notify(player.Id, $"Terminada la obra: {type.Info().Name} en {PlaceName(p)}.");
            }
            else
            {
                string name = p.PlannedCityName!;
                p.PlannedCityName = null;
                AddCity(player, p, name);
            }
        }
    }

    /// <summary>Takes up to <paramref name="amount"/> from a deposit's pocket and returns what was taken.</summary>
    private double Extract(Province p, ResourceType r, double amount)
    {
        double taken = Math.Min(amount, p.Reserves[(int)r]);
        p.Reserves[(int)r] -= taken;
        if (p.Reserves[(int)r] <= 0)
        {
            p.Reserves[(int)r] = 0;
            if (p.OwnerId == HumanPlayerId)
                Notify(p.OwnerId, $"Se ha agotado el yacimiento de {r.Name().ToLowerInvariant()} en {PlaceName(p)}.");
        }
        return taken;
    }

    /// <summary>Mood closes part of the gap to its target each day; fertility follows mood and food, more slowly.</summary>
    private void UpdateMoodAndFertility(Province p, Player owner, bool starving)
    {
        double before = p.Mood;
        p.Mood += (TargetMood(p) - p.Mood) * GameRules.MoodChangePerDay;
        p.Fertility += (TargetFertility(p, owner, starving) - p.Fertility) * GameRules.FertilityChangePerDay;

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
            .Where(c => c.OwnerId == player.Id && !Map.Provinces[c.ProvinceId].IsOccupied
                        && Map.Provinces[c.ProvinceId].Population > GameRules.MinEmigrationCityPopulation)
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
            if (p.CityId.HasValue || p.IsOccupied || nearest[id] < 0) continue;
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

    public CommandResult CanFoundCity(Unit unit)
    {
        if (!unit.CanFoundCity) return CommandResult.Fail("Solo los colonos pueden fundar ciudades.");
        if (unit.IsMoving) return CommandResult.Fail("La unidad está en marcha.");
        var p = Map.Provinces[unit.ProvinceId];
        if (!p.IsClaimable) return CommandResult.Fail("Aquí no se puede vivir.");
        if (p.OwnerId >= 0 && p.OwnerId != unit.OwnerId) return CommandResult.Fail("Esta provincia tiene dueño.");
        if (p.IsOccupied) return CommandResult.Fail("La provincia está ocupada por el enemigo.");
        if (p.CityId.HasValue) return CommandResult.Fail("Ya hay una ciudad aquí.");
        if (p.PlannedCityName != null) return CommandResult.Fail($"Ya se está construyendo la ciudad de {p.PlannedCityName}.");
        if (p.Neighbors.Any(n => Map.Provinces[n].CityId.HasValue || Map.Provinces[n].PlannedCityName != null))
            return CommandResult.Fail("Demasiado cerca de otra ciudad.");
        return CommandResult.Success();
    }

    /// <summary>
    /// The settlers claim their province (if nobody owns it) and become the population of a new city,
    /// called <paramref name="name"/> or, without one, a name of the game's choosing.
    /// </summary>
    public CommandResult FoundCity(int playerId, int unitId, string? name = null)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        var check = CanFoundCity(unit);
        if (!check.Ok) return check;
        if (name != null && CheckCityName(name) is { Ok: false } badName) return badName;

        var p = Map.Provinces[unit.ProvinceId];
        SetOwner(p, playerId);
        Settle(p, unit.Citizens, GameRules.StartingMood);
        RemoveUnit(unit);
        AddCity(Players[playerId], p, name?.Trim() ?? CityNames.Next(_usedCityNames, _random));
        return CommandResult.Success();
    }

    /// <summary>Founds a city in a province of the player's; the first one becomes the capital.</summary>
    private void AddCity(Player player, Province p, string name)
    {
        _usedCityNames.Add(name);
        var city = new City(_nextCityId++, name, player.Id, p.Id, Date.Hours);
        Cities.Add(city);
        p.CityId = city.Id;

        bool capital = player.CapitalCityId is null;
        if (capital) player.CapitalCityId = city.Id;
        Notify(player.Id, capital ? $"Fundada {city.Name}, capital de {player.Name}." : $"Fundada la ciudad de {city.Name} en {p.DisplayName}.");
    }

    public CommandResult CanClaim(Unit unit)
    {
        if (!unit.IsMilitary) return CommandResult.Fail("Solo las unidades militares reclaman territorio.");
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
        var claimed = Map.Provinces[unit.ProvinceId];
        SetOwner(claimed, playerId);
        if (Players[playerId].IsHuman) Notify(playerId, $"Reclamas la provincia y la llamas {claimed.Name}. Los colonos de tus ciudades empezarán a llegar.");
        return CommandResult.Success();
    }

    public CommandResult CanRecruitSettlers(City city)
    {
        var p = Map.Provinces[city.ProvinceId];
        if (p.IsOccupied) return CommandResult.Fail("La ciudad está ocupada por el enemigo.");
        if (p.Population - GameRules.SettlerCitizens < GameRules.MinCityPopulation)
            return CommandResult.Fail($"Hacen falta {GameRules.SettlerCitizens + GameRules.MinCityPopulation} habitantes.");
        if (!Players[city.OwnerId].Stockpile.Has(GameRules.SettlersCost)) return CommandResult.Fail($"Cuesta {GameRules.SettlersCost}.");
        return CommandResult.Success();
    }

    /// <summary>A band of settlers leaves the city at once to found another one.</summary>
    public CommandResult RecruitSettlers(int playerId, int cityId)
    {
        if (CityById(cityId) is not { } city || city.OwnerId != playerId) return CommandResult.Fail("Ciudad no válida.");
        var check = CanRecruitSettlers(city);
        if (!check.Ok) return check;
        Players[playerId].Stockpile.TrySpend(GameRules.SettlersCost);
        Map.Provinces[city.ProvinceId].Population -= GameRules.SettlerCitizens;
        AddUnit(playerId, UnitType.Settlers, city.ProvinceId, GameRules.SettlerCitizens);
        return CommandResult.Success();
    }

    /// <summary>The unit's citizens settle in the (own) province it stands in.</summary>
    public CommandResult Disband(int playerId, int unitId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        var p = Map.Provinces[unit.ProvinceId];
        if (p.OwnerId != playerId) return CommandResult.Fail("Solo puede asentarse en una provincia propia.");
        // A fleet paid off in port lands what it carries first.
        foreach (var cargo in Units.Where(u => u.CarrierId == unit.Id)) cargo.CarrierId = null;
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
        if (from.IsOccupied || to.IsOccupied) return CommandResult.Fail("La provincia está ocupada por el enemigo.");
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
        // A city still being built belongs to the people who planned it; a new owner starts over.
        if (p.OwnerId != playerId) p.PlannedCityName = null;
        p.OwnerId = p.ControllerId = playerId;
        Players[playerId].Provinces.Add(p.Id);
        NameProvince(p);
        OwnershipChanged?.Invoke(p.Id);
    }

    /// <summary>The first nation to claim a province names it; the name stays whoever holds it later.</summary>
    private void NameProvince(Province p)
    {
        if (p.Name.Length == 0 && p.IsClaimable) p.Name = ProvinceNames.Next(_usedProvinceNames, _random);
    }

    /// <summary>Raised with the province id whenever a province changes hands (the client recolours the map).</summary>
    public event Action<int>? OwnershipChanged;

    internal Unit AddUnit(int ownerId, UnitType type, int provinceId, int citizens, int number = 0, int headquartersLevel = 0)
    {
        var unit = new Unit(_nextUnitId++, Players[ownerId], type, provinceId, citizens, number, headquartersLevel);
        Units.Add(unit);
        _unitsById[unit.Id] = unit;
        return unit;
    }

    /// <summary>Takes a unit off the map: its subordinates lose their commander and it leaves any battle.</summary>
    private void RemoveUnit(Unit unit)
    {
        Units.Remove(unit);
        _unitsById.Remove(unit.Id);
        foreach (var sub in Units.Where(u => u.CommanderId == unit.Id)) sub.CommanderId = null;
        foreach (var battle in _battles) battle.Attackers.Remove(unit.Id);
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
