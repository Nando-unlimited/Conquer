using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Science;

namespace Conquer.Game.Buildings;

/// <summary>The buildings a province can have, one of each. The order is the order they are listed in.</summary>
public enum BuildingType
{
    Farm,
    Sawmill,
    Mine,
    Temple,
    Library,
    Market,
    Aqueduct,
    HerbalistHut,
}

/// <param name="Days">Days of work to build it.</param>
/// <param name="RequiresTech">Advance the owner must know first, if any.</param>
/// <param name="CityOnly">Only a province with a city can build it.</param>
/// <param name="NeedsDeposit">Only a province with a deposit that is not exhausted can build it.</param>
/// <param name="Effects">What it improves in its own province.</param>
public sealed record BuildingInfo(
    string Name, string Description, ResourceCost Cost, int Days,
    Tech? RequiresTech, bool CityOnly, bool NeedsDeposit, Modifiers Effects);

public static class Buildings
{
    public static readonly BuildingType[] All = Enum.GetValues<BuildingType>();

    private static readonly Dictionary<BuildingType, BuildingInfo> Table = new()
    {
        [BuildingType.Farm] = new("Granja", "+25 % de comida en la provincia.",
            new ResourceCost((ResourceType.Wood, 40), (ResourceType.Gold, 10)), 20, null, false, false, new() { Food = 0.25 }),
        [BuildingType.Sawmill] = new("Aserradero", "+50 % de madera en la provincia.",
            new ResourceCost((ResourceType.Wood, 30), (ResourceType.Gold, 10)), 15, null, false, false, new() { Wood = 0.5 }),
        [BuildingType.Mine] = new("Mina", "+50 % de producción de los yacimientos de la provincia.",
            new ResourceCost((ResourceType.Wood, 60), (ResourceType.Gold, 20)), 30, Tech.Mining, false, true, new() { Deposits = 0.5 }),
        [BuildingType.Temple] = new("Templo", "+10 de humor en la provincia.",
            new ResourceCost((ResourceType.Wood, 50), (ResourceType.Gold, 30)), 30, Tech.Mythology, false, false, new() { Mood = 10 }),
        [BuildingType.Library] = new("Biblioteca", "+50 % de ciencia de la ciudad.",
            new ResourceCost((ResourceType.Wood, 60), (ResourceType.Gold, 40)), 40, Tech.Writing, true, false, new() { Science = 0.5 }),
        [BuildingType.Market] = new("Mercado", "+50 % de oro de los impuestos de la provincia.",
            new ResourceCost((ResourceType.Wood, 80), (ResourceType.Gold, 50)), 45, Tech.Currency, true, false, new() { Taxes = 0.5 }),
        [BuildingType.Aqueduct] = new("Acueducto", "La tierra de la provincia alimenta un 25 % más de gente.",
            new ResourceCost((ResourceType.Wood, 100), (ResourceType.Gold, 40)), 60, Tech.Irrigation, false, false, new() { Capacity = 0.25 }),
        [BuildingType.HerbalistHut] = new("Herbolario", "+20 % de fertilidad en la provincia.",
            new ResourceCost((ResourceType.Wood, 40), (ResourceType.Gold, 30)), 30, Tech.Medicine, false, false, new() { Fertility = 0.2 }),
    };

    public static BuildingInfo Info(this BuildingType type) => Table[type];
}
