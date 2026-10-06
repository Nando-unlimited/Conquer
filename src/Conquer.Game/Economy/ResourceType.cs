namespace Conquer.Game.Economy;

public enum ResourceType
{
    Food,
    Wood,
    Coal,
    Iron,
    Copper,
    Silicon,
    Oil,
    Aluminium,
    Rubber,
    Gold,
    Silver,
    // New ones go at the end: saved games keep stores and deposits by position.
    Stone,
    Sulfur,
    Saltpeter,
    Horses,
}

/// <summary>Where a day's resources came from or went to (<see cref="Entities.Player.LastDayFlows"/>).</summary>
public enum ResourceFlow
{
    /// <summary>What the provinces yield: harvests, wood, taxes and deposits.</summary>
    Production,
    /// <summary>Trade deals and tributes: what comes in (positive) or goes out (negative).</summary>
    Exchange,
    /// <summary>The food the people and the troops eat, and the share of the stored food that rots.</summary>
    Consumption,
    /// <summary>The army's upkeep.</summary>
    Upkeep,
    /// <summary>What the workshops and factories use to make equipment.</summary>
    Workshops,
}

public static class Resources
{
    public static readonly ResourceType[] All = Enum.GetValues<ResourceType>();

    /// <summary>Resources found as deposits in the ground (everything except food and wood, which come from the land itself).</summary>
    public static readonly ResourceType[] Deposits =
    [
        ResourceType.Coal, ResourceType.Iron, ResourceType.Copper, ResourceType.Silicon, ResourceType.Oil,
        ResourceType.Aluminium, ResourceType.Rubber, ResourceType.Gold, ResourceType.Silver, ResourceType.Stone, ResourceType.Sulfur,
        ResourceType.Saltpeter, ResourceType.Horses,
    ];

    /// <summary>
    /// Resources every nation knows from the start. The rest stay hidden, and cannot be mined,
    /// until an advance reveals them (<see cref="Science.TechInfo.Reveals"/>).
    /// </summary>
    public static readonly ResourceType[] KnownFromStart =
    [
        ResourceType.Food, ResourceType.Wood, ResourceType.Copper, ResourceType.Gold, ResourceType.Silver, ResourceType.Stone,
        ResourceType.Horses,
    ];

    /// <summary>Herds rather than pockets: their pastures never run out.</summary>
    public static bool IsRenewable(this ResourceType type) => type == ResourceType.Horses;

    public static string Name(this ResourceType type) => type switch
    {
        ResourceType.Food => "Comida",
        ResourceType.Wood => "Madera",
        ResourceType.Coal => "Carbón",
        ResourceType.Iron => "Hierro",
        ResourceType.Copper => "Cobre",
        ResourceType.Silicon => "Silicio",
        ResourceType.Oil => "Petróleo",
        ResourceType.Aluminium => "Aluminio",
        ResourceType.Rubber => "Caucho",
        ResourceType.Gold => "Oro",
        ResourceType.Silver => "Plata",
        ResourceType.Stone => "Piedra",
        ResourceType.Sulfur => "Azufre",
        ResourceType.Saltpeter => "Salitre",
        ResourceType.Horses => "Caballos",
        _ => type.ToString(),
    };
}

/// <summary>A national store of every resource.</summary>
public sealed class Stockpile
{
    private readonly double[] _amounts = new double[Resources.All.Length];

    public double this[ResourceType type]
    {
        get => _amounts[(int)type];
        set => _amounts[(int)type] = value;
    }

    public bool Has(ResourceCost cost) => cost.Items.All(i => this[i.Type] >= i.Amount);

    public bool TrySpend(ResourceCost cost)
    {
        if (!Has(cost)) return false;
        foreach (var (type, amount) in cost.Items) this[type] -= amount;
        return true;
    }
}

public readonly record struct ResourceCost(params (ResourceType Type, double Amount)[] Items)
{
    /// <summary>The same resources, <paramref name="times"/> over.</summary>
    public ResourceCost Times(double times) => new([.. Items.Select(i => (i.Type, i.Amount * times))]);

    public override string ToString() => string.Join(", ", Items.Select(i => $"{i.Amount:0} {i.Type.Name()}"));
}
