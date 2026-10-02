using Conquer.Game.Entities;
using Conquer.Game.Rules;

namespace Conquer.Game.Simulation;

/// <summary>
/// The reserve of recruits. Soldiers still come out of the provinces' people, but only as many as the reserve holds:
/// it is a fixed base plus a share of the settled people (larger with some advances), and it refills slowly, so an army
/// lost takes years to replace.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The most recruits the nation can have ready: a base plus a share of its people in provinces it holds.</summary>
    public double ManpowerCapacity(Player player)
    {
        double people = player.Provinces.Select(id => Map.Provinces[id]).Where(p => !p.IsOccupied).Sum(p => p.Population);
        return (GameRules.BaseManpower + people * GameRules.ManpowerShare) * (1 + player.Bonuses.Manpower);
    }

    /// <summary>Recruits the reserve gains each day: it fills from empty in <see cref="GameRules.ManpowerRecoveryYears"/>.</summary>
    public double ManpowerPerDay(Player player) => ManpowerCapacity(player) / (GameRules.ManpowerRecoveryYears * 365);

    /// <summary>Each day the reserve refills, and shrinks to what the nation can hold if it lost people or land.</summary>
    private void DailyManpower(Player player)
    {
        double capacity = ManpowerCapacity(player);
        player.Manpower = Math.Min(capacity, player.Manpower + capacity / (GameRules.ManpowerRecoveryYears * 365));
    }

    /// <summary>Why the reserve cannot give that many men, or null when it can.</summary>
    private static CommandResult? LacksManpower(Player player, double men) =>
        player.Manpower + 1e-9 < men ? CommandResult.Fail($"Faltan reclutas: la reserva tiene {player.Manpower:N0} y hacen falta {men:N0}.") : null;

    /// <summary>Men going back to civilian life return to the reserve, as far as it holds.</summary>
    private void ReturnToReserve(Player player, double men) => player.Manpower = Math.Min(ManpowerCapacity(player), player.Manpower + men);
}
