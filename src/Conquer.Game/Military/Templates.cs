using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Science;

namespace Conquer.Game.Military;

/// <summary>
/// A regiment design, as in Hearts of Iron: which battalions it has (1 to
/// <see cref="MilitaryRules.MaxBattalionsPerUnit"/>). Cities train whole regiments from it.
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

    public int Men => Battalions.Sum(b => b.Info().Men);
    /// <summary>What all its battalions cost together.</summary>
    public ResourceCost Cost => new(Battalions.SelectMany(b => b.Info().Cost.Items)
        .GroupBy(i => i.Type).Select(g => (g.Key, g.Sum(i => i.Amount))).OrderBy(i => i.Key).ToArray());
    /// <summary>Its battalions train side by side, so the slowest sets the time; before advances (see <see cref="Simulation.GameSession.TrainingDays(Entities.Player, RegimentTemplate)"/>).</summary>
    public int TrainingDays => Battalions.Count == 0 ? 0 : Battalions.Max(b => b.Info().TrainingDays);
    /// <summary>Whether any of its battalions trains only in barracks.</summary>
    public bool NeedsBarracks => Battalions.Any(b => b.NeedsBarracks());
    /// <summary>Every advance any of its battalions needs.</summary>
    public IEnumerable<Tech> Requires => Battalions.SelectMany(b => b.Info().Requires).Distinct();
    public double Attack => Battalions.Sum(b => b.Info().Attack);
    public double Defense => Battalions.Sum(b => b.Info().Defense);
    public double MaxOrganisation => Battalions.Count == 0 ? 0 : Battalions.Average(b => b.Info().MaxOrganisation);
    public double Speed => Battalions.Count == 0 ? 0 : Battalions.Min(b => b.Info().Speed);
    public bool AnyMounted => Battalions.Any(b => b.Info().Mounted);

    /// <summary>"2 × Guerreros, 1 × Arqueros".</summary>
    public string Composition => string.Join(", ", Battalions.GroupBy(b => b).Select(g => $"{g.Count()} × {g.Key.Info().Name}"));
}
