using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Armies, as in Hearts of Iron III: regiments of battalions trained in cities, a chain of command of
/// HQs, supply from the nation's cities, hour-by-hour battles and the occupation of enemy land.
/// </summary>
public sealed partial class GameSession
{
    private readonly List<Battle> _battles = [];
    /// <summary>Provinces where each player's units are in supply, recomputed every day.</summary>
    private readonly Dictionary<int, HashSet<int>> _supplied = [];
    private readonly Dictionary<(int Player, int Level), int> _unitNumbers = [];
    private int _nextTemplateId;

    public IReadOnlyList<Battle> Battles => _battles;

    public Battle? BattleIn(int provinceId) => _battles.FirstOrDefault(b => b.ProvinceId == provinceId);

    // ------------------------------------------------------------------ units

    /// <summary>Puts a new regiment of the given battalions on the map, at full strength (tests and training).</summary>
    internal Unit AddRegiment(int ownerId, int provinceId, params BattalionType[] battalions)
    {
        var unit = AddUnit(ownerId, UnitType.Regiment, provinceId, 0, NextUnitNumber(ownerId, CommandLevels.Regiment));
        foreach (var type in battalions) unit.Battalions.Add(new Battalion(type));
        return unit;
    }

    internal Unit AddHeadquarters(int ownerId, int provinceId, int level) =>
        AddUnit(ownerId, UnitType.Headquarters, provinceId, CommandLevels.Info(level).Staff, NextUnitNumber(ownerId, level), level);

    /// <summary>Units of each level are numbered in order for each nation: Legión I, Legión II…</summary>
    private int NextUnitNumber(int playerId, int level)
    {
        int n = _unitNumbers.GetValueOrDefault((playerId, level)) + 1;
        _unitNumbers[(playerId, level)] = n;
        return n;
    }

    public Unit? CommanderOf(Unit unit) => unit.CommanderId is int id ? UnitById(id) : null;

    public IEnumerable<Unit> SubordinatesOf(Unit hq) => Units.Where(u => u.CommanderId == hq.Id);

    /// <summary>Rough fighting value of a regiment: its battalions' attack and defence, scaled by the men left.</summary>
    public static double RegimentPower(Unit unit) => unit.Battalions.Sum(b => (b.Info.Attack + b.Info.Defense) / 2 * b.StrengthShare);

    /// <summary>Fighting value of all of a nation's regiments.</summary>
    public double MilitaryPower(int playerId) => Units.Where(u => u.OwnerId == playerId && u.IsMilitary).Sum(RegimentPower);

    /// <summary>Enemy regiments (of anyone at war with the player) standing in a province; those aboard a ship do not count.</summary>
    public IEnumerable<Unit> EnemyRegimentsIn(int provinceId, int playerId) =>
        Units.Where(u => u.IsMilitary && !u.IsAboard && u.ProvinceId == provinceId && AtWar(u.OwnerId, playerId));

    // ------------------------------------------------------------------ movement

    /// <summary>
    /// Whether a unit may step into a province: free land and its own nation's always; a province
    /// held by another nation only for regiments at war with it; the sea only for aircraft (troops go by
    /// ship). A fleet sails the sea its nation can navigate and enters its own ports.
    /// </summary>
    public bool CanUnitEnter(Unit unit, int provinceId)
    {
        var p = Map.Provinces[provinceId];
        if (unit.IsFleet) return CanFleetEnter(unit, p);
        if (p.IsWater) return unit.Flies;
        int holder = p.IsOwned ? p.ControllerId : -1;
        if (holder < 0 || holder == unit.OwnerId) return true;
        return unit.IsMilitary && AtWar(unit.OwnerId, holder);
    }

    /// <summary>Navigation opens coastal seas and lakes; cartography, the open ocean too.</summary>
    public static bool CanSail(Player player, Province sea) => sea.Biome is Biome.ShallowSea or Biome.Lake
        ? player.Techs.Contains(Tech.Navigation)
        : player.Techs.Contains(Tech.Cartography);

    /// <summary>Hours a unit needs to walk from one province to its neighbour, at its own pace.</summary>
    private double UnitStepHours(Unit unit, int from, int to) => Pathfinder.StepHours(from, to) / unit.Speed;

    public CommandResult MoveUnit(int playerId, int unitId, int targetProvinceId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        // Aboard, a move is a landing; on land, a move onto one's own fleet at sea is boarding it.
        if (unit.IsAboard) return targetProvinceId == unit.ProvinceId ? CommandResult.Success() : Disembark(playerId, unitId, targetProvinceId);
        var target = Map.Provinces[targetProvinceId];
        if (target.IsWater && !unit.IsFleet && !unit.Flies)
        {
            var fleet = Units.FirstOrDefault(f => f.IsFleet && f.ProvinceId == targetProvinceId && CanEmbark(unit, f).Ok);
            return fleet != null ? Embark(playerId, unitId, fleet.Id)
                : CommandResult.Fail("Para cruzar el mar hay que embarcar: clic derecho sobre una flota tuya con transportes, junto a la costa.");
        }
        CancelAttack(unit);
        if (targetProvinceId == unit.ProvinceId)
        {
            unit.Path.Clear();
            unit.StepHours = unit.HoursToNext = 0;
            return CommandResult.Success();
        }
        if (!CanUnitEnter(unit, targetProvinceId))
        {
            if (target.IsWater) return CommandResult.Fail(target.Biome is Biome.ShallowSea or Biome.Lake ? "Hace falta la navegación a vela para ir por mar." : "Hace falta la cartografía para cruzar el océano.");
            if (unit.IsFleet) return CommandResult.Fail("Las flotas solo atracan en tus ciudades con costa.");
            string holder = Players[target.ControllerId].Name;
            return CommandResult.Fail(unit.IsMilitary ? $"No estás en guerra con {holder}." : $"No puede entrar en tierras de {holder}.");
        }
        if (Pathfinder.FindPath(unit.ProvinceId, targetProvinceId, id => CanUnitEnter(unit, id)) is not { } route)
            return CommandResult.Fail("No hay camino hasta allí.");
        var (path, hours) = route;
        unit.Path.Clear();
        unit.Path.AddRange(path);
        unit.StepHours = unit.HoursToNext = UnitStepHours(unit, unit.ProvinceId, path[0]);
        return CommandResult.Success($"Llegada en {FormatHours(hours / unit.Speed)}.");
    }

    /// <summary>
    /// Every unit walks an hour along its path. A regiment stepping into a province held by enemy
    /// regiments attacks it instead; one stepping into enemy land without defenders occupies it.
    /// </summary>
    private void MoveUnits()
    {
        foreach (var unit in Units.ToList())
        {
            if (!unit.IsMoving || unit.AttackingProvinceId.HasValue || !_unitsById.ContainsKey(unit.Id)) continue;
            unit.HoursToNext -= 1;
            while (unit.Path.Count > 0 && unit.HoursToNext <= 0)
            {
                int next = unit.Path[0];
                bool enemies = EnemyRegimentsIn(next, unit.OwnerId).Any();
                if (!CanUnitEnter(unit, next) || (enemies && !unit.IsMilitary))
                {
                    unit.Path.Clear();
                    unit.StepHours = unit.HoursToNext = 0;
                    if (unit.OwnerId == HumanPlayerId) Notify(unit.OwnerId, $"{unit.Name}: el camino está cortado.");
                    break;
                }
                if (enemies)
                {
                    StartAttack(unit, next);
                    break;
                }
                double carry = unit.HoursToNext;
                EnterProvince(unit, next);
                if (unit.IsFleet && EnemyFleetsIn(next, unit.OwnerId).Any())
                {
                    // Enemy ships ahead: the fleet stops and fights.
                    NotifyNavalEncounter(unit, next);
                    unit.Path.Clear();
                    unit.StepHours = unit.HoursToNext = 0;
                    break;
                }
                if (unit.Path.Count > 0)
                {
                    unit.StepHours = UnitStepHours(unit, unit.ProvinceId, unit.Path[0]);
                    unit.HoursToNext = unit.StepHours + carry;
                }
                else
                {
                    unit.StepHours = unit.HoursToNext = 0;
                    if (unit.OwnerId == HumanPlayerId) Notify(unit.OwnerId, $"{unit.Name}: llegada a su destino.");
                }
            }
        }
    }

    /// <summary>The unit arrives in the next province of its path; a fleet brings its cargo; a regiment takes it from the enemy.</summary>
    private void EnterProvince(Unit unit, int provinceId)
    {
        unit.ProvinceId = provinceId;
        if (unit.Path.Count > 0 && unit.Path[0] == provinceId) unit.Path.RemoveAt(0);
        if (unit.IsFleet) SyncCargo(unit);
        if (!unit.IsMilitary) return;
        var p = Map.Provinces[provinceId];
        if (p.IsOwned && p.ControllerId != unit.OwnerId && AtWar(unit.OwnerId, p.ControllerId)) Occupy(p, unit.OwnerId);
    }

    /// <summary>
    /// The province passes to the player's control: back to its owner if it was theirs, or occupied if it
    /// was the enemy's. Enemy HQs and settlers there flee, or are captured if they have nowhere to go.
    /// </summary>
    private void Occupy(Province p, int playerId)
    {
        int previous = p.ControllerId;
        p.ControllerId = playerId;
        OwnershipChanged?.Invoke(p.Id);
        // Civilians and fleets in port get away; those aboard go with their ships.
        foreach (var civilian in Units.Where(u => u.ProvinceId == p.Id && !u.IsMilitary && !u.IsAboard && AtWar(u.OwnerId, playerId)).ToList())
            Retreat(civilian);

        string place = PlaceName(p);
        if (playerId == HumanPlayerId)
            Notify(HumanPlayerId, p.OwnerId == playerId ? $"Liberada {place}." : $"Nuestras tropas ocupan {place}.");
        else if (previous == HumanPlayerId)
            Notify(HumanPlayerId, $"{Players[playerId].Name} ocupa {place}.");
    }

    // ------------------------------------------------------------------ training

    public CommandResult CanTrain(City city, BattalionType type)
    {
        var p = Map.Provinces[city.ProvinceId];
        if (type.Info().Naval && !IsPort(p, city.OwnerId)) return CommandResult.Fail("Los barcos solo se construyen en ciudades con puerto.");
        if (type.Info().Shipyard is BuildingType yard && !p.Buildings.Contains(yard))
            return CommandResult.Fail($"Requiere {yard.Info().Name.ToLowerInvariant()} en la ciudad.");
        return CanRaiseTroops(city, type.Info().Men, type.Info().Cost, type.Info().Requires);
    }

    /// <summary>Whether a city can raise troops: the advances are known, it is free, has the men to spare and the nation can pay.</summary>
    private CommandResult CanRaiseTroops(City city, int men, Economy.ResourceCost cost, IEnumerable<Tech> requires)
    {
        var player = Players[city.OwnerId];
        var p = Map.Provinces[city.ProvinceId];
        var missing = requires.Where(t => !player.Techs.Contains(t)).ToList();
        if (missing.Count > 0) return CommandResult.Fail("Requiere " + string.Join(" y ", missing.Select(t => t.Info().Name.ToLowerInvariant())) + ".");
        if (p.IsOccupied) return CommandResult.Fail("La ciudad está ocupada por el enemigo.");
        if (p.Population - men < GameRules.MinCityPopulation) return CommandResult.Fail($"Hacen falta {men + GameRules.MinCityPopulation} habitantes.");
        if (!player.Stockpile.Has(cost)) return CommandResult.Fail($"Cuesta {cost}.");
        return CommandResult.Success();
    }

    /// <summary>Pays for a battalion and takes its men from the city; it forms a new regiment when trained.</summary>
    public CommandResult Train(int playerId, int cityId, BattalionType type)
    {
        if (CityById(cityId) is not { } city || city.OwnerId != playerId) return CommandResult.Fail("Ciudad no válida.");
        var check = CanTrain(city, type);
        if (!check.Ok) return check;
        var info = type.Info();
        Players[playerId].Stockpile.TrySpend(info.Cost);
        Map.Provinces[city.ProvinceId].Population -= info.Men;
        city.Training.Add(new TrainingOrder(type));
        return CommandResult.Success($"{info.Name} en instrucción: {info.TrainingDays} días.");
    }

    public CommandResult CanRaiseHeadquarters(City city, int level)
    {
        var info = CommandLevels.Info(level);
        var p = Map.Provinces[city.ProvinceId];
        if (p.IsOccupied) return CommandResult.Fail("La ciudad está ocupada por el enemigo.");
        if (p.Population - info.Staff < GameRules.MinCityPopulation) return CommandResult.Fail($"Hacen falta {info.Staff + GameRules.MinCityPopulation} habitantes.");
        if (!Players[city.OwnerId].Stockpile.Has(info.Cost)) return CommandResult.Fail($"Cuesta {info.Cost}.");
        return CommandResult.Success();
    }

    /// <summary>Pays for an HQ of the given level (1 corps … 4 theatre); its staff come from the city.</summary>
    public CommandResult RaiseHeadquarters(int playerId, int cityId, int level)
    {
        if (CityById(cityId) is not { } city || city.OwnerId != playerId) return CommandResult.Fail("Ciudad no válida.");
        var check = CanRaiseHeadquarters(city, level);
        if (!check.Ok) return check;
        var info = CommandLevels.Info(level);
        Players[playerId].Stockpile.TrySpend(info.Cost);
        Map.Provinces[city.ProvinceId].Population -= info.Staff;
        city.Training.Add(new TrainingOrder(level));
        return CommandResult.Success($"Cuartel general de {Formations.LevelName(level, Players[playerId].ArmyEra).ToLowerInvariant()} en formación: {info.TrainingDays} días.");
    }

    /// <summary>Every order a city is training advances a day; finished ones appear in the city.</summary>
    private void DailyTraining(Player player)
    {
        foreach (var city in Cities.Where(c => c.OwnerId == player.Id && !Map.Provinces[c.ProvinceId].IsOccupied))
        {
            foreach (var order in city.Training.ToList())
            {
                if (--order.DaysLeft > 0) continue;
                city.Training.Remove(order);
                var unit = order.Battalion is BattalionType ship && ship.Info().Naval ? AddFleet(player.Id, city.ProvinceId, ship)
                    : order.Battalion is BattalionType type ? AddRegiment(player.Id, city.ProvinceId, type)
                    : order.TemplateBattalions.Count > 0 ? AddRegiment(player.Id, city.ProvinceId, [.. order.TemplateBattalions])
                    : AddHeadquarters(player.Id, city.ProvinceId, order.HeadquartersLevel);
                if (player.IsHuman) Notify(player.Id, $"Nueva unidad en {city.Name}: {unit.Name} ({order.Name(player.ArmyEra).ToLowerInvariant()}).");
            }
        }
    }

    // ------------------------------------------------------------------ templates

    public RegimentTemplate? TemplateById(Player player, int templateId) => player.Templates.FirstOrDefault(t => t.Id == templateId);

    internal RegimentTemplate AddTemplate(Player player, IEnumerable<BattalionType> battalions)
    {
        int number = player.Templates.Count == 0 ? 1 : player.Templates.Max(t => t.Number) + 1;
        var template = new RegimentTemplate(_nextTemplateId++, number, battalions);
        player.Templates.Add(template);
        return template;
    }

    /// <summary>A new template with a single warrior battalion, to be filled in.</summary>
    public CommandResult CreateTemplate(int playerId)
    {
        var template = AddTemplate(Players[playerId], [BattalionType.Warriors]);
        return CommandResult.Success($"{template.Name} creada.");
    }

    public CommandResult DuplicateTemplate(int playerId, int templateId)
    {
        if (TemplateById(Players[playerId], templateId) is not { } source) return CommandResult.Fail("Plantilla no válida.");
        var copy = AddTemplate(Players[playerId], source.Battalions);
        return CommandResult.Success($"{copy.Name} copiada de {source.Name}.");
    }

    public CommandResult DeleteTemplate(int playerId, int templateId)
    {
        var player = Players[playerId];
        if (TemplateById(player, templateId) is not { } template) return CommandResult.Fail("Plantilla no válida.");
        if (player.Templates.Count == 1) return CommandResult.Fail("Hace falta al menos una plantilla.");
        player.Templates.Remove(template);
        return CommandResult.Success($"{template.Name} borrada.");
    }

    public CommandResult CanAddToTemplate(Player player, RegimentTemplate template, BattalionType type)
    {
        if (type.Info().Naval) return CommandResult.Fail("Los barcos no van en plantillas: se construyen sueltos en los puertos.");
        if (template.Battalions.Count >= MilitaryRules.MaxBattalionsPerRegiment)
            return CommandResult.Fail($"Como mucho {Formations.BattalionCount(MilitaryRules.MaxBattalionsPerRegiment, player.ArmyEra)} por regimiento.");
        var missing = type.Info().Requires.Where(t => !player.Techs.Contains(t)).ToList();
        if (missing.Count > 0) return CommandResult.Fail("Requiere " + string.Join(" y ", missing.Select(t => t.Info().Name.ToLowerInvariant())) + ".");
        return CommandResult.Success();
    }

    public CommandResult AddToTemplate(int playerId, int templateId, BattalionType type)
    {
        var player = Players[playerId];
        if (TemplateById(player, templateId) is not { } template) return CommandResult.Fail("Plantilla no válida.");
        var check = CanAddToTemplate(player, template, type);
        if (!check.Ok) return check;
        template.Battalions.Add(type);
        return CommandResult.Success();
    }

    public CommandResult RemoveFromTemplate(int playerId, int templateId, int index)
    {
        if (TemplateById(Players[playerId], templateId) is not { } template) return CommandResult.Fail("Plantilla no válida.");
        if (template.Battalions.Count == 1) return CommandResult.Fail("Una plantilla necesita al menos un batallón.");
        if (index < 0 || index >= template.Battalions.Count) return CommandResult.Fail("Tropa no válida.");
        template.Battalions.RemoveAt(index);
        return CommandResult.Success();
    }

    public CommandResult CanTrainTemplate(City city, RegimentTemplate template) =>
        CanRaiseTroops(city, template.Men, template.Cost, template.Requires);

    /// <summary>Pays for every battalion of a template at once; they train side by side and form one regiment.</summary>
    public CommandResult TrainTemplate(int playerId, int cityId, int templateId)
    {
        if (CityById(cityId) is not { } city || city.OwnerId != playerId) return CommandResult.Fail("Ciudad no válida.");
        if (TemplateById(Players[playerId], templateId) is not { } template) return CommandResult.Fail("Plantilla no válida.");
        var check = CanTrainTemplate(city, template);
        if (!check.Ok) return check;
        Players[playerId].Stockpile.TrySpend(template.Cost);
        Map.Provinces[city.ProvinceId].Population -= template.Men;
        city.Training.Add(new TrainingOrder(template));
        return CommandResult.Success($"Regimiento de la {template.Name} en instrucción: {template.TrainingDays} días.");
    }

    // ------------------------------------------------------------------ organisation

    public CommandResult CanMerge(Unit unit, Unit other)
    {
        if (unit.Id == other.Id || unit.IsAboard || other.IsAboard || !(unit.IsMilitary && other.IsMilitary || unit.IsFleet && other.IsFleet))
            return CommandResult.Fail($"Solo se unen {Formations.LevelPlural(CommandLevels.Regiment, unit.Owner.ArmyEra)} entre sí, o flotas entre sí.");
        if (unit.OwnerId != other.OwnerId || unit.ProvinceId != other.ProvinceId) return CommandResult.Fail("Deben estar en la misma provincia.");
        if (unit.AttackingProvinceId.HasValue || other.AttackingProvinceId.HasValue) return CommandResult.Fail("Una de ellas está atacando.");
        if (unit.IsFleet && unit.Battalions.Count + other.Battalions.Count > MilitaryRules.MaxShipsPerFleet)
            return CommandResult.Fail($"Como mucho {Formations.ShipCount(MilitaryRules.MaxShipsPerFleet)} por flota.");
        if (unit.IsMilitary && unit.Battalions.Count + other.Battalions.Count > MilitaryRules.MaxBattalionsPerRegiment)
            return CommandResult.Fail($"Como mucho {Formations.BattalionCount(MilitaryRules.MaxBattalionsPerRegiment, unit.Owner.ArmyEra)} por unidad.");
        return CommandResult.Success();
    }

    /// <summary>The other regiment's battalions (or fleet's ships, and what they carry) join this one, and the other disappears.</summary>
    public CommandResult Merge(int playerId, int unitId, int otherId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId || UnitById(otherId) is not { } other) return CommandResult.Fail("Unidad no válida.");
        var check = CanMerge(unit, other);
        if (!check.Ok) return check;
        unit.Battalions.AddRange(other.Battalions);
        other.Battalions.Clear();
        foreach (var cargo in CargoOf(other).ToList()) cargo.CarrierId = unit.Id;
        unit.CommanderId ??= other.CommanderId;
        RemoveUnit(other);
        unit.Path.Clear();
        unit.StepHours = unit.HoursToNext = 0;
        return CommandResult.Success($"{other.Name} se une a {unit.Name}.");
    }

    /// <summary>One battalion leaves its regiment and forms a new one in the same province.</summary>
    public CommandResult Split(int playerId, int unitId, int battalionIndex)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId || !(unit.IsMilitary || unit.IsFleet) || unit.IsAboard) return CommandResult.Fail("Unidad no válida.");
        if (unit.Battalions.Count < 2) return CommandResult.Fail(unit.IsFleet ? "Solo tiene un barco." : $"Solo tiene {Formations.BattalionCount(1, unit.Owner.ArmyEra)}.");
        if (unit.AttackingProvinceId.HasValue) return CommandResult.Fail("Está atacando.");
        if (battalionIndex < 0 || battalionIndex >= unit.Battalions.Count) return CommandResult.Fail("Tropa no válida.");
        var battalion = unit.Battalions[battalionIndex];
        if (unit.IsFleet && CargoMen(unit) > unit.Capacity - battalion.Info.Capacity) return CommandResult.Fail("La carga no cabría en el resto de la flota.");
        unit.Battalions.RemoveAt(battalionIndex);
        var split = AddUnit(playerId, unit.Type, unit.ProvinceId, 0, NextUnitNumber(playerId, unit.IsFleet ? FleetNumbering : CommandLevels.Regiment));
        split.Battalions.Add(battalion);
        return CommandResult.Success($"{Formations.BattalionName(battalion.Info, unit.Owner.ArmyEra)} forma una unidad nueva: {split.Name}.");
    }

    public CommandResult CanAttach(Unit unit, Unit hq)
    {
        if (unit.CommandLevel < 0) return CommandResult.Fail("Esta unidad no forma parte de la cadena de mando.");
        if (!hq.IsHeadquarters || hq.OwnerId != unit.OwnerId) return CommandResult.Fail("Cuartel general no válido.");
        if (hq.HeadquartersLevel != unit.CommandLevel + 1)
            return CommandResult.Fail($"Esta unidad solo puede depender de un cuartel de {Formations.LevelName(unit.CommandLevel + 1, unit.Owner.ArmyEra).ToLowerInvariant()}.");
        if (unit.CommanderId == hq.Id) return CommandResult.Fail("Ya depende de él.");
        if (SubordinatesOf(hq).Count() >= CommandLevels.Info(hq.HeadquartersLevel).MaxSubordinates) return CommandResult.Fail($"{hq.Name} ya está completo.");
        return CommandResult.Success();
    }

    /// <summary>The unit reports to an HQ one level up.</summary>
    public CommandResult Attach(int playerId, int unitId, int hqId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId || UnitById(hqId) is not { } hq) return CommandResult.Fail("Unidad no válida.");
        var check = CanAttach(unit, hq);
        if (!check.Ok) return check;
        unit.CommanderId = hq.Id;
        return CommandResult.Success($"{unit.Name} queda bajo el mando de {hq.Name}.");
    }

    public CommandResult Detach(int playerId, int unitId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        if (unit.CommanderId is null) return CommandResult.Fail("No depende de ningún cuartel general.");
        unit.CommanderId = null;
        return CommandResult.Success();
    }

    /// <summary>Whether a unit's HQ is close enough to command it.</summary>
    public bool InCommandRange(Unit unit) =>
        CommanderOf(unit) is { } hq &&
        Map.DistanceKm(Map.Provinces[unit.ProvinceId], Map.Provinces[hq.ProvinceId]) <= CommandLevels.Info(hq.HeadquartersLevel).RangeKm;

    /// <summary>
    /// Combat and recovery bonus a unit gets from its chain of command: its own HQ in range, plus a
    /// little for each higher HQ linked in range above that.
    /// </summary>
    public double CommandBonus(Unit unit)
    {
        double bonus = 0;
        for (var link = unit; InCommandRange(link); link = CommanderOf(link)!)
            bonus += link == unit ? MilitaryRules.CommandBonus : MilitaryRules.HigherCommandBonus;
        return bonus;
    }

    // ------------------------------------------------------------------ supply and upkeep

    /// <summary>
    /// Supply runs from the nation's cities through land it controls (or nobody owns) up to
    /// <see cref="MilitaryRules.SupplyRangeHours"/> away, and one province beyond: the front line.
    /// </summary>
    private HashSet<int> ComputeSupply(Player player)
    {
        var sources = Cities.Where(c => c.OwnerId == player.Id && !Map.Provinces[c.ProvinceId].IsOccupied).Select(c => c.ProvinceId).ToList();
        var supplied = new HashSet<int>();
        if (sources.Count == 0) return supplied;
        var (hours, _) = Pathfinder.FromSources(sources, MilitaryRules.SupplyRangeHours,
            canEnter: id => !Map.Provinces[id].IsWater && (!Map.Provinces[id].IsOwned || Map.Provinces[id].ControllerId == player.Id));
        for (int id = 0; id < hours.Length; id++)
        {
            if (double.IsPositiveInfinity(hours[id])) continue;
            supplied.Add(id);
            supplied.UnionWith(Map.Provinces[id].Neighbors);
        }
        return supplied;
    }

    public bool IsInSupply(Unit unit) => IsSupplied(unit.OwnerId, unit.ProvinceId);

    /// <summary>Whether a nation's units in this province would be in supply.</summary>
    public bool IsSupplied(int playerId, int provinceId)
    {
        if (!_supplied.TryGetValue(playerId, out var supplied)) _supplied[playerId] = supplied = ComputeSupply(Players[playerId]);
        return supplied.Contains(provinceId);
    }

    /// <summary>A unit fighting: attacking a province, or defending one under attack.</summary>
    public bool InBattle(Unit unit) =>
        unit.AttackingProvinceId.HasValue || _battles.Any(b => b.ProvinceId == unit.ProvinceId && AtWar(b.AttackerId, unit.OwnerId));

    /// <summary>
    /// Once a day: training advances; supply is worked out; regiments in supply and out of combat
    /// recover organisation and get reinforcements from the capital, and those without supply wither.
    /// </summary>
    private void DailyMilitary(Player player)
    {
        DailyTraining(player);
        _supplied[player.Id] = ComputeSupply(player);
        var capital = player.CapitalCityId is int c && CityById(c) is { } city && !Map.Provinces[city.ProvinceId].IsOccupied
            ? Map.Provinces[city.ProvinceId] : null;

        foreach (var unit in Units.Where(u => u.OwnerId == player.Id && (u.IsMilitary || u.IsFleet)).ToList())
        {
            // Troops aboard live off the ships' stores; fleets are repaired and crewed only in their ports.
            if (unit.IsAboard || unit.IsFleet && !IsPort(Map.Provinces[unit.ProvinceId], player.Id)) continue;
            if (unit.IsMilitary && !IsInSupply(unit))
            {
                foreach (var b in unit.Battalions)
                {
                    b.Organisation = Math.Max(0, b.Organisation - b.Info.MaxOrganisation * MilitaryRules.OutOfSupplyOrganisationLoss);
                    b.Strength = Math.Max(0, b.Strength - b.Info.Men * MilitaryRules.OutOfSupplyAttrition);
                }
                if (unit.Citizens < 1) Destroy(unit, "se ha dispersado sin suministro");
                continue;
            }
            if (InBattle(unit)) continue;
            // A dry dock repairs a fleet faster: organisation and crews both.
            double repair = unit.IsFleet && Map.Provinces[unit.ProvinceId].Buildings.Contains(BuildingType.DryDock) ? MilitaryRules.DryDockRepair : 1;
            double recovery = MilitaryRules.OrganisationRecovery * (1 + CommandBonus(unit)) * (unit.IsMoving ? 0.5 : 1) * repair;
            foreach (var b in unit.Battalions)
            {
                b.Organisation = Math.Min(b.Info.MaxOrganisation, b.Organisation + b.Info.MaxOrganisation * recovery);
                double missing = b.Info.Men - b.Strength;
                if (missing <= 0 || capital == null) continue;
                double men = Math.Min(missing, Math.Min(b.Info.Men * MilitaryRules.ReinforcementRate * repair, capital.Population - GameRules.MinCityPopulation));
                if (men <= 0) continue;
                b.Strength += men;
                capital.Population -= men;
            }
        }
    }

    // ------------------------------------------------------------------ combat

    /// <summary>The regiment stops at the border and attacks the enemy regiments in the province ahead.</summary>
    private void StartAttack(Unit unit, int provinceId)
    {
        unit.AttackingProvinceId = provinceId;
        unit.HoursToNext = 0;
        var battle = _battles.FirstOrDefault(b => b.ProvinceId == provinceId && b.AttackerId == unit.OwnerId);
        if (battle == null)
        {
            int defender = EnemyRegimentsIn(provinceId, unit.OwnerId).First().OwnerId;
            battle = new Battle(provinceId, unit.OwnerId, defender, Date.Hours);
            _battles.Add(battle);
            string place = PlaceName(Map.Provinces[provinceId]);
            if (unit.OwnerId == HumanPlayerId) Notify(HumanPlayerId, $"Atacamos {place}.");
            else if (defender == HumanPlayerId) Notify(HumanPlayerId, $"{Players[unit.OwnerId].Name} ataca {place}.");
        }
        if (!battle.Attackers.Contains(unit.Id)) battle.Attackers.Add(unit.Id);
    }

    /// <summary>The regiment gives up its attack and stays where it is.</summary>
    private void CancelAttack(Unit unit)
    {
        if (unit.AttackingProvinceId is null) return;
        foreach (var battle in _battles) battle.Attackers.Remove(unit.Id);
        unit.AttackingProvinceId = null;
    }

    /// <summary>
    /// One hour of every battle. Each side's fire wears down the other's organisation and men; broken
    /// defenders retreat (or are destroyed if surrounded) and broken attackers give up. When no
    /// defenders are left, the attackers march in.
    /// </summary>
    private void ResolveBattles()
    {
        foreach (var battle in _battles.ToList())
        {
            var province = Map.Provinces[battle.ProvinceId];
            var attackers = battle.Attackers.Select(UnitById).OfType<Unit>().Where(u => u.AttackingProvinceId == battle.ProvinceId).ToList();
            var defenders = EnemyRegimentsIn(battle.ProvinceId, battle.AttackerId).ToList();
            if (attackers.Count == 0 || defenders.Count == 0)
            {
                EndBattle(battle, attackers, attackersWon: attackers.Count > 0);
                continue;
            }

            double attackFire = attackers.Sum(u => Fire(u, province, attacking: true));
            double defenseFire = defenders.Sum(u => Fire(u, province, attacking: false));
            Damage(defenders, attackFire);
            Damage(attackers, defenseFire);

            foreach (var unit in defenders.Where(Broken)) Retreat(unit);
            foreach (var unit in attackers.Where(Broken))
            {
                CancelAttack(unit);
                unit.Path.Clear();
                unit.StepHours = unit.HoursToNext = 0;
                if (unit.Citizens < 1) Destroy(unit, "aniquilada");
            }
        }
    }

    private static bool Broken(Unit unit) => unit.OrganisationShare < MilitaryRules.BreakingOrganisation || unit.Citizens < 1;

    /// <summary>
    /// Damage a regiment deals in an hour: each battalion's attack or defence, scaled by its men and
    /// organisation, the chain of command, supply, terrain and a little luck.
    /// </summary>
    /// <summary>How much harder those defending a province hit: its terrain, and its walls or castle.</summary>
    public static double DefenseMultiplier(Province province) =>
        MilitaryRules.DefenseMultiplier(province) * (1 + province.BuildingBonuses.Defense);

    private double Fire(Unit unit, Province province, bool attacking)
    {
        double fire = 0;
        foreach (var b in unit.Battalions)
        {
            double value = attacking ? b.Info.Attack : b.Info.Defense;
            if (attacking && b.Info.Mounted && MilitaryRules.IsRough(province.Biome)) value *= MilitaryRules.MountedRoughTerrainAttack;
            fire += value * b.StrengthShare * (0.5 + 0.5 * b.OrganisationShare);
        }
        fire *= 1 + CommandBonus(unit);
        if (!IsInSupply(unit)) fire *= MilitaryRules.OutOfSupplyEfficiency;
        if (!attacking) fire *= DefenseMultiplier(province);
        return fire * (1 + (_random.NextDouble() * 2 - 1) * MilitaryRules.CombatRandomness);
    }

    /// <summary>Spreads a side's fire over the enemy battalions as lost organisation and men.</summary>
    private static void Damage(List<Unit> units, double fire)
    {
        int battalions = units.Sum(u => u.Battalions.Count);
        if (battalions == 0) return;
        foreach (var b in units.SelectMany(u => u.Battalions))
        {
            b.Organisation = Math.Max(0, b.Organisation - fire * MilitaryRules.OrganisationDamage / battalions);
            b.Strength = Math.Max(0, b.Strength - fire * MilitaryRules.StrengthDamage / battalions);
        }
    }

    private void EndBattle(Battle battle, List<Unit> attackers, bool attackersWon)
    {
        _battles.Remove(battle);
        var province = Map.Provinces[battle.ProvinceId];
        string place = PlaceName(province);
        if (attackersWon)
        {
            foreach (var unit in attackers)
            {
                unit.AttackingProvinceId = null;
                EnterProvince(unit, battle.ProvinceId);
                unit.StepHours = unit.HoursToNext = unit.Path.Count > 0 ? UnitStepHours(unit, unit.ProvinceId, unit.Path[0]) : 0;
            }
        }
        foreach (var unit in attackers.Where(u => !attackersWon)) unit.AttackingProvinceId = null;

        if (battle.AttackerId == HumanPlayerId) Notify(HumanPlayerId, attackersWon ? $"Victoria en {place}." : $"Nuestro ataque a {place} ha fracasado.");
        else if (battle.DefenderId == HumanPlayerId) Notify(HumanPlayerId, attackersWon ? $"Hemos perdido {place}." : $"Hemos resistido en {place}.");
    }

    /// <summary>
    /// The unit falls back to a neighbouring province its nation holds (or free land) without enemies.
    /// With nowhere to go it is surrounded and destroyed.
    /// </summary>
    private void Retreat(Unit unit)
    {
        if (unit.IsFleet)
        {
            FleeOrSink(unit);
            return;
        }
        CancelAttack(unit);
        unit.Path.Clear();
        unit.StepHours = unit.HoursToNext = 0;
        var refuge = Map.Provinces[unit.ProvinceId].Neighbors
            .Where(n => CanUnitEnter(unit, n) && !EnemyRegimentsIn(n, unit.OwnerId).Any())
            .Where(n => !Map.Provinces[n].IsOwned || Map.Provinces[n].ControllerId == unit.OwnerId)
            .OrderByDescending(n => Map.Provinces[n].ControllerId == unit.OwnerId)
            .Select(n => (int?)n)
            .FirstOrDefault();
        if (refuge is int to && unit.Citizens >= 1) unit.ProvinceId = to;
        else Destroy(unit, "rodeada y destruida");
    }

    private void Destroy(Unit unit, string how)
    {
        RemoveUnit(unit);
        if (unit.OwnerId == HumanPlayerId) Notify(HumanPlayerId, $"{unit.Name}: {how}.");
    }
}
