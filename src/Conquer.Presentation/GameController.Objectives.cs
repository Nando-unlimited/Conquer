using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>
/// The card with the current objective, under the alerts: how many are met, the objective and how to meet it, a
/// button that takes the player where it is done, and the one to fold the card away (or unfold it).
/// </summary>
public sealed record ObjectiveCard(string Header, string Title, string Hint, Button? Go, Button Toggle, bool Folded);

/// <summary>The guided first objectives (<see cref="GameSession.CurrentObjective"/>), shown on a card that can be folded.</summary>
public sealed partial class GameController
{
    public bool ObjectivesFolded { get; set; }

    /// <summary>The card, or null once every objective is met.</summary>
    public ObjectiveCard? ObjectiveCard()
    {
        if (Session.CurrentObjective is not Objective o) return null;
        string header = $"Objetivo {Session.ObjectivesDone + 1} de {GameSession.AllObjectives.Length}";
        var toggle = new Button(ObjectivesFolded ? "+" : "-", () => ObjectivesFolded = !ObjectivesFolded,
            Tooltip: ObjectivesFolded ? "Mostrar el objetivo" : "Plegar", Size: TextSize.Small);
        return new ObjectiveCard(header, GameSession.ObjectiveTitle(o), GameSession.ObjectiveHint(o), GoTo(o), toggle, ObjectivesFolded);
    }

    /// <summary>The button that takes the player where the objective is met, if there is such a place.</summary>
    private Button? GoTo(Objective o)
    {
        int? capital = Human.CapitalCityId is int id ? Session.CityById(id)?.ProvinceId : null;
        Action? go = o switch
        {
            Objective.Capital => Session.Units.FirstOrDefault(u => u.OwnerId == Human.Id && u.Type == Game.Rules.UnitType.Settlers) is { } settlers ? () => ViewUnit(settlers.Id) : null,
            Objective.Research or Objective.Tech => () => OpenNation(NationTab.Science),
            Objective.Work when capital is int c => () => { ViewProvince(c); ProvinceTab = ProvinceTab.Buildings; },
            Objective.Regiment when capital is int c => () => { ViewProvince(c); ProvinceTab = ProvinceTab.Army; },
            Objective.Claim => Session.Units.FirstOrDefault(u => u.OwnerId == Human.Id && u.HasScouts) is { } scouts ? () => ViewUnit(scouts.Id) : null,
            Objective.SecondCity when capital is int c => () => { ViewProvince(c); ProvinceTab = ProvinceTab.General; },
            Objective.People => () => OpenNation(NationTab.Summary),
            Objective.Agreement => () => OpenNation(NationTab.Diplomacy),
            _ => null,
        };
        return go is null ? null : new Button("Ir", go, Tooltip: "Lleva adonde se cumple.", Size: TextSize.Small);
    }
}
