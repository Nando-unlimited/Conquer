using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Science;

namespace Conquer.Game.Buildings;

/// <summary>
/// The buildings a province can have, one of each. The order is the order they are listed in. Farms,
/// granaries, sawmills, mines, roads and railways go anywhere; the rest need a city. Roads and railways are
/// built only by engineers, also in enemy land the nation occupies.
/// </summary>
public enum BuildingType
{
    Farm,
    Granary,
    Sawmill,
    Mine,
    Temple,
    Library,
    Market,
    Aqueduct,
    HerbalistHut,
    Amphitheatre,
    Walls,
    Road,
    University,
    Bank,
    Castle,
    Factory,
    Hospital,
    Railway,
    PowerPlant,
    Port,
    DryDock,
}

/// <param name="Days">Days of work to build it.</param>
/// <param name="RequiresTech">Advance the owner must know first, if any.</param>
/// <param name="CityOnly">Only a province with a city can build it.</param>
/// <param name="NeedsDeposit">Only a province with a deposit that is not exhausted can build it.</param>
/// <param name="Effects">What it improves in its own province.</param>
/// <param name="NeedsCoast">Only a province next to the sea or a lake can build it.</param>
/// <param name="RequiresBuilding">Another building the province must have first.</param>
/// <param name="NeedsEngineers">
/// Built by engineers: only where the nation has some (see <see cref="Rules.MilitaryRules.EngineerWorkDays"/>), in its own
/// land or land it occupies, however few people live there; the work stops while none are there.
/// </param>
public sealed record BuildingInfo(
    string Name, string Description, ResourceCost Cost, int Days,
    Tech? RequiresTech, bool CityOnly, bool NeedsDeposit, Modifiers Effects, bool NeedsCoast = false, BuildingType? RequiresBuilding = null,
    bool NeedsEngineers = false);

public static class Buildings
{
    public static readonly BuildingType[] All = Enum.GetValues<BuildingType>();

    private static readonly Dictionary<BuildingType, BuildingInfo> Table = new()
    {
        [BuildingType.Farm] = new("Granja", "+25 % de comida en la provincia.",
            new ResourceCost((ResourceType.Wood, 40), (ResourceType.Gold, 10)), 20, null, false, false, new() { Food = 0.25 }),
        [BuildingType.Granary] = new("Granero", "El hambre mata a la mitad de gente en la provincia.",
            new ResourceCost((ResourceType.Wood, 50), (ResourceType.Gold, 15)), 25, Tech.Pottery, false, false, new() { FamineSurvival = 0.5 }),
        [BuildingType.Sawmill] = new("Aserradero", "+50 % de madera en la provincia.",
            new ResourceCost((ResourceType.Wood, 30), (ResourceType.Gold, 10)), 15, null, false, false, new() { Wood = 0.5 }),
        [BuildingType.Mine] = new("Mina", "+50 % de producción de los yacimientos de la provincia.",
            new ResourceCost((ResourceType.Wood, 60), (ResourceType.Gold, 20)), 30, Tech.Mining, false, true, new() { Deposits = 0.5 }),
        [BuildingType.Temple] = new("Templo", "+10 de humor en la provincia.",
            new ResourceCost((ResourceType.Wood, 50), (ResourceType.Gold, 30)), 30, Tech.Mythology, true, false, new() { Mood = 10 }),
        [BuildingType.Library] = new("Biblioteca", "+50 % de ciencia de la ciudad.",
            new ResourceCost((ResourceType.Wood, 60), (ResourceType.Gold, 40)), 40, Tech.Writing, true, false, new() { Science = 0.5 }),
        [BuildingType.Market] = new("Mercado", "+50 % de oro de los impuestos de la provincia.",
            new ResourceCost((ResourceType.Wood, 80), (ResourceType.Gold, 50)), 45, Tech.Currency, true, false, new() { Taxes = 0.5 }),
        [BuildingType.Aqueduct] = new("Acueducto", "La tierra de la provincia alimenta un 25 % más de gente.",
            new ResourceCost((ResourceType.Wood, 100), (ResourceType.Gold, 40)), 60, Tech.Irrigation, true, false, new() { Capacity = 0.25 }),
        [BuildingType.HerbalistHut] = new("Herbolario", "+20 % de fertilidad en la provincia.",
            new ResourceCost((ResourceType.Wood, 40), (ResourceType.Gold, 30)), 30, Tech.Medicine, true, false, new() { Fertility = 0.2 }),
        [BuildingType.Amphitheatre] = new("Anfiteatro", "+10 de humor en la provincia: juegos y espectáculos.",
            new ResourceCost((ResourceType.Wood, 120), (ResourceType.Gold, 60)), 60, Tech.Construction, true, false, new() { Mood = 10 }),
        [BuildingType.Walls] = new("Muralla", "Quien defiende la provincia hace un 50 % más de daño.",
            new ResourceCost((ResourceType.Wood, 150), (ResourceType.Gold, 50)), 90, Tech.Fortifications, true, false, new() { Defense = 0.5 }),
        [BuildingType.Road] = new("Calzada", "La provincia se cruza un 50 % más deprisa. La construyen tus ingenieros.",
            new ResourceCost((ResourceType.Wood, 80), (ResourceType.Gold, 30)), 40, Tech.Engineering, false, false, new() { MoveSpeed = 0.5 }, NeedsEngineers: true),
        [BuildingType.University] = new("Universidad", "+50 % de ciencia de la ciudad.",
            new ResourceCost((ResourceType.Wood, 150), (ResourceType.Gold, 120)), 90, Tech.Education, true, false, new() { Science = 0.5 }),
        [BuildingType.Bank] = new("Banco", "+50 % de oro de los impuestos de la provincia.",
            new ResourceCost((ResourceType.Wood, 100), (ResourceType.Gold, 150)), 80, Tech.Banking, true, false, new() { Taxes = 0.5 }),
        [BuildingType.Castle] = new("Castillo", "Quien defiende la provincia hace el doble de daño.",
            new ResourceCost((ResourceType.Wood, 250), (ResourceType.Gold, 100), (ResourceType.Iron, 20)), 150, Tech.Castles, true, false, new() { Defense = 1 }),
        [BuildingType.Factory] = new("Fábrica", "+50 % de madera y de yacimientos en la provincia.",
            new ResourceCost((ResourceType.Wood, 200), (ResourceType.Gold, 200), (ResourceType.Iron, 50), (ResourceType.Coal, 50)), 120, Tech.Industrialization, true, false,
            new() { Wood = 0.5, Deposits = 0.5 }),
        [BuildingType.Hospital] = new("Hospital", "+20 % de fertilidad y la tierra alimenta un 10 % más de gente en la provincia.",
            new ResourceCost((ResourceType.Wood, 150), (ResourceType.Gold, 150)), 90, Tech.Sanitation, true, false, new() { Fertility = 0.2, Capacity = 0.1 }),
        [BuildingType.Railway] = new("Ferrocarril", "La provincia se cruza el doble de deprisa. Lo construyen tus ingenieros.",
            new ResourceCost((ResourceType.Wood, 100), (ResourceType.Gold, 80), (ResourceType.Iron, 60), (ResourceType.Coal, 20)), 60, Tech.Railroad, false, false,
            new() { MoveSpeed = 1 }, NeedsEngineers: true),
        [BuildingType.PowerPlant] = new("Central eléctrica", "+25 % de ciencia y de impuestos en la provincia.",
            new ResourceCost((ResourceType.Wood, 100), (ResourceType.Gold, 250), (ResourceType.Iron, 80), (ResourceType.Coal, 80)), 120, Tech.Electricity, true, false,
            new() { Science = 0.25, Taxes = 0.25 }),
        [BuildingType.Port] = new("Puerto", "Construye y repara barcos. +15 % de impuestos por el comercio marítimo.",
            new ResourceCost((ResourceType.Wood, 120), (ResourceType.Gold, 60)), 60, Tech.Navigation, true, false, new() { Taxes = 0.15 }, NeedsCoast: true),
        [BuildingType.DryDock] = new("Dique seco", "Construye los barcos más avanzados y repara las flotas el doble de rápido.",
            new ResourceCost((ResourceType.Gold, 300), (ResourceType.Iron, 150), (ResourceType.Coal, 50)), 120, Tech.NavalEngineering, true, false, Modifiers.None,
            NeedsCoast: true, RequiresBuilding: BuildingType.Port),
    };

    public static BuildingInfo Info(this BuildingType type) => Table[type];
}
