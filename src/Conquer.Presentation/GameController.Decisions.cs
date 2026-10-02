using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>
/// An event waiting for the player's answer: what happened and where, one button per answer (its label, what it does
/// and what it costs in the tooltip), how long is left and how many more are waiting.
/// </summary>
public sealed record DecisionWindow(string Title, string Place, string Text, IReadOnlyList<Button> Options, IReadOnlyList<string> Effects,
    string Footer, Button View);

/// <summary>Events with decisions: the window opens by itself when one arrives, and time stops until the player answers.</summary>
public sealed partial class GameController
{
    public bool DecisionOpen => Session.PendingDecisions(Human.Id).Count > 0;

    /// <summary>The oldest decision waiting for the player, if any.</summary>
    public DecisionWindow? DecisionWindow()
    {
        var pending = Session.PendingDecisions(Human.Id);
        if (pending.Count == 0) return null;
        var d = pending[0];
        var province = Map.Provinces[d.ProvinceId];
        var options = Session.DecisionOptions(d);
        var buttons = new List<Button>();
        var effects = new List<string>();
        for (int i = 0; i < options.Count; i++)
        {
            int option = i;
            var can = Session.CanChoose(Human.Id, d.Id, i);
            string label = options[i].Cost is { } cost ? $"{options[i].Label} ({cost})" : options[i].Label;
            buttons.Add(new Button(label, () => Show(Session.Choose(Human.Id, d.Id, option)), can.Ok, Tooltip: can.Ok ? options[i].Effect : can.Message));
            effects.Add(options[i].Effect);
        }
        int days = (int)Math.Ceiling((d.ExpiresHours - Session.Date.Hours) / 24.0);
        string footer = $"Si no decides en {days} días, se optará por «{options[0].Label.ToLowerInvariant()}»."
                        + (pending.Count > 1 ? $" Quedan {pending.Count - 1} decisiones más." : "");
        return new DecisionWindow(Session.DecisionTitle(d), PlaceOf(province), Session.DecisionText(d), buttons, effects, footer,
            new Button("Ver en el mapa", () => ViewProvince(province.Id), Size: TextSize.Small));
    }
}
