using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>The end of the game: victory or defeat, how it came about, the final ranking and what to do now.</summary>
public sealed record GameOverWindow(string Title, bool Won, string Text, IReadOnlyList<(string Name, string Score, uint Color)> Ranking, IReadOnlyList<Button> Buttons);

/// <summary>Victory and defeat: the window opens once when the game is decided, and time stops while it is open.</summary>
public sealed partial class GameController
{
    /// <summary>Whether the player has already seen how the game ended (and chose to go on).</summary>
    private bool _outcomeSeen;

    public bool GameOverOpen => !_outcomeSeen && (Session.Outcome != null || Human.Eliminated);

    public GameOverWindow? GameOver(IMenuNavigator navigator)
    {
        if (!GameOverOpen) return null;
        bool won = Session.Outcome is { } o && o.WinnerId == Human.Id;
        string title = won ? "¡Victoria!" : "Derrota";
        string text = Session.Outcome is { } outcome
            ? $"{GameSession.VictoryName(outcome.Kind)}. {Session.VictoryText(outcome)}"
            : $"{Human.Name} ha caído: ya no le queda nada que gobernar.";
        var ranking = Session.Players.OrderByDescending(Session.Score).ThenBy(p => p.Id)
            .Select(p => (p.Eliminated ? $"{p.Name} (eliminada)" : p.Name, p.Eliminated ? "-" : $"{Session.Score(p):N0}", p.Color)).ToList();
        return new GameOverWindow(title, won, text, ranking,
        [
            new Button(Human.Eliminated ? "Seguir mirando" : "Seguir jugando", () => _outcomeSeen = true,
                Tooltip: "La partida continúa; esta ventana no volverá a abrirse."),
            new Button("Menú principal", navigator.ShowMainMenu),
        ]);
    }
}
