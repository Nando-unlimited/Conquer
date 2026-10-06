using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Science;

namespace Conquer.Game.Buildings;

/// <summary>
/// The buildings a province can have, one of each. The order is the order they are listed in. Farms,
/// granaries, sawmills, mines, barracks, workshops and factories go anywhere; the rest need a city. Only provinces with barracks train combat
/// troops, and only those with a workshop (or the factory it becomes) build war machines. Roads and railways are not buildings but
/// links between provinces (<see cref="Simulation.RoadNetwork"/>).
/// </summary>
public enum BuildingType
{
    Farm,
    Granary,
    Sawmill,
    Mine,
    /// <summary>Trains combat battalions, in a city or in a province without one; a city without barracks only trains scouts, engineers, HQs and (in port) ships.</summary>
    Barracks,
    Temple,
    Library,
    Market,
    Aqueduct,
    HerbalistHut,
    Amphitheatre,
    Walls,
    /// <summary>Only in saves from before 1.36.0, when roads were buildings: loaded as links between neighbours that both had one.</summary>
    Road,
    University,
    Bank,
    Castle,
    Factory,
    Hospital,
    /// <summary>Only in older saves, like <see cref="Road"/>.</summary>
    Railway,
    PowerPlant,
    Port,
    DryDock,
    /// <summary>Builds the war machines (<see cref="Military.Battalions.TrainingBuilding"/>); it becomes a <see cref="Factory"/> once its owner knows industrialisation.</summary>
    Workshop,
}

/// <param name="Days">Days of work to build it.</param>
/// <param name="RequiresTech">Advance the owner must know first, if any.</param>
/// <param name="CityOnly">Only a province with a city can build it.</param>
/// <param name="NeedsDeposit">Only a province with a deposit that is not exhausted can build it.</param>
/// <param name="Effects">What it improves in its own province.</param>
/// <param name="NeedsCoast">Only a province next to the sea or a lake can build it.</param>
/// <param name="RequiresBuilding">Another building the province must have first.</param>
/// <param name="BecomesWith">The building it turns into once its owner knows that one's advance; from then on that one is built instead.</param>
public sealed record BuildingInfo(
    string Name, string Description, ResourceCost Cost, int Days,
    Tech? RequiresTech, bool CityOnly, bool NeedsDeposit, Modifiers Effects, bool NeedsCoast = false, BuildingType? RequiresBuilding = null,
    BuildingType? BecomesWith = null)
{
    /// <summary>Its name with the indefinite article: «un cuartel», «una fábrica».</summary>
    public string WithArticle => (Name.EndsWith('a') ? "una " : "un ") + Name.ToLowerInvariant();
}

public static class Buildings
{
    public static readonly BuildingType[] All = [.. Enum.GetValues<BuildingType>().Where(t => t is not (BuildingType.Road or BuildingType.Railway))];

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
        [BuildingType.Barracks] = new("Cuartel", "Instruye las tropas de combate; tus avances militares lo hacen más rápido.",
            new ResourceCost((ResourceType.Wood, 60), (ResourceType.Gold, 30)), 30, null, false, false, Modifiers.None),
        [BuildingType.Temple] = new("Templo", "+10 de moral en la provincia.",
            new ResourceCost((ResourceType.Wood, 50), (ResourceType.Gold, 30)), 30, Tech.Mythology, true, false, new() { Mood = 10 }),
        [BuildingType.Library] = new("Biblioteca", "+50 % de ciencia de la ciudad.",
            new ResourceCost((ResourceType.Wood, 60), (ResourceType.Gold, 40)), 40, Tech.Writing, true, false, new() { Science = 0.5 }),
        [BuildingType.Market] = new("Mercado", "+50 % de oro de los impuestos de la provincia.",
            new ResourceCost((ResourceType.Wood, 80), (ResourceType.Gold, 50)), 45, Tech.Currency, true, false, new() { Taxes = 0.5 }),
        [BuildingType.Aqueduct] = new("Acueducto", "La tierra de la provincia alimenta un 25 % más de gente.",
            new ResourceCost((ResourceType.Wood, 100), (ResourceType.Gold, 40)), 60, Tech.Irrigation, true, false, new() { Capacity = 0.25 }),
        [BuildingType.HerbalistHut] = new("Herbolario", "+20 % de fertilidad en la provincia; las epidemias matan un 20 % menos.",
            new ResourceCost((ResourceType.Wood, 40), (ResourceType.Gold, 30)), 30, Tech.Medicine, true, false, new() { Fertility = 0.2, PlagueResistance = 0.2 }),
        [BuildingType.Amphitheatre] = new("Anfiteatro", "+10 de moral en la provincia: juegos y espectáculos.",
            new ResourceCost((ResourceType.Wood, 120), (ResourceType.Gold, 60)), 60, Tech.Construction, true, false, new() { Mood = 10 }),
        [BuildingType.Walls] = new("Muralla", "Quien defiende la provincia hace un 50 % más de daño, y el enemigo tiene que sitiarla 30 días para tomarla.",
            new ResourceCost((ResourceType.Wood, 150), (ResourceType.Gold, 50)), 90, Tech.Fortifications, true, false, new() { Defense = 0.5 }),
        [BuildingType.University] = new("Universidad", "+50 % de ciencia de la ciudad.",
            new ResourceCost((ResourceType.Wood, 150), (ResourceType.Gold, 120)), 90, Tech.Education, true, false, new() { Science = 0.5 }),
        [BuildingType.Bank] = new("Banco", "+50 % de oro de los impuestos de la provincia.",
            new ResourceCost((ResourceType.Wood, 100), (ResourceType.Gold, 150)), 80, Tech.Banking, true, false, new() { Taxes = 0.5 }),
        [BuildingType.Castle] = new("Castillo", "Quien defiende la provincia hace el doble de daño, y el enemigo tiene que sitiarla 60 días para tomarla.",
            new ResourceCost((ResourceType.Wood, 250), (ResourceType.Gold, 100), (ResourceType.Iron, 20)), 150, Tech.Castles, true, false, new() { Defense = 1 }),
        [BuildingType.Factory] = new("Fábrica", "Fabrica equipo al doble que un taller y construye las máquinas de guerra. +50 % de madera y de yacimientos en la provincia.",
            new ResourceCost((ResourceType.Wood, 200), (ResourceType.Gold, 200), (ResourceType.Iron, 50), (ResourceType.Coal, 50)), 120, Tech.Industrialization, false, false,
            new() { Wood = 0.5, Deposits = 0.5 }),
        [BuildingType.Hospital] = new("Hospital", "+20 % de fertilidad, la tierra alimenta un 10 % más y las epidemias matan un 40 % menos.",
            new ResourceCost((ResourceType.Wood, 150), (ResourceType.Gold, 150)), 90, Tech.Sanitation, true, false, new() { Fertility = 0.2, Capacity = 0.1, PlagueResistance = 0.4 }),
        [BuildingType.PowerPlant] = new("Central eléctrica", "+25 % de ciencia y de impuestos en la provincia.",
            new ResourceCost((ResourceType.Wood, 100), (ResourceType.Gold, 250), (ResourceType.Iron, 80), (ResourceType.Coal, 80)), 120, Tech.Electricity, true, false,
            new() { Science = 0.25, Taxes = 0.25 }),
        [BuildingType.Port] = new("Puerto", "Construye y repara barcos. +15 % de impuestos por el comercio marítimo.",
            new ResourceCost((ResourceType.Wood, 120), (ResourceType.Gold, 60)), 60, Tech.Navigation, true, false, new() { Taxes = 0.15 }, NeedsCoast: true),
        [BuildingType.DryDock] = new("Dique seco", "Construye los barcos más avanzados y repara las flotas el doble de rápido.",
            new ResourceCost((ResourceType.Gold, 300), (ResourceType.Iron, 150), (ResourceType.Coal, 50)), 120, Tech.NavalEngineering, true, false, Modifiers.None,
            NeedsCoast: true, RequiresBuilding: BuildingType.Port),
        [BuildingType.Workshop] = new("Taller", "Fabrica el equipo de las tropas (armas, caballos, catapultas, cañones...) y construye las máquinas de guerra. Con la industrialización pasa a ser una fábrica.",
            new ResourceCost((ResourceType.Wood, 80), (ResourceType.Gold, 40)), 40, null, false, false, Modifiers.None, BecomesWith: BuildingType.Factory),
    };

    public static BuildingInfo Info(this BuildingType type) => Table[type];

    /// <summary>What the building is for this player: the one it turns into (<see cref="BuildingInfo.BecomesWith"/>) once they know how, or itself.</summary>
    public static BuildingType For(this BuildingType type, Entities.Player player) =>
        type.Info().BecomesWith is BuildingType next && next.Info().RequiresTech is Tech tech && player.Techs.Contains(tech) ? next : type;
}
