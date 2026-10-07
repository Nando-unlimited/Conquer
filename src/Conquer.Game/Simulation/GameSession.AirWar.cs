using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// The war in the air: each air unit flies its mission over the provinces near its target (<see cref="MilitaryRules.MissionRadiusKm"/>).
/// Fighters fight for the sky; the side whose fighters rule it fights better on the ground, and the enemy's troops there
/// march slower and get less supply. Dive and tactical bombers add their fire to the battles, tactical and strategic
/// bombers damage the enemy's buildings, naval aircraft strike its fleets and convoys, all doing less under a sky the
/// enemy rules. Once a day the units that meet, and the anti-air beneath them, shoot each other's planes down.
/// Transports drop paratroopers.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The missions each kind of aircraft can fly, besides none.</summary>
    public static AirMission[] MissionsFor(BattalionType type) => type switch
    {
        BattalionType.Fighters => [AirMission.AirSuperiority],
        BattalionType.CloseSupport => [AirMission.CloseSupport],
        BattalionType.TacticalBombers => [AirMission.CloseSupport, AirMission.StrategicBombing],
        BattalionType.Bombers => [AirMission.StrategicBombing],
        BattalionType.NavalBombers => [AirMission.NavalStrike],
        BattalionType.AirTransports => [AirMission.Paradrop],
        _ => [],
    };

    public static string AirMissionName(AirMission mission) => mission switch
    {
        AirMission.AirSuperiority => "Superioridad aérea",
        AirMission.CloseSupport => "Apoyo cercano",
        AirMission.StrategicBombing => "Bombardeo",
        AirMission.NavalStrike => "Ataque naval",
        AirMission.Paradrop => "Lanzar paracaidistas",
        _ => "Sin misión",
    };

    public static string AirMissionDescription(AirMission mission) => mission switch
    {
        AirMission.AirSuperiority => "Disputa el cielo de la zona a los aviones enemigos: quien lo domina lucha mejor en tierra, y el enemigo marcha más despacio y recibe menos suministro.",
        AirMission.CloseSupport => "Suma su fuego a tus batallas de la zona.",
        AirMission.StrategicBombing => "Bombardea la provincia enemiga: quita moral y daña sus edificios, que producen menos y, muy dañados, se derrumban.",
        AirMission.NavalStrike => "Ataca las flotas enemigas y los convoyes que crucen los mares de la zona.",
        AirMission.Paradrop => "Lleva a los paracaidistas de su aeródromo adonde los lances (botón en su panel).",
        _ => "Se queda en su base.",
    };

    /// <summary>Gives an air unit a mission over a province within its range, or none.</summary>
    public CommandResult SetAirMission(int playerId, int unitId, AirMission mission, int? targetProvinceId = null)
    {
        if (AirUnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad aérea no válida.");
        _flyingAt = -1;
        if (mission == AirMission.None)
        {
            unit.Mission = AirMission.None;
            unit.TargetProvinceId = null;
            return CommandResult.Success($"{unit.Name} se queda en su base.");
        }
        if (!MissionsFor(unit.Type).Contains(mission))
            return CommandResult.Fail($"Los {unit.Type.Line().Name.ToLowerInvariant()} no pueden: {AirMissionName(mission).ToLowerInvariant()}.");
        if (targetProvinceId is not int target) return CommandResult.Fail("Elige la provincia de la misión.");
        if (!InRange(unit, Map.Provinces[target])) return CommandResult.Fail($"Está fuera de su alcance ({unit.Flights.Min(f => f.Info.RangeKm):N0} km).");
        unit.Mission = mission;
        unit.TargetProvinceId = target;
        return CommandResult.Success($"{unit.Name}: {AirMissionName(mission).ToLowerInvariant()} sobre {PlaceName(Map.Provinces[target])}.");
    }

    /// <summary>Whether the unit flies its mission: it has one in range and enough planes and organisation.</summary>
    public bool IsFlying(AirUnit unit) =>
        unit.Mission is not (AirMission.None or AirMission.Paradrop) && unit.TargetProvinceId is int t && InRange(unit, Map.Provinces[t])
        && unit.OrganisationShare >= MilitaryRules.MinFlyingOrganisation && unit.PlaneCount >= 1;

    private long _flyingAt = -1;
    private List<AirUnit> _flying = [];

    /// <summary>The air units flying their missions this hour (worked out once an hour, or again when a mission changes).</summary>
    private List<AirUnit> Flying()
    {
        if (_flyingAt != Date.Hours)
        {
            _flyingAt = Date.Hours;
            _flying = [.. _airUnits.Where(IsFlying)];
        }
        return _flying;
    }

    /// <summary>Whether the unit's mission reaches the province.</summary>
    public bool Covers(AirUnit unit, Province p) =>
        unit.TargetProvinceId is int t && Map.DistanceKm(Map.Provinces[t], p) <= MilitaryRules.MissionRadiusKm;

    /// <summary>
    /// An air unit's fire of one kind: its escuadrillas', each by its planes, organisation and crews' experience, and its
    /// chain of command.
    /// </summary>
    public double AirFire(AirUnit unit, Func<BattalionInfo, double> stat) =>
        unit.Flights.Sum(f => stat(f.Info) * f.StrengthShare * (0.5 + 0.5 * f.OrganisationShare) * (1 + MilitaryRules.ExperienceBonus * f.Experience))
        * (1 + AirCommandBonus(unit));

    /// <summary>The fighters' strength of a nation, and of its enemies, over a province.</summary>
    public (double Mine, double Enemies) FighterCover(int playerId, Province p)
    {
        double mine = 0, enemies = 0;
        foreach (var u in Flying())
        {
            if (u.Mission != AirMission.AirSuperiority || !Covers(u, p)) continue;
            double power = AirFire(u, i => i.AirAttack);
            if (u.OwnerId == playerId) mine += power;
            else if (AtWar(u.OwnerId, playerId)) enemies += power;
        }
        return (mine, enemies);
    }

    /// <summary>A nation's share of the fighters over a province against its enemies'; null when neither has any there.</summary>
    public double? AirSuperiority(int playerId, Province p)
    {
        var (mine, enemies) = FighterCover(playerId, p);
        return mine + enemies <= 0 ? null : mine / (mine + enemies);
    }

    /// <summary>Whether the nation's enemies rule the sky over the province.</summary>
    public bool EnemyRulesTheAir(int playerId, Province p) => AirSuperiority(playerId, p) is double s && 1 - s >= MilitaryRules.AirRuleShare;

    /// <summary>The fire a side's ground troops get from the sky over a province: up to the bonus with it theirs, as much less with it the enemy's.</summary>
    public double AirSuperiorityMultiplier(int playerId, Province p) =>
        AirSuperiority(playerId, p) is double s ? 1 + MilitaryRules.AirSuperiorityBonus * (2 * s - 1) : 1;

    /// <summary>The fire a nation's bombers in close support add to its battles in a province, each hour.</summary>
    public double CloseAirSupport(int playerId, Province p)
    {
        double fire = Flying().Where(u => u.OwnerId == playerId && u.Mission == AirMission.CloseSupport && Covers(u, p)).Sum(u => AirFire(u, i => i.Attack));
        return fire * (EnemyRulesTheAir(playerId, p) ? MilitaryRules.UnescortedBomberEffect : 1);
    }

    /// <summary>The anti-air guarding a province against a nation's aircraft: its enemies' anti-air battalions there and next to it.</summary>
    private double AntiAirFire(int playerId, Province p) =>
        Units.Where(u => u.IsMilitary && AtWar(u.OwnerId, playerId) && (u.ProvinceId == p.Id || p.Neighbors.Contains(u.ProvinceId)))
            .SelectMany(u => u.Battalions).Where(b => b.Type == BattalionType.AntiAir)
            .Sum(b => b.Info.Attack * b.StrengthShare * MilitaryRules.AntiAirAgainstAircraft);

    /// <summary>Planes the nation lost in the air the last day, and those it shot down, for the alerts.</summary>
    public double PlanesLostLastDay(int playerId) => _airLosses.GetValueOrDefault(playerId).Lost;
    public double PlanesDownedLastDay(int playerId) => _airLosses.GetValueOrDefault(playerId).Downed;
    private readonly Dictionary<int, (double Lost, double Downed)> _airLosses = [];

    /// <summary>
    /// Once a day: the flying units of nations at war whose areas meet shoot at each other, and the anti-air under the
    /// bombers and naval aircraft shoots at them; each loses a share of its planes and organisation (see
    /// <see cref="MilitaryRules.AirEvasion"/>). Then the bombers bomb and the naval aircraft strike, and the buildings
    /// not bombed are repaired a little.
    /// </summary>
    internal void DailyAir()
    {
        _airLosses.Clear();
        var bombed = new HashSet<int>();
        var flying = _airUnits.Where(IsFlying).ToList();

        var incoming = flying.ToDictionary(u => u, _ => 0.0);
        var shooters = flying.ToDictionary(u => u, _ => new List<AirUnit>());
        foreach (var a in flying)
        {
            var targets = flying.Where(b => AtWar(a.OwnerId, b.OwnerId)
                && Map.DistanceKm(Map.Provinces[a.TargetProvinceId!.Value], Map.Provinces[b.TargetProvinceId!.Value]) <= 2 * MilitaryRules.MissionRadiusKm).ToList();
            if (targets.Count == 0) continue;
            // Fighters fight for the sky; the others only defend themselves.
            double fire = AirFire(a, i => i.AirAttack) * (a.Mission == AirMission.AirSuperiority ? 1 : 0.5);
            foreach (var b in targets)
            {
                incoming[b] += fire / targets.Count;
                shooters[b].Add(a);
            }
        }
        foreach (var u in flying.Where(u => u.Mission is AirMission.CloseSupport or AirMission.StrategicBombing or AirMission.NavalStrike))
            incoming[u] += AntiAirFire(u.OwnerId, Map.Provinces[u.TargetProvinceId!.Value]);

        foreach (var u in flying)
        {
            double fire = incoming[u];
            double defence = u.Flights.Sum(f => f.Info.Defense * f.StrengthShare);
            double loss = fire <= 0 ? 0 : Math.Min(MilitaryRules.MaxDailyAirLoss, fire / (fire + MilitaryRules.AirDefenseWeight * defence + MilitaryRules.AirEvasion));
            double planes = u.PlaneCount * loss;
            foreach (var f in u.Flights)
            {
                f.Strength *= 1 - loss;
                f.Organisation = Math.Max(0, f.Organisation - f.Info.MaxOrganisation * loss * 2);
                f.Experience += MilitaryRules.ExperiencePerBattleHour * 4 * (1 - f.Experience);
            }
            if (planes <= 0) continue;
            var (lost, downed) = _airLosses.GetValueOrDefault(u.OwnerId);
            _airLosses[u.OwnerId] = (lost + planes, downed);
            var owners = shooters[u].Select(s => s.OwnerId).Distinct().ToList();
            foreach (int s in owners)
            {
                var (l, d) = _airLosses.GetValueOrDefault(s);
                _airLosses[s] = (l, d + planes / owners.Count);
            }
        }

        foreach (var u in flying.Where(u => u.Mission == AirMission.StrategicBombing)) Bomb(u, bombed);
        foreach (var u in flying.Where(u => u.Mission == AirMission.NavalStrike)) StrikeAtSea(u);
        RepairBuildings(bombed);

        if (PlanesLostLastDay(HumanPlayerId) >= 0.5)
            Notify(HumanPlayerId, $"Hemos perdido {PlanesLostLastDay(HumanPlayerId):0} aviones; derribado {PlanesDownedLastDay(HumanPlayerId):0} enemigos.");
    }

    /// <summary>
    /// A bomber unit's day over its target, if the enemy holds it: the people lose heart and its buildings take damage,
    /// a random one at a time, so they produce less; one damaged right through falls. Less under a sky the enemy rules.
    /// </summary>
    private void Bomb(AirUnit unit, HashSet<int> bombed)
    {
        var p = Map.Provinces[unit.TargetProvinceId!.Value];
        if (!p.IsOwned || !AtWar(unit.OwnerId, p.OwnerId)) return;
        double harm = AirFire(unit, i => i.Attack) * (EnemyRulesTheAir(unit.OwnerId, p) ? MilitaryRules.UnescortedBomberEffect : 1);
        p.Mood = Math.Max(0, p.Mood - Math.Min(5, harm / 10));
        bombed.Add(p.Id);
        if (p.Buildings.Count == 0) return;
        var building = p.Buildings.ElementAt(_random.Next(p.Buildings.Count));
        double damage = p.DamageOf(building) + harm * MilitaryRules.BuildingDamagePerHarm;
        if (damage < 1)
        {
            p.SetDamage(building, damage);
            return;
        }
        p.RemoveBuilding(building);
        Notify(p.OwnerId, $"Los bombarderos de {unit.Owner.Name} han derrumbado {building.Info().WithArticle} de {PlaceName(p)}.");
        if (unit.OwnerId == HumanPlayerId) Notify(HumanPlayerId, $"{unit.Name} ha derrumbado {building.Info().WithArticle} en {PlaceName(p)}.");
    }

    /// <summary>Damaged buildings not bombed today are repaired a little.</summary>
    private void RepairBuildings(HashSet<int> bombed)
    {
        foreach (var p in _damagedProvinces.Select(id => Map.Provinces[id]).ToList())
        {
            if (bombed.Contains(p.Id)) continue;
            foreach (var b in p.Buildings.Where(b => p.DamageOf(b) > 0).ToList())
                p.SetDamage(b, Math.Max(0, p.DamageOf(b) - MilitaryRules.BuildingRepairPerDay));
        }
        _damagedProvinces.RemoveWhere(id => !Map.Provinces[id].Buildings.Any(b => Map.Provinces[id].DamageOf(b) > 0));
        foreach (int id in bombed) _damagedProvinces.Add(id);
    }

    /// <summary>Provinces with damaged buildings, so the daily repairs need not look at every province.</summary>
    private readonly HashSet<int> _damagedProvinces = [];

    /// <summary>A naval unit's day: it strikes the enemy fleets in the seas of its area and the convoys crossing them.</summary>
    private void StrikeAtSea(AirUnit unit)
    {
        double fire = AirFire(unit, i => i.Attack) * MilitaryRules.NavalStrikeHours;
        if (EnemyRulesTheAir(unit.OwnerId, Map.Provinces[unit.TargetProvinceId!.Value])) fire *= MilitaryRules.UnescortedBomberEffect;
        var fleets = Units.Where(u => u.IsFleet && AtWar(u.OwnerId, unit.OwnerId) && Covers(unit, Map.Provinces[u.ProvinceId])).ToList();
        if (fleets.Count > 0) Damage(fleets, fire);
        foreach (var fleet in fleets.Where(f => f.Citizens < 1).ToList()) Sink(fleet);
        foreach (var s in _shipments.Where(s => s.Convoys > 0 && AtWar(s.OwnerId, unit.OwnerId) && s.SeaRoute.Any(id => Covers(unit, Map.Provinces[id]))).ToList())
            SinkConvoys(s, Math.Min(MilitaryRules.MaxDailyConvoyLoss, fire / (fire + MilitaryRules.ConvoyEvasion)), [unit.OwnerId]);
    }

    /// <summary>
    /// Whether the paratroopers can be dropped there: a unit of paratroopers only, at an airfield of the nation's with
    /// transports that can carry its men and reach the target, a province on land with no enemy troops.
    /// </summary>
    public CommandResult CanParadrop(Unit unit, Province target)
    {
        if (!unit.IsMilitary || unit.Battalions.Any(b => b.Type != BattalionType.Paratroopers)) return CommandResult.Fail("Solo se lanzan unidades de paracaidistas.");
        var transports = Transports(unit).ToList();
        if (transports.Count == 0) return CommandResult.Fail("Hacen falta aviones de transporte con organización en su aeródromo.");
        double room = transports.Sum(u => u.Flights.Sum(f => f.Info.Capacity * f.StrengthShare));
        if (room < unit.Citizens) return CommandResult.Fail($"Los transportes solo llevan {room:N0} hombres.");
        if (target.IsWater) return CommandResult.Fail("Solo se lanzan sobre tierra.");
        if (transports.Any(u => !InRange(u, target))) return CommandResult.Fail($"Está fuera del alcance de los transportes ({transports.Min(u => u.Info.RangeKm):N0} km).");
        if (EnemyRegimentsIn(target.Id, unit.OwnerId).Any()) return CommandResult.Fail("Hay tropas enemigas: no se puede saltar sobre ellas.");
        if (target.IsOwned && target.ControllerId != unit.OwnerId && !AtWar(unit.OwnerId, target.ControllerId))
            return CommandResult.Fail("Solo sobre tierra propia, libre o de una nación en guerra contigo.");
        return CommandResult.Success();
    }

    /// <summary>The transport units at the unit's airfield ready to fly.</summary>
    private IEnumerable<AirUnit> Transports(Unit unit) =>
        _airUnits.Where(u => u.OwnerId == unit.OwnerId && u.Type == BattalionType.AirTransports && u.BaseProvinceId == unit.ProvinceId
                             && u.OrganisationShare >= MilitaryRules.MinFlyingOrganisation && u.PlaneCount >= 1);

    /// <summary>
    /// Flies the paratroopers to the target and drops them: they land with half their organisation (and lose men under a
    /// sky the enemy rules) and take the province if the enemy holds it; the transports come back with half theirs.
    /// </summary>
    public CommandResult Paradrop(int playerId, int unitId, int targetProvinceId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        var target = Map.Provinces[targetProvinceId];
        var check = CanParadrop(unit, target);
        if (!check.Ok) return check;
        foreach (var f in Transports(unit).SelectMany(u => u.Flights).ToList()) f.Organisation /= 2;
        bool contested = EnemyRulesTheAir(playerId, target);
        foreach (var b in unit.Battalions)
        {
            b.Organisation /= 2;
            if (contested) b.Strength *= 1 - MilitaryRules.UnderEnemyAirSupplyLoss;
        }
        CancelAttack(unit);
        unit.Path.Clear();
        unit.HoursToNext = unit.StepHours = 0;
        EnterProvince(unit, target.Id);
        return CommandResult.Success($"{unit.Name} salta sobre {PlaceName(target)}" + (contested ? ", bajo el fuego de la aviación enemiga." : "."));
    }
}
