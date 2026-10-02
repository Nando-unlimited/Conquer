using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>A siege under way: who besieges the province and how many days of work they have done.</summary>
public sealed class Siege(int provinceId, int attackerId, double progress = 0)
{
    public int ProvinceId { get; } = provinceId;
    public int AttackerId { get; } = attackerId;
    public double Progress { get; internal set; } = progress;
}

/// <summary>
/// Sieges. A province with walls or a castle is not taken by marching in: the troops that enter it besiege it, and it
/// falls after <see cref="MilitaryRules.SiegeDaysPerDefense"/> days per point of its fortifications' defence, fewer with
/// artillery. If the besiegers leave, the siege is lifted.
/// </summary>
public sealed partial class GameSession
{
    private readonly Dictionary<int, Siege> _sieges = [];

    public IEnumerable<Siege> Sieges => _sieges.Values;

    public Siege? SiegeAt(int provinceId) => _sieges.GetValueOrDefault(provinceId);

    /// <summary>Whether the province's walls or castle hold out against an enemy that marches in.</summary>
    public static bool IsFortified(Province p) => p.BuildingBonuses.Defense > 0;

    /// <summary>Days a siege of the province takes without artillery.</summary>
    public static double SiegeDays(Province p) => MilitaryRules.SiegeDaysPerDefense * p.BuildingBonuses.Defense;

    /// <summary>The besieger's regiments in the province, on land.</summary>
    private IEnumerable<Unit> Besiegers(Siege siege) =>
        Units.Where(u => u.IsMilitary && !u.IsAboard && u.OwnerId == siege.AttackerId && u.ProvinceId == siege.ProvinceId);

    /// <summary>
    /// Days of siege work done in a day: one, plus what the artillery adds by its firepower (a full battalion of
    /// catapults adds one more; cannons and guns, more).
    /// </summary>
    public double DailySiegeWork(Siege siege) =>
        1 + Besiegers(siege).SelectMany(u => u.Battalions).Where(b => b.Type.Role() == BattalionRole.Artillery)
            .Sum(b => b.Info.Attack / MilitaryRules.SiegeAttackPerDay * b.StrengthShare);

    /// <summary>Whether the unit is besieging the province it stands in.</summary>
    public bool IsBesieging(Unit unit) => SiegeAt(unit.ProvinceId) is { } siege && siege.AttackerId == unit.OwnerId && unit.IsMilitary;

    /// <summary>A regiment marches into a fortified enemy province: it lays siege to it, or joins the siege.</summary>
    private void LaySiege(Province p, int attackerId)
    {
        if (_sieges.TryGetValue(p.Id, out var siege) && siege.AttackerId == attackerId) return;
        _sieges[p.Id] = new Siege(p.Id, attackerId);
        Raise(GameEventKind.SiegeLaid, attackerId, p.ControllerId);
        string place = PlaceName(p);
        if (attackerId == HumanPlayerId)
            Notify(HumanPlayerId, $"Sitiamos {place}: caerá en unos {SiegeDays(p):0} días, menos con artillería.");
        else if (p.ControllerId == HumanPlayerId)
            Notify(HumanPlayerId, $"¡{Players[attackerId].Name} sitia {place}!");
    }

    /// <summary>Each day every siege advances; one with no besiegers left is lifted, and one that is done takes the province.</summary>
    private void DailySieges()
    {
        foreach (var siege in _sieges.Values.ToList())
        {
            var p = Map.Provinces[siege.ProvinceId];
            if (!Besiegers(siege).Any() || !AtWar(siege.AttackerId, p.ControllerId))
            {
                _sieges.Remove(siege.ProvinceId);
                if (p.ControllerId == HumanPlayerId) Notify(HumanPlayerId, $"Se levanta el asedio de {PlaceName(p)}.");
                continue;
            }
            siege.Progress += DailySiegeWork(siege);
            if (siege.Progress < SiegeDays(p)) continue;
            _sieges.Remove(siege.ProvinceId);
            Occupy(p, siege.AttackerId);
        }
    }

    /// <summary>Sieges between two nations end with their war; the one of a province that changes hands, too.</summary>
    private void EndSieges(Func<Siege, bool> which)
    {
        foreach (var siege in _sieges.Values.Where(which).ToList()) _sieges.Remove(siege.ProvinceId);
    }
}
