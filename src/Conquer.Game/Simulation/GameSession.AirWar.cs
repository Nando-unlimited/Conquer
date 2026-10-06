using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// The war in the air: each wing flies its mission over the provinces near its target (<see cref="MilitaryRules.MissionRadiusKm"/>).
/// Fighters fight for the sky; the side whose fighters rule it fights better on the ground, and the enemy's troops there
/// march slower and get less supply. Attack aircraft add their fire to the battles, strategic bombers wreck the enemy's
/// provinces and naval aircraft strike its fleets and convoys, all doing less under a sky the enemy rules. Once a day the
/// wings that meet, and the anti-air beneath them, shoot each other's planes down. Transports drop paratroopers.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The missions each kind of aircraft can fly, besides none.</summary>
    public static AirMission[] MissionsFor(BattalionType type) => type switch
    {
        BattalionType.Fighters => [AirMission.AirSuperiority],
        BattalionType.CloseSupport => [AirMission.CloseSupport],
        BattalionType.Bombers => [AirMission.StrategicBombing],
        BattalionType.NavalBombers => [AirMission.NavalStrike],
        BattalionType.AirTransports => [AirMission.Paradrop],
        _ => [],
    };

    public static string AirMissionName(AirMission mission) => mission switch
    {
        AirMission.AirSuperiority => "Superioridad aérea",
        AirMission.CloseSupport => "Apoyo cercano",
        AirMission.StrategicBombing => "Bombardeo estratégico",
        AirMission.NavalStrike => "Ataque naval",
        AirMission.Paradrop => "Lanzar paracaidistas",
        _ => "Sin misión",
    };

    public static string AirMissionDescription(AirMission mission) => mission switch
    {
        AirMission.AirSuperiority => "Disputa el cielo de la zona a los aviones enemigos: quien lo domina lucha mejor en tierra, y el enemigo marcha más despacio y recibe menos suministro.",
        AirMission.CloseSupport => "Suma su fuego a tus batallas de la zona.",
        AirMission.StrategicBombing => "Bombardea las provincias enemigas de la zona: quita moral, frena sus talleres y derriba edificios.",
        AirMission.NavalStrike => "Ataca las flotas enemigas y los convoyes que crucen los mares de la zona.",
        AirMission.Paradrop => "Lleva a los paracaidistas de su aeródromo adonde los lances (botón en su panel).",
        _ => "Se queda en su base.",
    };

    /// <summary>Gives a wing a mission over a province within its range, or none.</summary>
    public CommandResult SetAirMission(int playerId, int wingId, AirMission mission, int? targetProvinceId = null)
    {
        if (WingById(wingId) is not { } wing || wing.OwnerId != playerId) return CommandResult.Fail("Ala no válida.");
        if (mission == AirMission.None)
        {
            wing.Mission = AirMission.None;
            _flyingAt = -1;
            wing.TargetProvinceId = null;
            return CommandResult.Success($"{wing.Name} se queda en su base.");
        }
        if (!MissionsFor(wing.Type).Contains(mission)) return CommandResult.Fail($"Un ala de {wing.Type.Line().Name.ToLowerInvariant()} no puede: {AirMissionName(mission).ToLowerInvariant()}.");
        if (targetProvinceId is not int target) return CommandResult.Fail("Elige la provincia de la misión.");
        if (!InRange(wing, Map.Provinces[target])) return CommandResult.Fail($"Está fuera de su alcance ({wing.Info.RangeKm:N0} km).");
        wing.Mission = mission;
        _flyingAt = -1;
        wing.TargetProvinceId = target;
        return CommandResult.Success($"{wing.Name}: {AirMissionName(mission).ToLowerInvariant()} sobre {PlaceName(Map.Provinces[target])}.");
    }

    /// <summary>Whether the wing flies its mission today: it has one in range and enough planes and organisation.</summary>
    public bool IsFlying(AirWing wing) =>
        wing.Mission != AirMission.None && wing.Mission != AirMission.Paradrop && wing.TargetProvinceId is int t && InRange(wing, Map.Provinces[t])
        && wing.Planes.OrganisationShare >= MilitaryRules.MinFlyingOrganisation && wing.PlaneCount >= 1;

    private long _flyingAt = -1;
    private List<AirWing> _flying = [];

    /// <summary>The wings flying their missions this hour (worked out once an hour, or again when a mission changes).</summary>
    private List<AirWing> Flying()
    {
        if (_flyingAt != Date.Hours)
        {
            _flyingAt = Date.Hours;
            _flying = [.. _wings.Where(IsFlying)];
        }
        return _flying;
    }

    /// <summary>Whether the wing's mission reaches the province.</summary>
    public bool Covers(AirWing wing, Province p) =>
        wing.TargetProvinceId is int t && Map.DistanceKm(Map.Provinces[t], p) <= MilitaryRules.MissionRadiusKm;

    /// <summary>How much a wing does: its planes, its organisation and its crews' experience.</summary>
    public static double Effectiveness(AirWing wing) =>
        wing.Planes.StrengthShare * (0.5 + 0.5 * wing.Planes.OrganisationShare) * (1 + MilitaryRules.ExperienceBonus * wing.Planes.Experience);

    /// <summary>The fighters' strength of a nation, and of its enemies, over a province.</summary>
    public (double Mine, double Enemies) FighterCover(int playerId, Province p)
    {
        double mine = 0, enemies = 0;
        foreach (var w in Flying())
        {
            if (w.Mission != AirMission.AirSuperiority || !Covers(w, p)) continue;
            double power = w.Info.AirAttack * Effectiveness(w);
            if (w.OwnerId == playerId) mine += power;
            else if (AtWar(w.OwnerId, playerId)) enemies += power;
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

    /// <summary>The fire a nation's attack aircraft add to its battles in a province, each hour.</summary>
    public double CloseAirSupport(int playerId, Province p)
    {
        double fire = Flying().Where(w => w.OwnerId == playerId && w.Mission == AirMission.CloseSupport && Covers(w, p))
            .Sum(w => w.Info.Attack * Effectiveness(w));
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

    /// <summary>Provinces bombed the day before and the share of their workshops' work lost to it.</summary>
    private readonly Dictionary<int, double> _bombed = [];

    /// <summary>The share of its work a province's workshop loses today to yesterday's bombing.</summary>
    public double BombingDamage(Province p) => _bombed.GetValueOrDefault(p.Id);

    /// <summary>
    /// Once a day: the flying wings of nations at war whose areas meet shoot at each other, and the anti-air under the
    /// attack aircraft, bombers and naval aircraft shoots at them; each wing loses planes and organisation (see
    /// <see cref="MilitaryRules.AirEvasion"/>). Then the bombers bomb and the naval aircraft strike.
    /// </summary>
    internal void DailyAir()
    {
        _airLosses.Clear();
        _bombed.Clear();
        var flying = _wings.Where(IsFlying).ToList();
        if (flying.Count == 0) return;

        var incoming = flying.ToDictionary(w => w, _ => 0.0);
        var shooters = flying.ToDictionary(w => w, _ => new List<AirWing>());
        foreach (var a in flying)
        {
            var targets = flying.Where(b => AtWar(a.OwnerId, b.OwnerId)
                && Map.DistanceKm(Map.Provinces[a.TargetProvinceId!.Value], Map.Provinces[b.TargetProvinceId!.Value]) <= 2 * MilitaryRules.MissionRadiusKm).ToList();
            if (targets.Count == 0) continue;
            // Fighters fight for the sky; the others only defend themselves.
            double fire = a.Info.AirAttack * Effectiveness(a) * (a.Mission == AirMission.AirSuperiority ? 1 : 0.5);
            foreach (var b in targets)
            {
                incoming[b] += fire / targets.Count;
                shooters[b].Add(a);
            }
        }
        foreach (var w in flying.Where(w => w.Mission is AirMission.CloseSupport or AirMission.StrategicBombing or AirMission.NavalStrike))
            incoming[w] += AntiAirFire(w.OwnerId, Map.Provinces[w.TargetProvinceId!.Value]);

        foreach (var w in flying)
        {
            double fire = incoming[w];
            double loss = fire <= 0 ? 0 : Math.Min(MilitaryRules.MaxDailyAirLoss,
                fire / (fire + MilitaryRules.AirDefenseWeight * w.Info.Defense * w.Planes.StrengthShare + MilitaryRules.AirEvasion));
            double planes = w.PlaneCount * loss;
            w.Planes.Strength *= 1 - loss;
            w.Planes.Organisation = Math.Max(0, w.Planes.Organisation - w.Info.MaxOrganisation * loss * 2);
            w.Planes.Experience += MilitaryRules.ExperiencePerBattleHour * 4 * (1 - w.Planes.Experience);
            if (planes <= 0) continue;
            var (lost, downed) = _airLosses.GetValueOrDefault(w.OwnerId);
            _airLosses[w.OwnerId] = (lost + planes, downed);
            foreach (var s in shooters[w].Select(s => s.OwnerId).Distinct())
            {
                var (l, d) = _airLosses.GetValueOrDefault(s);
                _airLosses[s] = (l, d + planes / shooters[w].Select(x => x.OwnerId).Distinct().Count());
            }
        }

        foreach (var w in flying.Where(w => w.Mission == AirMission.StrategicBombing)) Bomb(w);
        foreach (var w in flying.Where(w => w.Mission == AirMission.NavalStrike)) StrikeAtSea(w);

        if (PlanesLostLastDay(HumanPlayerId) >= 0.5)
            Notify(HumanPlayerId, $"Hemos perdido {PlanesLostLastDay(HumanPlayerId):0} aviones; derribado {PlanesDownedLastDay(HumanPlayerId):0} enemigos.");
    }

    /// <summary>
    /// A bomber wing's day over its target, if the enemy holds it: the people lose heart, the workshops lose work the next
    /// day and a building may come down; less under a sky the enemy rules.
    /// </summary>
    private void Bomb(AirWing wing)
    {
        var p = Map.Provinces[wing.TargetProvinceId!.Value];
        if (!p.IsOwned || !AtWar(wing.OwnerId, p.OwnerId)) return;
        double harm = wing.Info.Attack * Effectiveness(wing) * (EnemyRulesTheAir(wing.OwnerId, p) ? MilitaryRules.UnescortedBomberEffect : 1);
        p.Mood = Math.Max(0, p.Mood - Math.Min(5, harm / 10));
        _bombed[p.Id] = Math.Min(0.5, _bombed.GetValueOrDefault(p.Id) + harm / 200);
        if (p.Buildings.Count > 0 && _random.NextDouble() < harm / 500)
        {
            var building = p.Buildings.ElementAt(_random.Next(p.Buildings.Count));
            p.RemoveBuilding(building);
            Notify(p.OwnerId, $"Los bombarderos de {wing.Owner.Name} han destruido {building.Info().WithArticle.Replace("un ", "el ").Replace("una ", "la ")} de {PlaceName(p)}.");
            if (wing.OwnerId == HumanPlayerId) Notify(HumanPlayerId, $"{wing.Name} ha destruido {building.Info().WithArticle} en {PlaceName(p)}.");
        }
    }

    /// <summary>A naval wing's day: it strikes the enemy fleets in the seas of its area and the convoys crossing them.</summary>
    private void StrikeAtSea(AirWing wing)
    {
        double fire = wing.Info.Attack * Effectiveness(wing) * MilitaryRules.NavalStrikeHours;
        if (EnemyRulesTheAir(wing.OwnerId, Map.Provinces[wing.TargetProvinceId!.Value])) fire *= MilitaryRules.UnescortedBomberEffect;
        var fleets = Units.Where(u => u.IsFleet && AtWar(u.OwnerId, wing.OwnerId) && Covers(wing, Map.Provinces[u.ProvinceId])).ToList();
        if (fleets.Count > 0) Damage(fleets, fire);
        foreach (var fleet in fleets.Where(f => f.Citizens < 1).ToList()) Sink(fleet);
        foreach (var s in _shipments.Where(s => s.Convoys > 0 && AtWar(s.OwnerId, wing.OwnerId) && s.SeaRoute.Any(id => Covers(wing, Map.Provinces[id]))).ToList())
            SinkConvoys(s, Math.Min(MilitaryRules.MaxDailyConvoyLoss, fire / (fire + MilitaryRules.ConvoyEvasion)), [wing.OwnerId]);
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
        double room = transports.Sum(w => w.Info.Capacity * w.Planes.StrengthShare);
        if (room < unit.Citizens) return CommandResult.Fail($"Los transportes solo llevan {room:N0} hombres.");
        if (target.IsWater) return CommandResult.Fail("Solo se lanzan sobre tierra.");
        if (transports.Any(w => !InRange(w, target))) return CommandResult.Fail($"Está fuera del alcance de los transportes ({transports.Min(w => w.Info.RangeKm):N0} km).");
        if (EnemyRegimentsIn(target.Id, unit.OwnerId).Any()) return CommandResult.Fail("Hay tropas enemigas: no se puede saltar sobre ellas.");
        if (target.IsOwned && target.ControllerId != unit.OwnerId && !AtWar(unit.OwnerId, target.ControllerId))
            return CommandResult.Fail("Solo sobre tierra propia, libre o de una nación en guerra contigo.");
        return CommandResult.Success();
    }

    /// <summary>The transport wings at the unit's airfield ready to fly.</summary>
    private IEnumerable<AirWing> Transports(Unit unit) =>
        _wings.Where(w => w.OwnerId == unit.OwnerId && w.Type == BattalionType.AirTransports && w.BaseProvinceId == unit.ProvinceId
                          && w.Planes.OrganisationShare >= MilitaryRules.MinFlyingOrganisation && w.PlaneCount >= 1);

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
        foreach (var w in Transports(unit).ToList()) w.Planes.Organisation /= 2;
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
