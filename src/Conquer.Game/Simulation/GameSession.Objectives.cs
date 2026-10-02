using Conquer.Game.Economy;
using Conquer.Game.Rules;

namespace Conquer.Game.Simulation;

/// <summary>The first steps a new ruler is guided through, in order.</summary>
public enum Objective
{
    Capital,
    Research,
    Work,
    Regiment,
    Claim,
    Tech,
    SecondCity,
    People,
    Agreement,
}

/// <summary>
/// Objectives: a guided start for the human. Each is checked every hour and, once met, stays met and pays
/// <see cref="GameRules.ObjectiveGold"/> gold. They can be met in any order; the first one left is the one shown.
/// </summary>
public sealed partial class GameSession
{
    public static readonly Objective[] AllObjectives = Enum.GetValues<Objective>();

    private readonly HashSet<Objective> _objectivesDone = [];

    public bool ObjectiveDone(Objective o) => _objectivesDone.Contains(o);

    public int ObjectivesDone => _objectivesDone.Count;

    /// <summary>The first objective not met yet, or null when all are.</summary>
    public Objective? CurrentObjective => AllObjectives.Where(o => !ObjectiveDone(o)).Select(o => (Objective?)o).FirstOrDefault();

    public static string ObjectiveTitle(Objective o) => o switch
    {
        Objective.Capital => "Funda tu capital",
        Objective.Research => "Elige qué investigar",
        Objective.Work => "Pon en marcha una obra",
        Objective.Regiment => "Entrena un regimiento",
        Objective.Claim => "Reclama una provincia",
        Objective.Tech => "Descubre un avance",
        Objective.SecondCity => "Funda una segunda ciudad",
        Objective.People => $"Llega a {GameRules.ObjectivePeople:#,0} habitantes",
        _ => "Firma un acuerdo con otra nación",
    };

    public static string ObjectiveHint(Objective o) => o switch
    {
        Objective.Capital => "Selecciona a tus colonos, llévalos a una tierra fértil (mejor junto a un río) y pulsa «Fundar ciudad».",
        Objective.Research => "Abre la nación (N), pestaña Ciencia, y elige un avance en cada rama.",
        Objective.Work => "Selecciona tu capital y, en la pestaña Edificios, construye una granja o un aserradero.",
        Objective.Regiment => "En la pestaña Ejército de tu capital, entrena exploradores: sirven para reclamar tierras.",
        Objective.Claim => "Lleva un regimiento con exploradores a una provincia sin dueño y pulsa «Reclamar».",
        Objective.Tech => "Espera a que tus ciudades terminen de investigar algo. Más gente y universidades dan más ciencia.",
        Objective.SecondCity => "Envía colonos desde tu capital (pestaña General) a una provincia tuya y funda allí otra ciudad.",
        Objective.People => "Las ciudades crecen con comida, buena moral y fertilidad. Reclama tierras para que tengan sitio.",
        _ => "En la pestaña Diplomacia, propón un pacto o una alianza, o firma un acuerdo en la pestaña Comercio.",
    };

    /// <summary>Whether the human meets the objective right now.</summary>
    private bool Met(Objective o)
    {
        var human = Human;
        return o switch
        {
            Objective.Capital => human.CapitalCityId.HasValue,
            Objective.Research => human.Researching.Any(t => t.HasValue),
            Objective.Work => human.Provinces.Any(id => Map.Provinces[id].Constructing.HasValue),
            Objective.Regiment => Units.Any(u => u.OwnerId == human.Id && u.IsMilitary),
            Objective.Claim => human.Provinces.Count >= 2,
            Objective.Tech => human.Techs.Count > 0,
            Objective.SecondCity => Cities.Count(c => c.OwnerId == human.Id) >= 2,
            Objective.People => human.Provinces.Sum(id => Map.Provinces[id].Population) >= GameRules.ObjectivePeople,
            _ => Players.Any(p => p.Id != human.Id && (AreAllied(human.Id, p.Id) || HavePact(human.Id, p.Id) || GivesAccess(p.Id, human.Id)
                                                       || GivesAccess(human.Id, p.Id))) || TradesOf(human.Id).Any(),
        };
    }

    /// <summary>Every hour: objectives the human has just met are marked done and paid, and the next one is announced.</summary>
    private void CheckObjectives()
    {
        if (CurrentObjective is null) return;
        foreach (var o in AllObjectives.Where(o => !ObjectiveDone(o) && Met(o)))
        {
            _objectivesDone.Add(o);
            Human.Stockpile[ResourceType.Gold] += GameRules.ObjectiveGold;
            string next = CurrentObjective is { } n ? $" Siguiente: {ObjectiveTitle(n).ToLowerInvariant()}." : " ¡Has cumplido todos los objetivos!";
            Notify(HumanPlayerId, $"Objetivo cumplido: {ObjectiveTitle(o).ToLowerInvariant()} (+{GameRules.ObjectiveGold:0} de oro).{next}");
        }
    }

    /// <summary>Restores the objectives met; saves from before 1.81.0 count those already met, without paying them.</summary>
    private void LoadObjectives(SaveGame save)
    {
        if (save.ObjectivesDone is { } done) _objectivesDone.UnionWith(done);
        else _objectivesDone.UnionWith(AllObjectives.Where(Met));
    }
}
