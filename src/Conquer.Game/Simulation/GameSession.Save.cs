using Conquer.Game.AI;
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
        ThreeCommandLevels = true,
        Hours = Date.Hours,
        ComputerRivals = _computerRivals,
        Players = Players.Select(p => new PlayerSave(
            p.Id, p.Name, p.Color, p.IsHuman, [.. Resources.All.Select(r => p.Stockpile[r])], p.CapitalCityId,
            [.. p.LastDayNet], p.IsStarving, p.FoodReserveDays, [.. p.Techs.Order()],
            [.. p.ResearchProgress], p.SpareScience, p.LastDayScience,
            [.. p.Templates.Select(t => new TemplateSave(t.Id, t.Number, [.. t.Battalions]))], [.. p.ResearchPriorities],
            [.. p.Researching.OfType<Tech>()], [.. p.Institutions.Order()])).ToList(),
        // Provinces nobody has touched keep their generated state, so only the rest are stored.
        Provinces = Map.Provinces.Where(Changed).Select(p => new ProvinceSave(
            p.Id, p.OwnerId, p.ControllerId, p.Population, p.CityId, p.Mood, p.Fertility, [.. p.Reserves],
            [.. p.Buildings.Order()], p.Constructing, p.ConstructionDaysLeft, p.PlannedCityName, [.. p.Institutions.Order()], p.Name)).ToList(),
        Cities = Cities.Select(c => new CitySave(c.Id, c.Name, c.OwnerId, c.ProvinceId, c.FoundedHours, c.FestivalUntilHours,
            [.. c.Training.Select(o => new TrainingSave(o.Battalion, o.TemplateName, [.. o.TemplateBattalions], o.HeadquartersLevel, o.DaysLeft, o.TotalDays))])).ToList(),
        Units = Units.Select(u => new UnitSave(
            u.Id, u.OwnerId, u.Type, u.ProvinceId, u.Type is UnitType.Regiment or UnitType.Fleet ? 0 : u.Citizens, u.Number, u.HeadquartersLevel,
            [.. u.Battalions.Select(b => new BattalionSave(b.Type, b.Strength, b.Organisation, b.Experience))],
            u.CommanderId, u.AttackingProvinceId, [.. u.Path], u.HoursToNext, u.StepHours, u.CarrierId,
            u.General is { } g ? new GeneralSave(g.Name, g.Trait, g.StartingSkill, g.Victories) : null)).ToList(),
        Migrations = Migrations.Select(m => new MigrationSave(m.Id, m.OwnerId, m.FromProvinceId, m.ToProvinceId, m.People,
            m.DepartHours, m.ArriveHours, m.Forced, m.Mood)).ToList(),
        Battles = _battles.Select(b => new BattleSave(b.ProvinceId, b.AttackerId, b.DefenderId, b.StartHours, [.. b.Attackers])).ToList(),
        Wars = _wars.Select(w => new WarSave(w.Key.Item1, w.Key.Item2, w.Value)).ToList(),
        Notifications = [.. Notifications],
        Ais = _ais.Select(ai => ai.ToSave()).ToList(),
        EmigrationCarry = new Dictionary<int, double>(_emigrationCarry),
        UnitNumbers = _unitNumbers.Select(n => new UnitNumberSave(n.Key.Player, n.Key.Level, n.Value)).ToList(),
        NextUnitId = _nextUnitId,
        NextCityId = _nextCityId,
        NextMigrationId = _nextMigrationId,
        NextTemplateId = _nextTemplateId,
        InstitutionBirths = _institutionBirths.OrderBy(b => b.Key).Select(b => new InstitutionBirthSave(b.Key, b.Value.ProvinceId, b.Value.Hours)).ToList(),
    };

    /// <summary>Whether the province differs from how <see cref="ResetProvinces"/> leaves it.</summary>
    private static bool Changed(Province p) =>
        p.OwnerId != -1 || p.ControllerId != -1 || p.Population != 0 || p.CityId.HasValue
        || p.Mood != GameRules.StartingMood || p.Fertility != 1 || p.Buildings.Count > 0 || p.Constructing.HasValue || p.PlannedCityName != null
        || p.Institutions.Count > 0 || p.Name.Length > 0
        || Resources.Deposits.Any(r => p.Reserves[(int)r] != p.DepositSizes[(int)r] * GameRules.DepositSizeMultiplier);

    /// <summary>
    /// Carries on a saved game on <paramref name="map"/>, freshly generated from the save's settings.
    /// Throws <see cref="InvalidDataException"/> if the map is not the one the game was played on.
    /// </summary>
    public static GameSession Load(WorldMap map, SaveGame save)
    {
        if (Fingerprint(map) != save.MapFingerprint)
            throw new InvalidDataException($"Esta versión del juego genera el mapa de otra forma; la partida ({save.GameVersion}) no se puede cargar.");
        // Older saves had five HQ levels: brigades and divisions become corps, and the rest move down two.
        int Level(int old) => save.ThreeCommandLevels || old <= 0 ? old : Math.Max(1, old - 2);

        // Random choices after loading follow from the seed and the date rather than repeat the original game's.
        int seed = unchecked(map.Seed * 31 + (int)save.Hours);
        ResetProvinces(map);
        var session = new GameSession(map, seed) { _computerRivals = save.ComputerRivals, Date = new GameDate(save.Hours) };
        foreach (var b in save.InstitutionBirths ?? []) session._institutionBirths[b.Institution] = (b.ProvinceId, b.Hours);

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
            foreach (var b in ps.Buildings) p.AddBuilding(b);
            p.Constructing = ps.Constructing;
            p.ConstructionDaysLeft = ps.ConstructionDaysLeft;
            p.PlannedCityName = ps.PlannedCityName;
            p.Institutions.UnionWith(ps.Institutions ?? []);
            p.Name = ps.Name ?? "";
            if (p.Name.Length > 0) session._usedProvinceNames.Add(p.Name);
        }

        foreach (var s in save.Players)
        {
            var player = new Player(s.Id, s.Name, s.Color, s.IsHuman) { CapitalCityId = s.CapitalCityId };
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
            player.Provinces.UnionWith(save.Provinces.Where(p => p.OwnerId == s.Id).Select(p => p.Id));
            session.Players.Add(player);
        }

        foreach (var c in save.Cities)
        {
            var city = new City(c.Id, c.Name, c.OwnerId, c.ProvinceId, c.FoundedHours) { FestivalUntilHours = c.FestivalUntilHours };
            foreach (var o in c.Training)
                city.Training.Add(new TrainingOrder(o.Battalion, o.TemplateName, o.TemplateBattalions, Level(o.HeadquartersLevel), o.DaysLeft, o.TotalDays));
            session.Cities.Add(city);
            session._usedCityNames.Add(c.Name);
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
            // HQs from before generals get one now.
            if (unit.IsHeadquarters)
                unit.General = u.General is { } g ? new General(g.Name, g.Trait, g.StartingSkill, g.Victories) : General.Appoint(session._random);
            unit.Path.AddRange(u.Path);
            session.Units.Add(unit);
            session._unitsById[unit.Id] = unit;
        }

        foreach (var m in save.Migrations)
            session.Migrations.Add(new Migration(m.Id, m.OwnerId, m.From, m.To, m.People, m.DepartHours, m.ArriveHours, m.Forced, m.Mood));
        foreach (var b in save.Battles)
        {
            var battle = new Battle(b.ProvinceId, b.AttackerId, b.DefenderId, b.StartHours);
            battle.Attackers.AddRange(b.Attackers);
            session._battles.Add(battle);
        }
        foreach (var w in save.Wars) session._wars[(w.A, w.B)] = w.StartHours;
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
        return session;
    }

    /// <summary>A number that changes if the generator makes a different map from the same settings.</summary>
    public static long Fingerprint(WorldMap map)
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
            Add(BitConverter.SingleToInt32Bits(p.RiverFlow));
        }
        return (long)hash;
    }
}
