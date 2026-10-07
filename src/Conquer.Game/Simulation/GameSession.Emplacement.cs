using Conquer.Game.Entities;
using Conquer.Game.Rules;

namespace Conquer.Game.Simulation;

/// <summary>
/// Emplacing: a combat unit on land digs in where it stands. Day by day it entrenches (up to
/// <see cref="MilitaryRules.EmplacementDays"/>), which makes it harder to dislodge, and meanwhile it regains organisation
/// faster and its shipments reach it sooner. Moving or attacking lifts the emplacement and loses what was dug.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>Whether the unit is dug in where it stands: emplaced there, still and not attacking.</summary>
    public static bool IsEmplaced(Unit unit) =>
        unit.EmplacedAt == unit.ProvinceId && !unit.IsMoving && unit.AttackingProvinceId is null && !unit.IsAboard;

    /// <summary>The extra defence the unit's entrenchment gives it (0 when it is not emplaced).</summary>
    public static double EmplacementBonus(Unit unit) => IsEmplaced(unit) ? MilitaryRules.EmplacementDefense * unit.Entrenchment : 0;

    public CommandResult CanEmplace(Unit unit)
    {
        if (!unit.IsMilitary || unit.Flies) return CommandResult.Fail("Solo las unidades de combate de tierra se emplazan.");
        if (unit.IsAboard) return CommandResult.Fail("Va embarcada.");
        if (IsEmplaced(unit)) return CommandResult.Fail("Ya está emplazada.");
        if (unit.IsMoving || unit.AttackingProvinceId.HasValue) return CommandResult.Fail("Detenla antes de emplazarla.");
        return CommandResult.Success();
    }

    /// <summary>The unit starts digging in where it stands.</summary>
    public CommandResult Emplace(int playerId, int unitId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        var can = CanEmplace(unit);
        if (!can.Ok) return can;
        unit.EmplacedAt = unit.ProvinceId;
        unit.Entrenchment = 0;
        return CommandResult.Success();
    }

    /// <summary>The unit leaves its emplacement, ready to march.</summary>
    public CommandResult LiftEmplacement(int playerId, int unitId)
    {
        if (UnitById(unitId) is not { } unit || unit.OwnerId != playerId) return CommandResult.Fail("Unidad no válida.");
        Unemplace(unit);
        return CommandResult.Success();
    }

    private static void Unemplace(Unit unit)
    {
        unit.EmplacedAt = null;
        unit.Entrenchment = 0;
    }

    /// <summary>Each day, emplaced units dig in a little more; those that left their place lose it.</summary>
    private void DailyEmplacements(Player player)
    {
        foreach (var unit in Units.Where(u => u.OwnerId == player.Id && u.EmplacedAt.HasValue))
        {
            if (!IsEmplaced(unit)) Unemplace(unit);
            else unit.Entrenchment = Math.Min(1, unit.Entrenchment + 1 / MilitaryRules.EmplacementDays);
        }
    }
}
