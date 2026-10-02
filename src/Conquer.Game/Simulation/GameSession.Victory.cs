using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.Science;

namespace Conquer.Game.Simulation;

/// <summary>The ways to win the game.</summary>
public enum VictoryKind
{
    Domination,
    Science,
    Score,
}

/// <summary>Who won the game, how and when.</summary>
public sealed record GameOutcome(int WinnerId, VictoryKind Kind, long Hours);

/// <summary>
/// Victory. A nation wins when every other is gone or its vassal (domination), when it knows every advance (science),
/// or, if nobody has by the start of year <see cref="GameRules.ScoreVictoryYear"/>, with the highest score. The human
/// loses when another nation wins or when their own nation is eliminated. The game can go on after either.
/// </summary>
public sealed partial class GameSession
{
    public GameOutcome? Outcome { get; private set; }

    /// <summary>Whether the human has lost: another nation has won, or theirs is gone.</summary>
    public bool HumanDefeated => Human.Eliminated || Outcome is { } o && o.WinnerId != HumanPlayerId;

    /// <summary>A nation's score: its people, provinces, cities and advances.</summary>
    public double Score(Player player) => player.Eliminated ? 0
        : player.Provinces.Sum(id => Map.Provinces[id].Population) / GameRules.ScorePeople
          + player.Provinces.Count * GameRules.ScorePerProvince
          + Cities.Count(c => c.OwnerId == player.Id) * GameRules.ScorePerCity
          + player.Techs.Count * GameRules.ScorePerTech;

    /// <summary>The nations still standing, highest score first.</summary>
    public IReadOnlyList<Player> Ranking => [.. Players.Where(p => !p.Eliminated).OrderByDescending(Score).ThenBy(p => p.Id)];

    /// <summary>The other nations a nation still has to eliminate or make its vassals to dominate.</summary>
    public int RivalsLeft(int playerId) => Players.Count(p => p.Id != playerId && !p.Eliminated && OverlordOf(p.Id) != playerId);

    /// <summary>Whether the nation has met a victory condition now.</summary>
    private bool Wins(Player player, VictoryKind kind) => kind switch
    {
        VictoryKind.Science => player.Techs.Count == Techs.All.Length,
        VictoryKind.Domination => Players.Count > 1 && player.CapitalCityId.HasValue && RivalsLeft(player.Id) == 0,
        _ => Date.Year >= GameRules.ScoreVictoryYear && Ranking.FirstOrDefault() == player,
    };

    /// <summary>Each day, until someone has won: the first nation (the human first) to meet a condition wins.</summary>
    private void DailyVictory()
    {
        if (Outcome != null) return;
        foreach (var kind in Enum.GetValues<VictoryKind>())
            foreach (var player in Players.Where(p => !p.Eliminated).OrderByDescending(p => p.Id == HumanPlayerId))
            {
                if (!Wins(player, kind)) continue;
                Win(player.Id, kind);
                return;
            }
    }

    /// <summary>The nation wins the game.</summary>
    public void Win(int playerId, VictoryKind kind)
    {
        Outcome = new GameOutcome(playerId, kind, Date.Hours);
        Notify(HumanPlayerId, playerId == HumanPlayerId ? $"¡Victoria! {VictoryText(Outcome)}" : $"Derrota. {VictoryText(Outcome)}");
    }

    public static string VictoryName(VictoryKind kind) => kind switch
    {
        VictoryKind.Domination => "Dominación",
        VictoryKind.Science => "Ciencia",
        _ => "Puntuación",
    };

    /// <summary>How the game was won, in a sentence.</summary>
    public string VictoryText(GameOutcome outcome)
    {
        string who = Players[outcome.WinnerId].Name;
        return outcome.Kind switch
        {
            VictoryKind.Domination => $"{who} domina el mundo: no queda ninguna nación que no haya caído o no le rinda vasallaje.",
            VictoryKind.Science => $"{who} ha descubierto todos los avances de la ciencia.",
            _ => $"Al llegar el año {GameRules.ScoreVictoryYear}, {who} tiene la mayor puntuación ({Score(Players[outcome.WinnerId]):N0}).",
        };
    }
}
