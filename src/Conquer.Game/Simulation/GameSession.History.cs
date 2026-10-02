using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Rules;

namespace Conquer.Game.Simulation;

/// <summary>A nation's figures on one day, for the statistics graphs.</summary>
public sealed record HistorySample(long Hours, int PlayerId, double Population, double Soldiers, double Gold, int Provinces, double Science);

/// <summary>
/// The ledger. Every <see cref="GameRules.HistoryDays"/> days each nation's people, soldiers, daily gold, provinces and
/// daily science are written down, so the nation screen can draw how they have changed over the game.
/// </summary>
public sealed partial class GameSession
{
    private readonly List<HistorySample> _history = [];

    public IReadOnlyList<HistorySample> History => _history;

    public IEnumerable<HistorySample> HistoryOf(int playerId) => _history.Where(h => h.PlayerId == playerId);

    /// <summary>A nation's figures now.</summary>
    public HistorySample Sample(Player player) => new(Date.Hours, player.Id,
        player.Provinces.Sum(id => Map.Provinces[id].Population),
        Units.Where(u => u.OwnerId == player.Id && (u.IsMilitary || u.IsFleet)).Sum(u => u.Citizens),
        player.LastDayNet[(int)ResourceType.Gold], player.Provinces.Count, player.LastDayScience);

    /// <summary>Every <see cref="GameRules.HistoryDays"/> days, writes down the figures of the nations still standing.</summary>
    private void DailyHistory()
    {
        if (Date.Days % GameRules.HistoryDays != 0) return;
        foreach (var player in Players.Where(p => !p.Eliminated)) _history.Add(Sample(player));
    }
}
