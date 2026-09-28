using Conquer.Game.Entities;

namespace Conquer.Game.Simulation;

/// <summary>War and peace between nations. Armies may only enter the land of nations they are at war with.</summary>
public sealed partial class GameSession
{
    /// <summary>Pairs of nations at war (lower id first), with the hour each war began.</summary>
    private readonly Dictionary<(int, int), long> _wars = [];

    private static (int, int) WarKey(int a, int b) => a < b ? (a, b) : (b, a);

    public bool AtWar(int a, int b) => a != b && a >= 0 && b >= 0 && _wars.ContainsKey(WarKey(a, b));

    public IEnumerable<Player> EnemiesOf(int playerId) => Players.Where(p => AtWar(playerId, p.Id));

    /// <summary>Days since the war between two nations began (0 at peace).</summary>
    public double WarDays(int a, int b) => _wars.TryGetValue(WarKey(a, b), out long start) ? (Date.Hours - start) / 24.0 : 0;

    public CommandResult CanDeclareWar(int playerId, int targetId)
    {
        if (playerId == targetId || targetId < 0 || targetId >= Players.Count) return CommandResult.Fail("Nación no válida.");
        if (AtWar(playerId, targetId)) return CommandResult.Fail($"Ya estás en guerra con {Players[targetId].Name}.");
        return CommandResult.Success();
    }

    public CommandResult DeclareWar(int playerId, int targetId)
    {
        var check = CanDeclareWar(playerId, targetId);
        if (!check.Ok) return check;
        _wars[WarKey(playerId, targetId)] = Date.Hours;
        if (playerId == HumanPlayerId) Notify(HumanPlayerId, $"Declaramos la guerra a {Players[targetId].Name}.");
        else if (targetId == HumanPlayerId) Notify(HumanPlayerId, $"¡{Players[playerId].Name} nos declara la guerra!");
        return CommandResult.Success();
    }

    /// <summary>
    /// Offers peace to an enemy. A computer rival accepts if the war is going badly for it or has
    /// dragged on; a human player is never forced into it.
    /// </summary>
    public CommandResult ProposePeace(int playerId, int targetId)
    {
        if (!AtWar(playerId, targetId)) return CommandResult.Fail($"No estás en guerra con {Players[targetId].Name}.");
        var ai = _ais.FirstOrDefault(a => a.PlayerId == targetId);
        if (ai == null || !ai.WouldAcceptPeace(playerId)) return CommandResult.Fail($"{Players[targetId].Name} rechaza la paz.");
        MakePeace(playerId, targetId);
        return CommandResult.Success($"Paz firmada con {Players[targetId].Name}.");
    }

    /// <summary>
    /// Ends a war: battles between the two stop, every occupied province goes back to its owner and
    /// the armies standing in the other's land walk back to their nearest province.
    /// </summary>
    internal void MakePeace(int a, int b)
    {
        _wars.Remove(WarKey(a, b));
        foreach (var battle in _battles.Where(x => WarKey(x.AttackerId, x.DefenderId) == WarKey(a, b)).ToList())
        {
            foreach (var unit in battle.Attackers.Select(UnitById).OfType<Unit>()) unit.AttackingProvinceId = null;
            _battles.Remove(battle);
        }
        foreach (var p in Map.Provinces.Where(p => p.IsOccupied && WarKey(p.OwnerId, p.ControllerId) == WarKey(a, b)))
        {
            p.ControllerId = p.OwnerId;
            OwnershipChanged?.Invoke(p.Id);
        }
        foreach (var unit in Units.ToList())
        {
            var here = Map.Provinces[unit.ProvinceId];
            if ((unit.OwnerId != a && unit.OwnerId != b) || !here.IsOwned || here.ControllerId == unit.OwnerId) continue;
            unit.Path.Clear();
            unit.StepHours = unit.HoursToNext = 0;
            var home = Players[unit.OwnerId].Provinces.Where(id => Map.Provinces[id].ControllerId == unit.OwnerId)
                .OrderBy(id => Map.DistanceKm(here, Map.Provinces[id])).Select(id => (int?)id).FirstOrDefault();
            if (home is int h) unit.ProvinceId = h;
            else RemoveUnit(unit);
        }
        foreach (int id in new[] { a, b }.Where(id => id == HumanPlayerId))
            Notify(id, $"Paz firmada con {Players[a == id ? b : a].Name}.");
    }
}
