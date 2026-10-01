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
    private int _nextOfficerId;

    public IReadOnlyList<Battle> Battles => _battles;

    public Battle? BattleIn(int provinceId) => _battles.FirstOrDefault(b => b.ProvinceId == provinceId);

    // ------------------------------------------------------------------ units

    /// <summary>Puts a new regiment of the given battalions on the map, at full strength (tests and training).</summary>
    internal Unit AddRegiment(int ownerId, int provinceId, params BattalionType[] battalions)
    {
        var unit = AddUnit(ownerId, UnitType.Regiment, provinceId, 0, NextUnitNumber(ownerId, CommandLevels.Combat));
        foreach (var type in battalions) unit.Battalions.Add(new Battalion(type));
        return unit;
    }

    /// <summary>A new HQ, with a newly recruited general of the right rank at its head.</summary>
    internal Unit AddHeadquarters(int ownerId, int provinceId, int level)
    {
        var hq = AddUnit(ownerId, UnitType.Headquarters, provinceId, CommandLevels.Info(level).Staff, NextUnitNumber(ownerId, level), level);
        hq.Officer = NewOfficer(hq.Owner, hq.RequiredRank);
        return hq;
    }

    /// <summary>Units of each level are numbered in order for each nation: 1.er Regimiento, 2.º Regimiento…; corps I, II…</summary>
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

    /// <summary>
    /// Whether troops can be raised in a province of the player's: it needs a city or barracks (only the barracks
    /// train combat troops, see <see cref="CanRaiseTroops"/>).
    /// </summary>
    public CommandResult CanTrainIn(int playerId, Province p)
    {
        if (p.OwnerId != playerId) return CommandResult.Fail("La provincia no es tuya.");
        if (!p.CityId.HasValue && !p.Buildings.Contains(BuildingType.Barracks)) return CommandResult.Fail("Hace falta una ciudad o un cuartel en la provincia.");
        if (p.IsOccupied) return CommandResult.Fail("La provincia está ocupada por el enemigo.");
        return CommandResult.Success();
    }

    public CommandResult CanTrain(Province p, BattalionType type)
    {
        if (type.Info().Naval && !IsPort(p, p.OwnerId)) return CommandResult.Fail("Los barcos solo se construyen en ciudades con puerto.");
        if (type.Info().Shipyard is BuildingType yard && !p.Buildings.Contains(yard))
            return CommandResult.Fail($"Requiere {yard.Info().Name.ToLowerInvariant()} en la ciudad.");
        return CanRaiseTroops(p, type.Info().Men, type.Info().Cost, type.Info().Requires, type.NeedsBarracks());
    }

    /// <summary>
    /// Whether a province can raise troops: it has a city or barracks (<see cref="CanTrainIn"/>), the advances are
    /// known, it has barracks if they are combat troops (<paramref name="needsBarracks"/>), it has the men to spare
    /// (keeping a city's minimum, or a settled province's) and the nation can pay.
    /// </summary>
    private CommandResult CanRaiseTroops(Province p, int men, Economy.ResourceCost cost, IEnumerable<Tech> requires, bool needsBarracks)
    {
        var where = CanTrainIn(p.OwnerId, p);
        if (!where.Ok) return where;
        var player = Players[p.OwnerId];
        var missing = requires.Where(t => !player.Techs.Contains(t)).ToList();
        if (missing.Count > 0) return CommandResult.Fail("Requiere " + string.Join(" y ", missing.Select(t => t.Info().Name.ToLowerInvariant())) + ".");
        if (needsBarracks && !p.Buildings.Contains(BuildingType.Barracks)) return CommandResult.Fail("Requiere un cuartel en la provincia.");
        int keep = MinimumPopulation(p);
        if (p.Population - men < keep) return CommandResult.Fail($"Hacen falta {men + keep} habitantes.");
        if (!player.Stockpile.Has(cost)) return CommandResult.Fail($"Cuesta {cost}.");
        return CommandResult.Success();
    }

    /// <summary>People who stay when troops are raised: a city's minimum, or enough to keep a province without one settled.</summary>
    private static int MinimumPopulation(Province p) => p.CityId.HasValue ? GameRules.MinCityPopulation : GameRules.SettledPopulation;

    /// <summary>Pays for a battalion and takes its men from the province; it forms a new regiment there when trained.</summary>
    public CommandResult Train(int playerId, int provinceId, BattalionType type)
    {
        var p = Map.Provinces[provinceId];
        if (p.OwnerId != playerId) return CommandResult.Fail("La provincia no es tuya.");
        var check = CanTrain(p, type);
        if (!check.Ok) return check;
        var info = type.Info();
        Players[playerId].Stockpile.TrySpend(info.Cost);
        p.Population -= info.Men;
        int days = TrainingDays(Players[playerId], type);
        p.Training.Add(new TrainingOrder(type, days));
        return CommandResult.Success($"{info.Name} en instrucción: {days} días.");
    }

    /// <summary>How much faster the player trains a battalion: <see cref="MilitaryRules.TechTrainingSpeed"/> for each advance it knows that studies it.</summary>
    public static double TrainingSpeed(Player player, BattalionType type) =>
        MilitaryRules.TechTrainingSpeed * player.Techs.Count(t => t.Info().FasterTraining.Contains(type));

    /// <summary>Days a battalion takes the player to train: its normal days, fewer with the advances that study it.</summary>
    public static int TrainingDays(Player player, BattalionType type) =>
        (int)Math.Ceiling(type.Info().TrainingDays / (1 + TrainingSpeed(player, type)));

    /// <summary>Days a regiment of the template takes the player: its battalions train side by side, so the slowest sets the time.</summary>
    public static int TrainingDays(Player player, RegimentTemplate template) =>
        template.Battalions.Count == 0 ? 0 : template.Battalions.Max(b => TrainingDays(player, b));

    public CommandResult CanRaiseHeadquarters(Province p, int level)
    {
        var where = CanTrainIn(p.OwnerId, p);
        if (!where.Ok) return where;
        var info = CommandLevels.Info(level);
        int keep = MinimumPopulation(p);
        if (p.Population - info.Staff < keep) return CommandResult.Fail($"Hacen falta {info.Staff + keep} habitantes.");
        if (!Players[p.OwnerId].Stockpile.Has(info.Cost)) return CommandResult.Fail($"Cuesta {info.Cost}.");
        return CommandResult.Success();
    }

    /// <summary>Pays for an HQ of the given level (1 corps … 3 army group); its staff come from the province.</summary>
    public CommandResult RaiseHeadquarters(int playerId, int provinceId, int level)
    {
        var p = Map.Provinces[provinceId];
        if (p.OwnerId != playerId) return CommandResult.Fail("La provincia no es tuya.");
        var check = CanRaiseHeadquarters(p, level);
        if (!check.Ok) return check;
        var info = CommandLevels.Info(level);
        Players[playerId].Stockpile.TrySpend(info.Cost);
        p.Population -= info.Staff;
        p.Training.Add(new TrainingOrder(level));
        return CommandResult.Success($"Cuartel general de {Formations.LevelName(level).ToLowerInvariant()} en formación: {info.TrainingDays} días.");
    }

    /// <summary>Every order a province is training advances a day (not while occupied); finished ones appear in it.</summary>
    private void DailyTraining(Player player)
    {
        foreach (int id in player.Provinces)
        {
            var p = Map.Provinces[id];
            if (p.Training.Count == 0 || p.IsOccupied) continue;
            foreach (var order in p.Training.ToList())
            {
                if (--order.DaysLeft > 0) continue;
                p.Training.Remove(order);
                var unit = order.Battalion is BattalionType ship && ship.Info().Naval ? AddFleet(player.Id, id, ship)
                    : order.Battalion is BattalionType type ? AddRegiment(player.Id, id, type)
                    : order.TemplateBattalions.Count > 0 ? AddRegiment(player.Id, id, [.. order.TemplateBattalions])
                    : AddHeadquarters(player.Id, id, order.HeadquartersLevel);
                if (player.IsHuman) Notify(player.Id, $"Nueva unidad en {PlaceName(p)}: {unit.Name} ({order.Name.ToLowerInvariant()}).");
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
        if (template.Battalions.Count >= MilitaryRules.MaxBattalionsPerUnit)
            return CommandResult.Fail($"Como mucho {Formations.BattalionCount(MilitaryRules.MaxBattalionsPerUnit)} por unidad.");
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

    public CommandResult CanTrainTemplate(Province p, RegimentTemplate template) =>
        CanRaiseTroops(p, template.Men, template.Cost, template.Requires, template.NeedsBarracks);

    /// <summary>Pays for every battalion of a template at once; they train side by side and form one regiment.</summary>
    public CommandResult TrainTemplate(int playerId, int provinceId, int templateId)
    {
        var p = Map.Provinces[provinceId];
        if (p.OwnerId != playerId) return CommandResult.Fail("La provincia no es tuya.");
        if (TemplateById(Players[playerId], templateId) is not { } template) return CommandResult.Fail("Plantilla no válida.");
        var check = CanTrainTemplate(p, template);
        if (!check.Ok) return check;
        Players[playerId].Stockpile.TrySpend(template.Cost);
        p.Population -= template.Men;
        int days = TrainingDays(Players[playerId], template);
        p.Training.Add(new TrainingOrder(template, days));
        return CommandResult.Success($"{Formations.CombatName(template.Battalions.Count)} de la {template.Name} en instrucción: {days} días.");
    }

    // ------------------------------------------------------------------ organisation

    public CommandResult CanMerge(Unit unit, Unit other)
    {
        if (unit.Id == other.Id || unit.IsAboard || other.IsAboard || !(unit.IsMilitary && other.IsMilitary || unit.IsFleet && other.IsFleet))
            return CommandResult.Fail($"Solo se unen {Formations.CombatPlural} entre sí, o flotas entre sí.");
        if (unit.OwnerId != other.OwnerId || unit.ProvinceId != other.ProvinceId) return CommandResult.Fail("Deben estar en la misma provincia.");
        if (unit.AttackingProvinceId.HasValue || other.AttackingProvinceId.HasValue) return CommandResult.Fail("Una de ellas está atacando.");
        if (unit.IsFleet && unit.Battalions.Count + other.Battalions.Count > MilitaryRules.MaxShipsPerFleet)
            return CommandResult.Fail($"Como mucho {Formations.ShipCount(MilitaryRules.MaxShipsPerFleet)} por flota.");
        if (unit.IsMilitary && unit.Battalions.Count + other.Battalions.Count > MilitaryRules.MaxBattalionsPerUnit)
            return CommandResult.Fail($"Como mucho {Formations.BattalionCount(MilitaryRules.MaxBattalionsPerUnit)} por unidad.");
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
        // The unit keeps its own officer; the other's takes over only if it had none, or else goes to the reserve.
        if (unit.Officer == null)
        {
            unit.Officer = other.Officer;
            other.Officer = null;
        }
        RemoveUnit(other);
        unit.Path.Clear();
        unit.StepHours = unit.HoursToNext = 0;
        Promote(unit);
        return CommandResult.Success($"{other.Name} se une a {unit.Name}.");
    }

    /// <summary>One battalion leaves its regiment and forms a new one in the same province.</summary>
    public CommandResult Split(int playerId, int unitId, int battalionIndex) => Split(playerId, unitId, [battalionIndex]);

    /// <summary>
    /// The chosen battalions (or ships) leave their unit together and form a new one in the same province, with no
    /// officer; the officer stays with the unit they leave.
    /// </summary>
    public CommandResult Split(int playerId, int unitId, IReadOnlyCollection<int> battalionIndexes)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId || !(unit.IsMilitary || unit.IsFleet) || unit.IsAboard) return CommandResult.Fail("Unidad no válida.");
        var indexes = battalionIndexes.Distinct().OrderDescending().ToList();
        if (indexes.Count == 0) return CommandResult.Fail(unit.IsFleet ? "Elige qué barcos separar." : "Elige qué batallones separar.");
        if (indexes.Any(i => i < 0 || i >= unit.Battalions.Count)) return CommandResult.Fail("Tropa no válida.");
        if (indexes.Count >= unit.Battalions.Count) return CommandResult.Fail(unit.IsFleet ? "Debe quedar al menos un barco." : "Debe quedar al menos un batallón.");
        if (unit.AttackingProvinceId.HasValue) return CommandResult.Fail("Está atacando.");
        var leaving = indexes.Select(i => unit.Battalions[i]).Reverse().ToList();
        if (unit.IsFleet && CargoMen(unit) > unit.Capacity - leaving.Sum(b => b.Info.Capacity)) return CommandResult.Fail("La carga no cabría en el resto de la flota.");
        foreach (int i in indexes) unit.Battalions.RemoveAt(i);
        var split = AddUnit(playerId, unit.Type, unit.ProvinceId, 0, NextUnitNumber(playerId, unit.IsFleet ? FleetNumbering : CommandLevels.Combat));
        split.Battalions.AddRange(leaving);
        string what = leaving.Count == 1 ? Formations.BattalionName(leaving[0].Info) : unit.IsFleet ? $"{leaving.Count} barcos" : Formations.BattalionCount(leaving.Count);
        return CommandResult.Success($"{what} {(leaving.Count == 1 ? "forma" : "forman")} una unidad nueva: {split.Name}.");
    }

    /// <summary>Gives a unit a name of the player's choosing; an empty one brings back the automatic name.</summary>
    public CommandResult RenameUnit(int playerId, int unitId, string name)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId || unit.Type == UnitType.Settlers) return CommandResult.Fail("Unidad no válida.");
        name = name.Trim();
        if (name.Length > MilitaryRules.MaxUnitNameLength) return CommandResult.Fail($"Como mucho {MilitaryRules.MaxUnitNameLength} letras.");
        unit.CustomName = name.Length == 0 || name == unit.AutomaticName ? null : name;
        return CommandResult.Success(unit.CustomName == null ? $"Vuelve a llamarse {unit.Name}." : $"Ahora se llama {unit.Name}.");
    }

    // ------------------------------------------------------------------ officers

    public CommandResult CanRecruitOfficer(Player player) =>
        player.Stockpile[Economy.ResourceType.Gold] < MilitaryRules.OfficerCost
            ? CommandResult.Fail($"Hacen falta {MilitaryRules.OfficerCost:0} de oro.")
            : CommandResult.Success();

    /// <summary>A new officer, with random traits, joins the nation's reserve for gold.</summary>
    public CommandResult RecruitOfficer(int playerId)
    {
        var player = Players[playerId];
        var check = CanRecruitOfficer(player);
        if (!check.Ok) return check;
        player.Stockpile[Economy.ResourceType.Gold] -= MilitaryRules.OfficerCost;
        var officer = NewOfficer(player, OfficerRank.Colonel);
        player.OfficerReserve.Add(officer);
        return CommandResult.Success($"{officer.Title} se une a la reserva: {officer.Summary}.");
    }

    /// <summary>
    /// Puts an officer from the reserve at the head of a combat unit or HQ; whoever led it goes back to the reserve.
    /// The officer is promoted if the unit calls for a higher rank.
    /// </summary>
    public CommandResult AssignOfficer(int playerId, int unitId, int officerId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId || !unit.HasOfficer) return CommandResult.Fail("Unidad no válida.");
        var reserve = Players[playerId].OfficerReserve;
        if (reserve.FirstOrDefault(o => o.Id == officerId) is not { } officer) return CommandResult.Fail("Ese oficial no está en la reserva.");
        reserve.Remove(officer);
        if (unit.Officer is { } previous) reserve.Add(previous);
        unit.Officer = officer;
        Promote(unit);
        return CommandResult.Success($"{officer.Title} toma el mando de {unit.Name}.");
    }

    /// <summary>The unit's officer steps down and goes back to the reserve.</summary>
    public CommandResult RelieveOfficer(int playerId, int unitId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        if (unit.Officer is not { } officer) return CommandResult.Fail("No tiene oficial.");
        unit.Officer = null;
        Players[playerId].OfficerReserve.Add(officer);
        return CommandResult.Success($"{officer.Title} vuelve a la reserva.");
    }

    /// <summary>An officer in the reserve leaves the army for good.</summary>
    public CommandResult RetireOfficer(int playerId, int officerId)
    {
        var reserve = Players[playerId].OfficerReserve;
        if (reserve.FirstOrDefault(o => o.Id == officerId) is not { } officer) return CommandResult.Fail("Ese oficial no está en la reserva.");
        reserve.Remove(officer);
        return CommandResult.Success($"{officer.Title} se retira.");
    }

    /// <summary>A newly recruited officer whose name no other officer of the nation already has (if a few tries find one).</summary>
    private Officer NewOfficer(Player player, OfficerRank rank)
    {
        var taken = Units.Where(u => u.OwnerId == player.Id).Select(u => u.Officer?.Name).Concat(player.OfficerReserve.Select(o => o.Name)).ToHashSet();
        var officer = Officer.Recruit(_nextOfficerId++, _random, rank);
        for (int i = 0; i < 10 && taken.Contains(officer.Name); i++) officer = Officer.Recruit(officer.Id, _random, rank);
        return officer;
    }

    /// <summary>Raises the unit's officer to the rank its size calls for, if below it.</summary>
    private void Promote(Unit unit)
    {
        if (unit.Officer is not { } officer || officer.Rank >= unit.RequiredRank) return;
        officer.Rank = unit.RequiredRank;
        if (unit.OwnerId == HumanPlayerId) Notify(HumanPlayerId, $"{officer.Name} asciende a {Officer.RankName(officer.Rank).ToLowerInvariant()} al frente de {unit.Name}.");
    }

    public CommandResult CanAttach(Unit unit, Unit hq)
    {
        if (unit.CommandLevel < 0) return CommandResult.Fail("Esta unidad no forma parte de la cadena de mando.");
        if (!hq.IsHeadquarters || hq.OwnerId != unit.OwnerId) return CommandResult.Fail("Cuartel general no válido.");
        if (hq.HeadquartersLevel != unit.CommandLevel + 1)
            return CommandResult.Fail($"Esta unidad solo puede depender de un cuartel de {Formations.LevelName(unit.CommandLevel + 1).ToLowerInvariant()}.");
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
    /// Supply runs from the nation's cities along its roads and railways as far as they go through land it controls
    /// (or nobody owns) (<see cref="SupplyNetwork"/>), and from there through that land up to
    /// <see cref="MilitaryRules.SupplyRangeHours"/> away (sooner along roads), and one province beyond: the front line.
    /// </summary>
    private HashSet<int> ComputeSupply(Player player)
    {
        var cities = Cities.Where(c => c.OwnerId == player.Id && !Map.Provinces[c.ProvinceId].IsOccupied).Select(c => c.ProvinceId).ToList();
        var supplied = new HashSet<int>();
        if (cities.Count == 0) return supplied;
        var sources = SupplyNetwork(player.Id, cities);
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

    /// <summary>
    /// What a nation's forces cost each day, by resource: a share of what every battalion, ship and HQ cost to
    /// raise, gold and materials alike (wood only goes into building them), more or less as the officer leading it keeps it.
    /// </summary>
    public double[] Upkeep(Player player)
    {
        var upkeep = new double[Economy.Resources.All.Length];
        foreach (var unit in Units.Where(u => u.OwnerId == player.Id))
        {
            double factor = Math.Max(0, 1 + (unit.Officer?.UpkeepChange ?? 0));
            if (unit.IsHeadquarters) AddUpkeep(upkeep, CommandLevels.Info(unit.HeadquartersLevel).Cost, factor);
            foreach (var b in unit.Battalions) AddUpkeep(upkeep, b.Info.Cost, factor);
        }
        return upkeep;
    }

    /// <summary>What one battalion, ship or HQ of this cost adds to the daily upkeep, times <paramref name="factor"/>.</summary>
    public static void AddUpkeep(double[] upkeep, Economy.ResourceCost cost, double factor = 1)
    {
        foreach (var (type, amount) in cost.Items)
        {
            if (type == Economy.ResourceType.Wood) continue;
            upkeep[(int)type] += amount * factor * (type == Economy.ResourceType.Gold ? MilitaryRules.UpkeepGoldShare : MilitaryRules.UpkeepResourceShare);
        }
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
            // Unpaid troops lose heart and slip away, wherever they are, and nobody joins them.
            if (player.ArmyUnpaid)
            {
                foreach (var b in unit.Battalions)
                {
                    b.Organisation = Math.Max(0, b.Organisation - b.Info.MaxOrganisation * MilitaryRules.UnpaidOrganisationLoss);
                    b.Strength = Math.Max(0, b.Strength - b.Info.Men * MilitaryRules.UnpaidDesertion);
                }
                if (unit.Citizens >= 1) continue;
                if (unit.IsFleet) Sink(unit);
                else Destroy(unit, "se ha disuelto: nadie le pagaba", officerSurvives: true);
                continue;
            }
            // Troops aboard live off the ships' stores; fleets are repaired and crewed only in their ports.
            if (unit.IsAboard || unit.IsFleet && !IsPort(Map.Provinces[unit.ProvinceId], player.Id)) continue;
            if (unit.IsMilitary && !IsInSupply(unit))
            {
                foreach (var b in unit.Battalions)
                {
                    b.Organisation = Math.Max(0, b.Organisation - b.Info.MaxOrganisation * MilitaryRules.OutOfSupplyOrganisationLoss);
                    b.Strength = Math.Max(0, b.Strength - b.Info.Men * MilitaryRules.OutOfSupplyAttrition);
                }
                if (unit.Citizens < 1) Destroy(unit, "se ha dispersado sin suministro", officerSurvives: true);
                continue;
            }
            if (InBattle(unit)) continue;
            // A dry dock repairs a fleet faster: organisation and crews both.
            double repair = unit.IsFleet && Map.Provinces[unit.ProvinceId].Buildings.Contains(BuildingType.DryDock) ? MilitaryRules.DryDockRepair : 1;
            double recovery = MilitaryRules.OrganisationRecovery * Math.Max(0, 1 + CommandBonus(unit) + (GeneralOf(unit)?.RecoveryBonus ?? 0) + (unit.Officer?.RecoveryBonus ?? 0))
                              * (unit.IsMoving ? 0.5 : 1) * repair;
            foreach (var b in unit.Battalions)
            {
                b.Organisation = Math.Min(b.Info.MaxOrganisation, b.Organisation + b.Info.MaxOrganisation * recovery);
                double missing = b.Info.Men - b.Strength;
                if (missing <= 0 || capital == null) continue;
                double men = Math.Min(missing, Math.Min(b.Info.Men * MilitaryRules.ReinforcementRate * repair, capital.Population - GameRules.MinCityPopulation));
                if (men <= 0) continue;
                // Recruits are green: they water down the battalion's experience.
                b.Experience = b.Experience * b.Strength / (b.Strength + men);
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
    /// One hour of every battle. Each side puts its best battalions in the front line (as many as the
    /// terrain allows) with its artillery and aircraft behind; their fire wears down the enemy's engaged
    /// battalions, who gain experience. Broken defenders retreat (or are destroyed if surrounded) and broken
    /// attackers give up. When no defenders are left, the attackers march in.
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

            var attacking = Engage(attackers, province, attacking: true);
            var defending = Engage(defenders, province, attacking: false, HasEngineers(attackers));
            double attackFire = SideFire(attacking), defenseFire = SideFire(defending);
            battle.DefenderLosses += Damage(defending, attackFire);
            battle.AttackerLosses += Damage(attacking, defenseFire);
            battle.History.Add(new BattleHour(attackers.Sum(u => u.Citizens), defenders.Sum(u => u.Citizens),
                AverageOrganisation(attackers), AverageOrganisation(defenders), attackFire, defenseFire));
            foreach (var e in attacking.Concat(defending))
                e.Battalion.Experience += MilitaryRules.ExperiencePerBattleHour * (1 - e.Battalion.Experience);

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

    /// <summary>The organisation (0-1) of a side's units, on average; 0 when none are left.</summary>
    public static double AverageOrganisation(IReadOnlyCollection<Unit> units) => units.Count == 0 ? 0 : units.Average(u => u.OrganisationShare);

    private static bool Broken(Unit unit) => unit.OrganisationShare < MilitaryRules.BreakingOrganisation || unit.Citizens < 1;

    /// <summary>
    /// How much harder those defending a province hit: its terrain (less against <paramref name="engineers"/>), and its
    /// walls or castle.
    /// </summary>
    public static double DefenseMultiplier(Province province, bool engineers = false) =>
        MilitaryRules.DefenseMultiplier(province, engineers) * (1 + province.BuildingBonuses.Defense);

    /// <summary>Whether any of these units brings engineers, fighting or not.</summary>
    public static bool HasEngineers(IEnumerable<Unit> units) => units.Any(u => u.Battalions.Any(b => b.Type == BattalionType.Engineers));

    /// <summary>The general of a unit's own HQ, if it is within range to lead it (the unit's own officer is <see cref="Unit.Officer"/>).</summary>
    public Officer? GeneralOf(Unit unit) => InCommandRange(unit) ? CommanderOf(unit)!.Officer : null;

    /// <summary>A battalion in the fight: whose it is, the damage it deals this hour and its share of the enemy's.</summary>
    public readonly record struct Engaged(Unit Unit, Battalion Battalion, BattalionRole Role, double Fire, double Exposure);

    /// <summary>
    /// The battalions of one side that fight this hour: the strongest of the front-line troops, as many as
    /// the terrain's front holds, and up to half as many artillery, aircraft and engineers behind them. The rest wait
    /// in reserve. Each fires its attack or defence, scaled by its men, organisation and experience, the
    /// chain of command, its officer and its HQ's general, supply and, for defenders, the terrain and walls
    /// (the terrain counting less when <paramref name="enemyEngineers"/> come with the attack).
    /// </summary>
    public List<Engaged> Engage(List<Unit> units, Province province, bool attacking, bool enemyEngineers = false)
    {
        var all = new List<Engaged>();
        foreach (var unit in units)
        {
            double multiplier = Math.Max(0, 1 + CommandBonus(unit) + (GeneralOf(unit)?.FireBonus(attacking) ?? 0) + (unit.Officer?.FireBonus(attacking) ?? 0))
                                * (IsInSupply(unit) ? 1 : MilitaryRules.OutOfSupplyEfficiency)
                                * (attacking ? 1 : DefenseMultiplier(province, enemyEngineers));
            foreach (var b in unit.Battalions)
            {
                double value = attacking ? b.Info.Attack : b.Info.Defense;
                if (attacking && b.Info.Mounted && MilitaryRules.IsRough(province.Biome)) value *= MilitaryRules.MountedRoughTerrainAttack;
                double fire = value * b.StrengthShare * (0.5 + 0.5 * b.OrganisationShare) * (1 + MilitaryRules.ExperienceBonus * b.Experience) * multiplier;
                all.Add(new Engaged(unit, b, b.Type.Role(), fire, 1));
            }
        }
        int width = MilitaryRules.FrontWidth(province.Biome);
        bool Support(Engaged e) => e.Role is BattalionRole.Artillery or BattalionRole.Air or BattalionRole.Engineers;
        var front = all.Where(e => !Support(e)).OrderByDescending(e => e.Fire).Take(width);
        var behind = all.Where(Support).OrderByDescending(e => e.Fire).Take(width / 2).Select(e => e with { Exposure = MilitaryRules.SupportExposure });
        return [.. front, .. behind];
    }

    /// <summary>A side's fire this hour: its engaged battalions', more for mixing kinds of troops, and a little luck.</summary>
    private double SideFire(List<Engaged> side) =>
        ExpectedFire(side) * (1 + (_random.NextDouble() * 2 - 1) * MilitaryRules.CombatRandomness);

    /// <summary>A side's fire in an hour before luck: its engaged battalions', more for mixing kinds of troops.</summary>
    public static double ExpectedFire(List<Engaged> side) => side.Sum(e => e.Fire) * (1 + CombinedArms(side.Select(e => e.Role)));

    /// <summary>Extra fire for each kind of troop beyond the first among those fighting, up to a limit.</summary>
    public static double CombinedArms(IEnumerable<BattalionRole> roles) =>
        Math.Min(MilitaryRules.MaxCombinedArmsBonus, MilitaryRules.CombinedArmsBonus * Math.Max(0, roles.Where(r => r != BattalionRole.Naval).Distinct().Count() - 1));

    /// <summary>
    /// Spreads the enemy's fire over a side's engaged battalions as lost organisation and men: artillery and
    /// aircraft behind the line take a smaller share, and the officers leading them may spare or lose organisation.
    /// Returns the men lost.
    /// </summary>
    private double Damage(List<Engaged> side, double fire)
    {
        double exposure = side.Sum(e => e.Exposure);
        if (exposure <= 0) return 0;
        double lost = 0;
        foreach (var e in side)
        {
            double share = fire * e.Exposure / exposure;
            double loss = Math.Max(0, 1 + (GeneralOf(e.Unit)?.OrganisationLoss ?? 0) + (e.Unit.Officer?.OrganisationLoss ?? 0));
            e.Battalion.Organisation = Math.Max(0, e.Battalion.Organisation - share * MilitaryRules.OrganisationDamage * loss);
            double strength = Math.Max(0, e.Battalion.Strength - share * MilitaryRules.StrengthDamage);
            lost += e.Battalion.Strength - strength;
            e.Battalion.Strength = strength;
        }
        return lost;
    }

    /// <summary>Spreads a side's fire over the enemy battalions as lost organisation and men (battles at sea).</summary>
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
        battle.AttackersWon = attackersWon;
        battle.EndHours = Date.Hours;
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
        var winners = attackersWon ? attackers : EnemyRegimentsIn(battle.ProvinceId, battle.AttackerId).ToList();
        foreach (var officer in winners.Select(GeneralOf).Concat(winners.Select(u => u.Officer)).OfType<Officer>().Distinct()) officer.Victories++;

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

    /// <summary>
    /// The unit is lost. Its officer falls with it when it is wiped out in battle or sunk, but makes it back to the
    /// reserve when it just melts away (<paramref name="officerSurvives"/>).
    /// </summary>
    private void Destroy(Unit unit, string how, bool officerSurvives = false)
    {
        var officer = unit.Officer;
        RemoveUnit(unit, officerSurvives);
        if (unit.OwnerId != HumanPlayerId) return;
        Notify(HumanPlayerId, $"{unit.Name}: {how}." + (officer != null && !officerSurvives ? $" Ha caído el {char.ToLowerInvariant(officer.Title[0])}{officer.Title[1..]}." : ""));
    }
}
