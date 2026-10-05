using Conquer.Game.AI;
using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>Saving a game to a <see cref="SaveGame"/> and carrying on from one.</summary>
public sealed partial class GameSession
{
    public SaveGame ToSave(string gameVersion) => new()
    {
        GameVersion = gameVersion,
        SavedAtUtc = DateTime.UtcNow,
        World = Map.Settings ?? new WorldSettings(Map.Kind, Map.Seed),
        MapFingerprint = Fingerprint(Map),
        ExtraDeposits = true,
        RealisticPopulation = true,
        ThreeCommandLevels = true,
        Barracks = true,
        Workshops = true,
        Hours = Date.Hours,
        ComputerRivals = _computerRivals,
        Players = Players.Select(p => new PlayerSave(
            p.Id, p.Name, p.Color, p.IsHuman, [.. Resources.All.Select(r => p.Stockpile[r])], p.CapitalCityId,
            [.. p.LastDayNet], p.IsStarving, p.FoodReserveDays, [.. p.Techs.Order()],
            [.. p.ResearchProgress], p.SpareScience, p.LastDayScience,
            [.. p.Templates.Select(t => new TemplateSave(t.Id, t.Number, [.. t.Battalions]))], [.. p.ResearchPriorities],
            [.. p.Researching.OfType<Tech>()], [.. p.Institutions.Order()], [.. p.OfficerReserve.Select(ToSave)], p.Eliminated, p.Manpower, p.ReligionId,
            [.. p.Explored.Order()])).ToList(),
        // Provinces nobody has touched keep their generated state, so only the rest are stored.
        Provinces = Map.Provinces.Where(Changed).Select(p => new ProvinceSave(
            p.Id, p.OwnerId, p.ControllerId, p.Population, p.CityId, p.Mood, p.Fertility, [.. p.Reserves],
            [.. p.Buildings.Order()], p.Constructing, p.ConstructionDaysLeft, p.PlannedCityName, [.. p.Institutions.Order()], p.Name,
            p.Training.Count == 0 ? null : [.. p.Training.Select(ToSave)], p.CultureId, p.Assimilation, p.RevoltProgress, p.ReligionId, p.Conversion,
            p.PlagueDaysLeft, p.PlagueImmuneUntil)).ToList(),
        Cities = Cities.Select(c => new CitySave(c.Id, c.Name, c.OwnerId, c.ProvinceId, c.FoundedHours, c.FestivalUntilHours)).ToList(),
        Units = Units.Select(u => new UnitSave(
            u.Id, u.OwnerId, u.Type, u.ProvinceId, u.Type is UnitType.Regiment or UnitType.Fleet ? 0 : u.Citizens, u.Number, u.HeadquartersLevel,
            [.. u.Battalions.Select(b => new BattalionSave(b.Type, b.Strength, b.Organisation, b.Experience))],
            u.CommanderId, u.AttackingProvinceId, [.. u.Path], u.HoursToNext, u.StepHours, u.CarrierId,
            Officer: u.Officer is { } o ? ToSave(o) : null, CustomName: u.CustomName, AutoClaim: u.AutoClaim)).ToList(),
        Migrations = Migrations.Select(m => new MigrationSave(m.Id, m.OwnerId, m.FromProvinceId, m.ToProvinceId, m.People,
            m.DepartHours, m.ArriveHours, m.Forced, m.Mood)).ToList(),
        Battles = _battles.Select(b => new BattleSave(b.ProvinceId, b.AttackerId, b.DefenderId, b.StartHours, [.. b.Attackers], b.AttackerLosses, b.DefenderLosses)).ToList(),
        Wars = _wars.Select(w => new WarSave(w.Key.Item1, w.Key.Item2, w.Value.StartHours,
            w.Value.Victories.GetValueOrDefault(w.Key.Item1), w.Value.Victories.GetValueOrDefault(w.Key.Item2))).ToList(),
        Truces = _truces.Where(t => t.Value > Date.Hours).Select(t => new TruceSave(t.Key.Item1, t.Key.Item2, t.Value)).ToList(),
        Sieges = _sieges.Values.OrderBy(x => x.ProvinceId).Select(x => new SiegeSave(x.ProvinceId, x.AttackerId, x.Progress)).ToList(),
        Alliances = _alliances.Order().Select(x => new AllianceSave(x.Item1, x.Item2)).ToList(),
        Memories = _memories.OrderBy(m => m.Key).SelectMany(m => m.Value.Select(x => new MemorySave(m.Key.From, m.Key.To, x.Reason, x.Value))).ToList(),
        Vassals = _vassals.OrderBy(v => v.Key).Select(v => new VassalSave(v.Key, v.Value.Overlord, v.Value.Since)).ToList(),
        Reparations = [.. _reparations],
        Pacts = _pacts.Order().Select(x => new AllianceSave(x.Item1, x.Item2)).ToList(),
        Access = _access.Order().Select(x => new AccessSave(x.Granter, x.Grantee)).ToList(),
        Trades = [.. _trades],
        NextTradeId = _nextTradeId,
        Decisions = [.. _decisions],
        MoodEvents = [.. _moodEvents.Where(m => m.UntilHours > Date.Hours)],
        NextDecisionId = _nextDecisionId,
        History = [.. _history],
        ObjectivesDone = [.. _objectivesDone.Order()],
        Outcome = Outcome,
        Notifications = [.. Notifications],
        Ais = _ais.Select(ai => ai.ToSave()).ToList(),
        EmigrationCarry = new Dictionary<int, double>(_emigrationCarry),
        UnitNumbers = _unitNumbers.Select(n => new UnitNumberSave(n.Key.Player, n.Key.Level, n.Value)).ToList(),
        NextUnitId = _nextUnitId,
        NextCityId = _nextCityId,
        NextMigrationId = _nextMigrationId,
        NextTemplateId = _nextTemplateId,
        NextOfficerId = _nextOfficerId,
        InstitutionBirths = _institutionBirths.OrderBy(b => b.Key).Select(b => new InstitutionBirthSave(b.Key, b.Value.ProvinceId, b.Value.Hours)).ToList(),
        Roads = Roads.Links.OrderBy(l => l.A).ThenBy(l => l.B).Select(l => new RoadLinkSave(l.A, l.B, l.Kind)).ToList(),
        RoadProjects = _roadProjects.Select(r => new RoadProjectSave(r.Id, r.OwnerId, r.Kind, [.. r.Route], r.DaysPerLink, r.Next, r.WorkLeft)).ToList(),
        NextRoadProjectId = _nextRoadProjectId,
    };

    /// <summary>
    /// The roads and railways, and the works under way. Saves from before 1.36.0 had roads and railways as buildings of
    /// a province: two neighbours that both had one are joined by it.
    /// </summary>
    private void LoadRoads(SaveGame save)
    {
        Roads.Clear();
        foreach (var l in save.Roads ?? []) Roads.Lay(l.A, l.B, l.Kind);
        foreach (var r in save.RoadProjects ?? []) _roadProjects.Add(new RoadProject(r.Id, r.OwnerId, r.Kind, r.Route, r.DaysPerLink, r.Next, r.WorkLeft));
        _nextRoadProjectId = save.NextRoadProjectId;
        if (save.Roads != null) return;
        foreach (var (building, kind) in new[] { (BuildingType.Road, RoadKind.Road), (BuildingType.Railway, RoadKind.Railway) })
        {
            var had = save.Provinces.Where(p => p.Buildings.Contains(building)).Select(p => p.Id).ToHashSet();
            foreach (int id in had)
                foreach (int n in Map.Provinces[id].Neighbors)
                    if (had.Contains(n)) Roads.Lay(id, n, kind);
        }
    }

    private static OfficerSave ToSave(Officer o) => new(o.Id, o.Name, [.. o.Traits], o.StartingSkill, o.Victories, o.Rank, o.Branch);

    private static Officer FromSave(OfficerSave o) => new(o.Id, o.Name, o.Traits, o.StartingSkill, o.Victories, o.Rank, o.Branch);

    /// <summary>Whether the province differs from how <see cref="ResetProvinces"/> leaves it.</summary>
    private static TrainingSave ToSave(TrainingOrder o) =>
        new(o.Battalion, o.TemplateName, [.. o.TemplateBattalions], o.HeadquartersLevel, o.DaysLeft, o.TotalDays);

    /// <summary>A saved order, its HQ level moved to the current levels by <paramref name="level"/>.</summary>
    private static TrainingOrder FromSave(TrainingSave o, Func<int, int> level) =>
        new(o.Battalion, o.TemplateName, o.TemplateBattalions, level(o.HeadquartersLevel), o.DaysLeft, o.TotalDays);

    private static bool Changed(Province p) =>
        p.OwnerId != -1 || p.ControllerId != -1 || p.Population != 0 || p.CityId.HasValue
        || p.Mood != GameRules.StartingMood || p.Fertility != 1 || p.Buildings.Count > 0 || p.Constructing.HasValue || p.PlannedCityName != null
        || p.Institutions.Count > 0 || p.Name.Length > 0 || p.Training.Count > 0 || p.CultureId != -1 || p.RevoltProgress != 0 || p.ReligionId != -1 || p.PlagueDaysLeft != 0 || p.PlagueImmuneUntil != 0
        || Resources.Deposits.Any(r => p.Reserves[(int)r] != p.DepositSizes[(int)r] * GameRules.DepositSizeMultiplier);

    /// <summary>
    /// Carries on a saved game on <paramref name="map"/>, freshly generated from the save's settings.
    /// Throws <see cref="InvalidDataException"/> if the map is not the one the game was played on.
    /// </summary>
    public static GameSession Load(WorldMap map, SaveGame save)
    {
        if (Fingerprint(map) != save.MapFingerprint && Fingerprint(map, DrawnRiverFlows(map)) != save.MapFingerprint)
            throw new InvalidDataException($"Esta versión del juego genera el mapa de otra forma; la partida ({save.GameVersion}) no se puede cargar.");
        // Older saves had five HQ levels: brigades and divisions become corps, and the rest move down two.
        int Level(int old) => save.ThreeCommandLevels || old <= 0 ? old : Math.Max(1, old - 2);

        // Random choices after loading follow from the seed and the date rather than repeat the original game's.
        int seed = unchecked(map.Seed * 31 + (int)save.Hours);
        ResetProvinces(map);
        // Saves from before officers have none with an id, so numbering starts afresh.
        var session = new GameSession(map, seed) { _computerRivals = save.ComputerRivals, Date = new GameDate(save.Hours), _nextOfficerId = save.NextOfficerId ?? 0 };
        foreach (var b in save.InstitutionBirths ?? []) session._institutionBirths[b.Institution] = (b.ProvinceId, b.Hours);
        session.LoadRoads(save);

        foreach (var ps in save.Provinces)
        {
            var p = map.Provinces[ps.Id];
            p.OwnerId = ps.OwnerId;
            p.ControllerId = ps.ControllerId;
            p.Population = ps.Population;
            p.CityId = ps.CityId;
            p.Mood = ps.Mood;
            p.Fertility = ps.Fertility;
            ps.Reserves.CopyTo(p.Reserves, 0);
            // A deposit the old generator did not place has nothing saved; it starts full, like the rest did.
            if (!save.ExtraDeposits)
                foreach (var r in Resources.Deposits)
                    if (p.Reserves[(int)r] == 0) p.Reserves[(int)r] = p.DepositSizes[(int)r] * GameRules.DepositSizeMultiplier;
            foreach (var b in ps.Buildings.Where(Buildings.Buildings.All.Contains)) p.AddBuilding(b);
            p.Constructing = ps.Constructing is BuildingType c && Buildings.Buildings.All.Contains(c) ? c : null;
            p.ConstructionDaysLeft = ps.ConstructionDaysLeft;
            p.PlannedCityName = ps.PlannedCityName;
            p.Institutions.UnionWith(ps.Institutions ?? []);
            p.Name = ps.Name ?? "";
            if (p.Name.Length > 0) session._usedProvinceNames.Add(p.Name);
            foreach (var o in ps.Training ?? []) p.Training.Add(FromSave(o, Level));
            // Before 1.61.0 there were no cultures: the people share their ruler's.
            p.CultureId = ps.CultureId ?? (p.Population >= 1 ? p.OwnerId : -1);
            p.Assimilation = ps.Assimilation;
            p.RevoltProgress = ps.RevoltProgress;
            p.ReligionId = ps.ReligionId ?? -1;
            p.Conversion = ps.Conversion;
            p.PlagueDaysLeft = ps.PlagueDaysLeft;
            p.PlagueImmuneUntil = ps.PlagueImmuneUntil;
        }

        foreach (var s in save.Players)
        {
            // Before 1.77.0 there were no faiths: each nation gets one by its number, and its people share it.
            var player = new Player(s.Id, s.Name, s.Color, s.IsHuman) { CapitalCityId = s.CapitalCityId, Eliminated = s.Eliminated, ReligionId = s.ReligionId ?? s.Id % Religions.Count };
            foreach (var r in Resources.All) player.Stockpile[r] = s.Stockpile[(int)r];
            s.LastDayNet.CopyTo(player.LastDayNet, 0);
            player.IsStarving = s.IsStarving;
            player.FoodReserveDays = s.FoodReserveDays;
            foreach (var tech in s.Techs) player.Learn(tech);
            // Saves from before the branches have no priorities and leave the default ones.
            s.ResearchPriorities?.CopyTo(player.ResearchPriorities, 0);
            foreach (var tech in s.CurrentResearch ?? []) player.Researching[(int)tech.Info().Branch] = tech;
            foreach (var institution in s.Institutions ?? []) player.Adopt(institution);
            s.ResearchProgress.CopyTo(player.ResearchProgress, 0);
            player.SpareScience = s.SpareScience;
            player.LastDayScience = s.LastDayScience;
            foreach (var t in s.Templates) player.Templates.Add(new RegimentTemplate(t.Id, t.Number, t.Battalions));
            foreach (var o in s.OfficerReserve ?? []) player.OfficerReserve.Add(FromSave(o));
            player.Provinces.UnionWith(save.Provinces.Where(p => p.OwnerId == s.Id).Select(p => p.Id));
            // Before 1.75.0 there was no reserve of recruits: it starts full.
            player.Manpower = s.Manpower ?? session.ManpowerCapacity(player);
            // Before 1.92.0 nothing was explored: the nation starts knowing what it sees.
            player.Explored.UnionWith(s.Explored ?? []);
            session.Players.Add(player);
        }

        foreach (var ps in save.Provinces.Where(ps => ps.ReligionId is null && ps.Population >= 1 && ps.OwnerId >= 0))
            map.Provinces[ps.Id].ReligionId = session.Players[ps.OwnerId].ReligionId;

        foreach (var c in save.Cities)
        {
            var city = new City(c.Id, c.Name, c.OwnerId, c.ProvinceId, c.FoundedHours) { FestivalUntilHours = c.FestivalUntilHours };
            // Before 1.44.1 the city held what was training; now its province does.
            foreach (var o in c.Training ?? [])
                map.Provinces[c.ProvinceId].Training.Add(FromSave(o, Level));
            session.Cities.Add(city);
            session._usedCityNames.Add(c.Name);
            // Before barracks every city trained troops: in an older save each keeps doing so with one.
            if (!save.Barracks) map.Provinces[c.ProvinceId].AddBuilding(BuildingType.Barracks);
        }
        // Before workshops the barracks also built the war machines: in an older save each that could keeps doing so with
        // a workshop (a factory, for a nation already industrialised).
        if (!save.Workshops)
            foreach (var p in map.Provinces.Where(p => p.OwnerId >= 0 && p.Buildings.Contains(BuildingType.Barracks)))
            {
                var owner = session.Players[p.OwnerId];
                if (BuildingType.Workshop.Info().RequiresTech is { } tech && owner.Techs.Contains(tech)) p.AddBuilding(BuildingType.Workshop.For(owner));
            }

        foreach (var u in save.Units)
        {
            var unit = new Unit(u.Id, session.Players[u.OwnerId], u.Type, u.ProvinceId, u.Citizens, u.Number, Level(u.HeadquartersLevel))
            {
                CommanderId = u.CommanderId,
                AttackingProvinceId = u.AttackingProvinceId,
                CarrierId = u.CarrierId,
                HoursToNext = u.HoursToNext,
                StepHours = u.StepHours,
            };
            foreach (var b in u.Battalions) unit.Battalions.Add(new Battalion(b.Type) { Strength = b.Strength, Organisation = b.Organisation, Experience = b.Experience });
            unit.CustomName = u.CustomName;
            unit.AutoClaim = u.AutoClaim;
            // Generals from before officers become officers of their HQ's rank, and HQs from before generals get one now.
            if (u.Officer is { } o) unit.Officer = FromSave(o);
            else if (u.General is { } g) unit.Officer = new Officer(session._nextOfficerId++, g.Name, [g.Trait], g.StartingSkill, g.Victories, unit.RequiredRank);
            else if (unit.IsHeadquarters) unit.Officer = Officer.Recruit(session._nextOfficerId++, session._random, unit.RequiredRank);
            unit.Path.AddRange(u.Path);
            session.Units.Add(unit);
            session._unitsById[unit.Id] = unit;
        }

        foreach (var m in save.Migrations)
            session.Migrations.Add(new Migration(m.Id, m.OwnerId, m.From, m.To, m.People, m.DepartHours, m.ArriveHours, m.Forced, m.Mood));
        foreach (var b in save.Battles)
        {
            var battle = new Battle(b.ProvinceId, b.AttackerId, b.DefenderId, b.StartHours)
            {
                AttackerLosses = b.AttackerLosses,
                DefenderLosses = b.DefenderLosses,
            };
            battle.Attackers.AddRange(b.Attackers);
            session._battles.Add(battle);
        }
        foreach (var w in save.Wars)
        {
            var war = session._wars[(w.A, w.B)] = new War(w.StartHours);
            war.Victories[w.A] = w.VictoriesA;
            war.Victories[w.B] = w.VictoriesB;
        }
        foreach (var t in save.Truces ?? []) session._truces[(t.A, t.B)] = t.UntilHours;
        foreach (var x in save.Sieges ?? []) session._sieges[x.ProvinceId] = new Siege(x.ProvinceId, x.AttackerId, x.Progress);
        foreach (var x in save.Alliances ?? []) session._alliances.Add((x.A, x.B));
        foreach (var x in save.Memories ?? []) session.Remember(x.From, x.To, x.Reason, x.Value);
        foreach (var x in save.Vassals ?? []) session._vassals[x.Vassal] = (x.Overlord, x.SinceHours);
        session._reparations.AddRange(save.Reparations ?? []);
        foreach (var x in save.Pacts ?? []) session._pacts.Add((x.A, x.B));
        foreach (var x in save.Access ?? []) session._access.Add((x.Granter, x.Grantee));
        session._trades.AddRange(save.Trades ?? []);
        session._nextTradeId = save.NextTradeId;
        session.LoadDecisions(save);
        session._history.AddRange(save.History ?? []);
        session.LoadObjectives(save);
        session.Outcome = save.Outcome;
        session.Notifications.AddRange(save.Notifications);
        foreach (var (province, carry) in save.EmigrationCarry) session._emigrationCarry[province] = carry;
        foreach (var n in save.UnitNumbers)
        {
            var key = (n.PlayerId, n.Level > 0 ? Level(n.Level) : n.Level);
            session._unitNumbers[key] = Math.Max(session._unitNumbers.GetValueOrDefault(key), n.Number);
        }
        // A chain of command that no longer fits the levels (after moving old HQs) comes apart.
        foreach (var unit in session.Units.Where(u => u.CommanderId is int c && (session.UnitById(c) is not { } hq || hq.HeadquartersLevel != u.CommandLevel + 1)))
            unit.CommanderId = null;
        session._nextUnitId = save.NextUnitId;
        session._nextCityId = save.NextCityId;
        session._nextMigrationId = save.NextMigrationId;
        session._nextTemplateId = save.NextTemplateId;

        foreach (var a in save.Ais)
        {
            var ai = new AiPlayer(session, session.Players[a.PlayerId], seed + 100 + a.PlayerId);
            ai.Restore(a);
            session._ais.Add(ai);
        }
        // Saves from before provinces were named on claiming: those with an owner get their name now.
        foreach (var p in map.Provinces.Where(p => p.IsOwned && p.Name.Length == 0)) session.NameProvince(p);
        if (!save.RealisticPopulation)
            foreach (var p in map.Provinces.Where(p => p.Population > 0)) p.Population = Math.Min(p.Population, session.CapacityOf(p));
        return session;
    }

    /// <summary>
    /// A number that changes if the generator makes a different map from the same settings. With
    /// <paramref name="riverFlows"/>, each province's river is taken from there instead of from the province.
    /// </summary>
    public static long Fingerprint(WorldMap map, float[]? riverFlows = null)
    {
        // FNV-1a over what shapes the game: each province's biome, size, place and neighbours.
        ulong hash = 14695981039346656037;
        void Add(long value)
        {
            hash ^= (ulong)value;
            hash *= 1099511628211;
        }
        Add(map.Width);
        Add(map.Height);
        Add(map.Provinces.Count);
        foreach (var p in map.Provinces)
        {
            Add((int)p.Biome);
            Add(p.PixelCount);
            Add(p.CenterX);
            Add(p.CenterY);
            Add(p.Neighbors.Length);
            Add(BitConverter.SingleToInt32Bits(riverFlows?[p.Id] ?? p.RiverFlow));
        }
        return (long)hash;
    }

    /// <summary>
    /// Each province's river as 1.31.0 and 1.32.0 worked it out, from the rivers as drawn. Their saves were
    /// fingerprinted with it, so loading one checks against it too; the game itself uses the rivers of 1.30.1.
    /// </summary>
    private static float[] DrawnRiverFlows(WorldMap map)
    {
        var flows = new float[map.Provinces.Count];
        foreach (var r in map.Rivers)
        {
            var p = map.Provinces[map.ProvinceIds[(int)r.Y1 * map.Width + (int)r.X1]];
            if (!p.IsWater) flows[p.Id] = Math.Max(flows[p.Id], r.Flow);
        }
        return flows;
    }
}
