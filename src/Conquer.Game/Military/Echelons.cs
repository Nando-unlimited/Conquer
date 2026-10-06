namespace Conquer.Game.Military;

/// <summary>
/// The size of a combat unit, which goes about the map as one: a regiment of up to
/// <see cref="Rules.MilitaryRules.MaxBattalionsPerRegiment"/> battalions, a brigade of up to
/// <see cref="Rules.MilitaryRules.MaxRegimentsPerBrigade"/> regiments, or a division of brigades and regiments
/// (up to <see cref="Rules.MilitaryRules.MaxDivisionParts"/> of them and <see cref="Rules.MilitaryRules.MaxDivisionMen"/> men).
/// </summary>
public enum Echelon
{
    Regiment,
    Brigade,
    Division,
}

/// <summary>
/// A regiment inside a combat unit: its own number and name, which it takes back when it leaves to go about on its
/// own, and its battalions.
/// </summary>
public sealed class Regiment(int number, string? customName = null)
{
    public int Number { get; set; } = number;
    public string? CustomName { get; set; } = customName;
    public List<Battalion> Battalions { get; } = [];

    public string Name => CustomName ?? Formations.CombatUnitName(Number, Echelon.Regiment);
    /// <summary>Its full complement: the men of all its battalions at full strength.</summary>
    public int Men => Battalions.Sum(b => b.Info.Men);
}

/// <summary>A brigade inside a division: its own number and name, and its regiments.</summary>
public sealed class Brigade(int number, string? customName = null)
{
    public int Number { get; set; } = number;
    public string? CustomName { get; set; } = customName;
    public List<Regiment> Regiments { get; } = [];

    public string Name => CustomName ?? Formations.CombatUnitName(Number, Echelon.Brigade);
    public IEnumerable<Battalion> Battalions => Regiments.SelectMany(r => r.Battalions);
    public int Men => Regiments.Sum(r => r.Men);
}
