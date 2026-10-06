using Conquer.Game.Buildings;
using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Equipment, as in Hearts of Iron: workshops and factories each make the pieces of one battalion model (weapons,
/// horses, catapults, tanks...) for the resources of its cost but the gold, into the nation's stockpile. Battalions
/// take them from there to be trained, to replace their losses and to take up a newer model.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>Whether the province has a workshop or the factory it becomes, which can make equipment.</summary>
    public static bool HasWorkshop(Province p) => p.Has(BuildingType.Workshop) || p.Has(BuildingType.Factory);

    /// <summary>
    /// The models whose equipment the nation can make: the newest it knows of each line, ships aside, and the shared
    /// supplies once (as the scouts', which every nation knows).
    /// </summary>
    public static IReadOnlyList<(BattalionType Type, BattalionInfo Model)> ProducibleModels(Player player) =>
        [.. Battalions.All.Where(t => t.BestModel(player.Techs) >= 0 && !t.Redundant(player.Techs))
            .Select(t => (t, t.ModelFor(player.Techs))).Where(x => x.Item2.NeedsEquipment).DistinctBy(x => x.Item2.SupplyKey)];

    /// <summary>Pieces a day the province would make of the model: a battalion's worth in its training days, twice that in a factory.</summary>
    public static double ProductionRate(Province p, BattalionInfo model) =>
        !model.NeedsEquipment || !HasWorkshop(p) ? 0
        : model.Pieces / (double)model.TrainingDays * (p.Has(BuildingType.Factory) ? MilitaryRules.FactoryOutput : 1);

    /// <summary>What the province's line makes, if anything.</summary>
    public static BattalionInfo? ProductionOf(Province p) => Battalions.ModelByKey(p.Production);

    public CommandResult CanProduce(Province p, BattalionInfo model)
    {
        if (!HasWorkshop(p)) return CommandResult.Fail($"Hace falta un taller en la provincia.");
        if (p.IsOccupied) return CommandResult.Fail("La provincia está ocupada por el enemigo.");
        if (!model.NeedsEquipment) return CommandResult.Fail("Los barcos se construyen enteros en los puertos.");
        var player = Players[p.OwnerId];
        var missing = model.Requires.Where(t => !player.Techs.Contains(t)).ToList();
        if (missing.Count > 0) return CommandResult.Fail("Requiere " + string.Join(" y ", missing.Select(t => Science.Techs.Info(t).Name.ToLowerInvariant())) + ".");
        return CommandResult.Success();
    }

    /// <summary>Sets what the province's workshop or factory makes; null stops it.</summary>
    public CommandResult SetProduction(int playerId, int provinceId, string? modelKey)
    {
        var p = Map.Provinces[provinceId];
        if (p.OwnerId != playerId) return CommandResult.Fail("La provincia no es tuya.");
        if (modelKey == null)
        {
            p.Production = null;
            return CommandResult.Success($"El taller de {PlaceName(p)} para.");
        }
        if (Battalions.ModelByKey(modelKey) is not { } model) return CommandResult.Fail("Suministro no válido.");
        var check = CanProduce(p, model);
        if (!check.Ok) return check;
        p.Production = modelKey;
        return CommandResult.Success($"{PlaceName(p)} fabrica {model.SupplyName.ToLowerInvariant()}: {ProductionRate(p, model):0.#} al día.");
    }

    /// <summary>
    /// Each workshop and factory makes its day's pieces, as many as the nation's stores pay for (what it cannot pay it
    /// does not make), unless its province is occupied or its model is no longer known.
    /// </summary>
    private void DailyProduction(Player player)
    {
        foreach (int id in player.Provinces)
        {
            var p = Map.Provinces[id];
            if (ProductionOf(p) is not { } model || p.IsOccupied || !CanProduce(p, model).Ok) continue;
            double pieces = ProductionRate(p, model);
            var cost = model.EquipmentCost;
            // The share of the day's work the stores can pay for.
            double share = cost.Items.Length == 0 ? 1 : cost.Items.Min(i => Math.Min(1, player.Stockpile[i.Type] / (i.Amount * pieces / model.Pieces)));
            if (share <= 0) continue;
            foreach (var (type, amount) in cost.Items)
            {
                double used = amount * pieces / model.Pieces * share;
                player.Stockpile[type] -= used;
                player.LastDayNet[(int)type] -= used;
                player.Record(ResourceFlow.Workshops, type, -used);
            }
            player.AddEquipment(model, pieces * share);
        }
    }

    /// <summary>
    /// The model a battalion of the line would be trained with now: the newest the nation knows that it has the equipment
    /// for (<paramref name="count"/> battalions' worth); null if it has none.
    /// </summary>
    public static int? StockedModel(Player player, BattalionType type, int count = 1)
    {
        var models = type.Models();
        for (int i = type.BestModel(player.Techs); i >= 0; i--)
            if (!models[i].NeedsEquipment || player.EquipmentOf(models[i]) >= models[i].Pieces * count) return i;
        return null;
    }
}
