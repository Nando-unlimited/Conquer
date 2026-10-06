using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// The war at sea beyond the battles: convoys carry the shipments that cross the sea, and fleets are given missions:
/// patrolling (going after the enemy fleets in the neighbouring seas), escorting the nation's convoys, raiding the
/// enemy's, and blockading the enemy ports on their sea (no ships built, nothing sent by sea and no sea trade).
/// </summary>
public sealed partial class GameSession
{
    /// <summary>Convoys at sea carrying shipments.</summary>
    public double ConvoysInUse(Player player) => _shipments.Where(s => s.OwnerId == player.Id).Sum(s => s.Convoys);

    /// <summary>Convoys in port, free to carry new shipments.</summary>
    public double FreeConvoys(Player player) => Math.Max(0, player.Convoys - ConvoysInUse(player));

    public CommandResult CanOrderConvoys(Player player)
    {
        if (!player.Techs.Contains(Science.Tech.Navigation)) return CommandResult.Fail("Requiere navegación a vela.");
        if (!Shipyards(player).Any()) return CommandResult.Fail("Necesitas un puerto: constrúyelo en una ciudad con costa.");
        return CommandResult.Success();
    }

    /// <summary>Adds a batch of <see cref="MilitaryRules.ConvoysPerOrder"/> convoys to the end of the shipyards' queue.</summary>
    public CommandResult OrderConvoys(int playerId)
    {
        var player = Players[playerId];
        var check = CanOrderConvoys(player);
        if (!check.Ok) return check;
        player.ShipOrders.Add(new ShipOrder { Id = _nextShipOrderId++, Type = BattalionType.Transport, Model = 0, Convoys = true });
        return CommandResult.Success($"{MilitaryRules.ConvoysPerOrder} convoyes encargados a los astilleros: {player.ShipOrders.Count}.º de la cola.");
    }

    public CommandResult SetFleetMission(int playerId, int fleetId, FleetMission mission)
    {
        if (UnitById(fleetId) is not { } fleet || fleet.OwnerId != playerId || !fleet.IsFleet) return CommandResult.Fail("Solo las flotas tienen misiones.");
        fleet.Mission = mission;
        _blockadesAt = -1;
        return CommandResult.Success($"{fleet.Name}: {MissionName(mission).ToLowerInvariant()}.");
    }

    public static string MissionName(FleetMission mission) => mission switch
    {
        FleetMission.Patrol => "Patrullar",
        FleetMission.Escort => "Escoltar convoyes",
        FleetMission.Raid => "Atacar convoyes",
        FleetMission.Blockade => "Bloquear puertos",
        _ => "Sin misión",
    };

    public static string MissionDescription(FleetMission mission) => mission switch
    {
        FleetMission.Patrol => "Va a por las flotas enemigas que entren en los mares vecinos.",
        FleetMission.Escort => "Protege a tus convoyes en este mar y los vecinos: los ataques enemigos hunden menos.",
        FleetMission.Raid => "Hunde los convoyes enemigos que crucen este mar o los vecinos (los submarinos, el doble).",
        FleetMission.Blockade => "Cierra los puertos enemigos de este mar: no construyen barcos, no envían nada por mar y pierden su comercio marítimo.",
        _ => "Solo combate a las flotas enemigas que encuentre.",
    };

    /// <summary>The seas a fleet's mission reaches: where it is and the seas next to it.</summary>
    private IEnumerable<int> MissionSeas(Unit fleet) =>
        Map.Provinces[fleet.ProvinceId].Neighbors.Where(n => Map.Provinces[n].IsWater).Append(fleet.ProvinceId);

    /// <summary>Fleets of the nation's enemies blockading the port: on a mission to, in a sea next to it.</summary>
    public IEnumerable<Unit> BlockadersOf(Province port) =>
        !port.CityId.HasValue || !port.Has(Buildings.BuildingType.Port) || port.OwnerId < 0 ? []
        : Units.Where(u => u.IsFleet && u.Mission == FleetMission.Blockade && AtWar(u.OwnerId, port.OwnerId) && port.Neighbors.Contains(u.ProvinceId));

    private long _blockadesAt = -1;
    private readonly HashSet<int> _blockaded = [];

    /// <summary>Whether enemy fleets blockade the port (worked out once an hour, or again when a mission changes).</summary>
    public bool IsBlockaded(Province port)
    {
        if (_blockadesAt != Date.Hours)
        {
            _blockadesAt = Date.Hours;
            _blockaded.Clear();
            foreach (var fleet in Units.Where(u => u.IsFleet && u.Mission == FleetMission.Blockade))
                foreach (int n in Map.Provinces[fleet.ProvinceId].Neighbors)
                    if (BlockadersOf(Map.Provinces[n]).Any()) _blockaded.Add(n);
        }
        return _blockaded.Contains(port.Id);
    }

    /// <summary>The ports a fleet blockades now.</summary>
    public IEnumerable<Province> Blockading(Unit fleet) =>
        fleet.Mission != FleetMission.Blockade ? []
        : Map.Provinces[fleet.ProvinceId].Neighbors.Select(n => Map.Provinces[n]).Where(p => IsBlockaded(p) && AtWar(fleet.OwnerId, p.OwnerId));

    /// <summary>A fleet's strength against convoys: its ships' fire, submarines' twice over.</summary>
    private static double RaidFire(Unit fleet) =>
        fleet.Ships.Sum(s => s.Info.Attack * s.StrengthShare * (0.5 + 0.5 * s.OrganisationShare) * (s.Type == BattalionType.Submarine ? MilitaryRules.SubmarineRaidBonus : 1));

    /// <summary>
    /// Once a day every shipment crossing the sea runs the raiders on its route: the enemy fleets raiding a sea on it or
    /// next to it sink a share of its convoys and cargo (see <see cref="MilitaryRules.ConvoyEvasion"/>), the less the
    /// more of its own nation's fleets escort those seas.
    /// </summary>
    private void DailyConvoyRaids()
    {
        foreach (var player in Players) player.ConvoysLostLastDay = player.ConvoysSunkLastDay = 0;
        var raiders = Units.Where(u => u.IsFleet && u.Mission == FleetMission.Raid).ToList();
        if (raiders.Count == 0) return;
        var escorts = Units.Where(u => u.IsFleet && u.Mission == FleetMission.Escort).ToList();
        foreach (var s in _shipments.Where(s => s.Convoys > 0).ToList())
        {
            var route = s.SeaRoute.ToHashSet();
            var hunting = raiders.Where(r => AtWar(r.OwnerId, s.OwnerId) && MissionSeas(r).Any(route.Contains)).ToList();
            if (hunting.Count == 0) continue;
            double raid = hunting.Sum(RaidFire);
            double escort = escorts.Where(e => e.OwnerId == s.OwnerId && MissionSeas(e).Any(route.Contains)).Sum(ExpectedNavalFire);
            double share = Math.Min(MilitaryRules.MaxDailyConvoyLoss, raid / (raid + MilitaryRules.EscortWeight * escort + MilitaryRules.ConvoyEvasion));
            if (share <= 0) continue;

            var owner = Players[s.OwnerId];
            double lost = s.Convoys * share;
            s.Convoys -= lost;
            owner.Convoys = Math.Max(0, owner.Convoys - lost);
            owner.ConvoysLostLastDay += lost;
            s.Men *= 1 - share;
            s.Ammo *= 1 - share;
            foreach (var key in s.Pieces.Keys.ToList()) s.Pieces[key] *= 1 - share;
            foreach (var raider in hunting) Players[raider.OwnerId].ConvoysSunkLastDay += lost / hunting.Count;
        }
        if (Human.ConvoysLostLastDay >= 0.05)
            Notify(HumanPlayerId, $"El enemigo ha hundido {Human.ConvoysLostLastDay:0.#} convoyes con lo que llevaban. Escolta sus rutas con tus flotas.");
        if (Human.ConvoysSunkLastDay >= 0.05)
            Notify(HumanPlayerId, $"Tus flotas han hundido {Human.ConvoysSunkLastDay:0.#} convoyes enemigos.");
    }

    /// <summary>
    /// Every hour, patrolling fleets that are not moving or fighting set off after an enemy fleet in a neighbouring sea,
    /// the weakest first; they keep patrolling where the chase leaves them.
    /// </summary>
    private void Patrol()
    {
        foreach (var fleet in Units.Where(u => u.IsFleet && u.Mission == FleetMission.Patrol && !u.IsMoving).ToList())
        {
            if (EnemyFleetsIn(fleet.ProvinceId, fleet.OwnerId).Any()) continue;
            var prey = Map.Provinces[fleet.ProvinceId].Neighbors.Where(n => Map.Provinces[n].IsWater)
                .SelectMany(n => EnemyFleetsIn(n, fleet.OwnerId)).MinBy(ExpectedNavalFire);
            if (prey != null) MoveUnit(fleet.OwnerId, fleet.Id, prey.ProvinceId);
        }
    }
}
