using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Science;

namespace Conquer.Game.Military;

/// <summary>
/// A regiment design, as in Hearts of Iron: which battalions it has (1 to
/// <see cref="MilitaryRules.MaxBattalionsPerUnit"/>). Cities train whole regiments from it. It names lines, not models:
/// what it costs and how it fights depend on the advances its nation knows (<c>known</c>).
/// </summary>
public sealed class RegimentTemplate
{
    public int Id { get; }
    /// <summary>Its number among the nation's templates; it names it ("Plantilla II").</summary>
    public int Number { get; }
    public List<BattalionType> Battalions { get; } = [];

    public RegimentTemplate(int id, int number, IEnumerable<BattalionType> battalions)
    {
        Id = id;
        Number = number;
        Battalions.AddRange(battalions);
    }

    public string Name => $"Plantilla {Formations.Roman(Number)}";

    /// <summary>The model of each of its battalions for a nation knowing these advances.</summary>
    public IEnumerable<BattalionInfo> Models(IReadOnlySet<Tech> known) => Battalions.Select(b => b.ModelFor(known));

    public int Men(IReadOnlySet<Tech> known) => Models(known).Sum(m => m.Men);
    /// <summary>The equipment its battalions need, by model: "200 armas de guerreros, 100 armas de arqueros".</summary>
    public string Equipment(IReadOnlySet<Tech> known) => string.Join(", ", Models(known).Where(m => m.NeedsEquipment).GroupBy(m => m.SupplyKey)
        .Select(g => g.First().PiecesText(g.Sum(m => m.Pieces))));
    /// <summary>What training all its battalions costs together: their gold (their equipment is made apart, see <see cref="Equipment"/>).</summary>
    public ResourceCost Cost(IReadOnlySet<Tech> known) => new(Models(known).SelectMany(m => m.TrainingCost.Items)
        .GroupBy(i => i.Type).Select(g => (g.Key, g.Sum(i => i.Amount))).OrderBy(i => i.Key).ToArray());
    /// <summary>The buildings its battalions train in (<see cref="Military.Battalions.TrainingBuilding"/>): barracks, a workshop, both or none.</summary>
    public IEnumerable<Buildings.BuildingType> TrainingBuildings(IReadOnlySet<Tech> known) =>
        Battalions.Select(b => b.ModelFor(known).TrainingBuilding(b)).OfType<Buildings.BuildingType>().Distinct().Order();
    /// <summary>Every advance it needs: those that open each of its lines.</summary>
    public IEnumerable<Tech> Requires => Battalions.SelectMany(b => b.First().Requires).Distinct();
    /// <summary>Its battalions train side by side, so the slowest sets the time; before advances (see <see cref="Simulation.GameSession.TrainingDays(Entities.Player, RegimentTemplate)"/>).</summary>
    public int TrainingDays(IReadOnlySet<Tech> known) => Battalions.Count == 0 ? 0 : Models(known).Max(m => m.TrainingDays);
    public double Attack(IReadOnlySet<Tech> known) => Models(known).Sum(m => m.Attack);
    public double Defense(IReadOnlySet<Tech> known) => Models(known).Sum(m => m.Defense);
    public double MaxOrganisation(IReadOnlySet<Tech> known) => Battalions.Count == 0 ? 0 : Models(known).Average(m => m.MaxOrganisation);
    public double Speed(IReadOnlySet<Tech> known) => Battalions.Count == 0 ? 0 : Models(known).Min(m => m.Speed);
    public bool AnyMounted(IReadOnlySet<Tech> known) => Models(known).Any(m => m.Mounted);

    /// <summary>"2 × Guerreros, 1 × Arqueros", by the models a nation knowing these advances would raise.</summary>
    public string Composition(IReadOnlySet<Tech> known) =>
        string.Join(", ", Battalions.GroupBy(b => b).Select(g => $"{g.Count()} × {g.Key.ModelFor(known).Name}"));
}
