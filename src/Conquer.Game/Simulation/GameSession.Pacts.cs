using Conquer.Game.Entities;
using Conquer.Game.Rules;

namespace Conquer.Game.Simulation;

/// <summary>
/// Agreements short of an alliance. A non-aggression pact keeps two nations from declaring war on each other until one
/// breaks it, which leaves a truce of a year behind. Military access lets one nation's armies cross another's land;
/// it is granted one way, and withdrawn at will.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>Pairs of nations with a non-aggression pact, lower id first.</summary>
    private readonly HashSet<(int, int)> _pacts = [];
    /// <summary>Who lets whose armies through its land: (granter, grantee).</summary>
    private readonly HashSet<(int Granter, int Grantee)> _access = [];

    public bool HavePact(int a, int b) => a != b && _pacts.Contains(WarKey(a, b));

    /// <summary>Whether <paramref name="granterId"/> lets the armies of <paramref name="granteeId"/> cross its land.</summary>
    public bool GivesAccess(int granterId, int granteeId) => _access.Contains((granterId, granteeId));

    /// <summary>Whether a nation's armies may cross another's land in peace: allies, overlord and vassal, or with access.</summary>
    public bool MayCross(int playerId, int holderId) => AreAllied(playerId, holderId) || InVassalage(playerId, holderId) || GivesAccess(holderId, playerId);

    private CommandResult CanAgree(int playerId, int targetId)
    {
        if (playerId == targetId || targetId < 0 || targetId >= Players.Count || Players[targetId].Eliminated) return CommandResult.Fail("Nación no válida.");
        if (AtWar(playerId, targetId)) return CommandResult.Fail($"Estás en guerra con {Players[targetId].Name}.");
        if (InVassalage(playerId, targetId)) return CommandResult.Fail("Entre señor y vasallo no hace falta.");
        if (OverlordOf(playerId) is int overlord) return CommandResult.Fail($"Eres vasallo de {Players[overlord].Name}: tu señor decide.");
        if (OverlordOf(targetId) is int theirs) return CommandResult.Fail($"{Players[targetId].Name} es vasallo de {Players[theirs].Name}.");
        return CommandResult.Success();
    }

    // ------------------------------------------------------------------ pacts

    public CommandResult CanProposePact(int playerId, int targetId)
    {
        var check = CanAgree(playerId, targetId);
        if (!check.Ok) return check;
        if (HavePact(playerId, targetId)) return CommandResult.Fail($"Ya tenéis un pacto con {Players[targetId].Name}.");
        if (AreAllied(playerId, targetId)) return CommandResult.Fail("Los aliados ya no pueden atacarse.");
        return CommandResult.Success();
    }

    /// <summary>Offers a non-aggression pact; a computer rival decides with <see cref="AI.AiPlayer.WouldSignPact"/>, a human is never forced.</summary>
    public CommandResult ProposePact(int playerId, int targetId)
    {
        var check = CanProposePact(playerId, targetId);
        if (!check.Ok) return check;
        var ai = _ais.FirstOrDefault(a => a.PlayerId == targetId);
        if (ai == null || !ai.WouldSignPact(playerId))
            return CommandResult.Fail($"{Players[targetId].Name} rechaza el pacto (su opinión de nosotros: {Opinion(targetId, playerId):0}; pide {GameRules.PactAcceptOpinion:0}, o {GameRules.FearedPactOpinion:0} si nuestro ejército es más fuerte).");
        _pacts.Add(WarKey(playerId, targetId));
        if (targetId == HumanPlayerId) Notify(HumanPlayerId, $"Pacto de no agresión firmado con {Players[playerId].Name}.");
        return CommandResult.Success($"Pacto de no agresión firmado con {Players[targetId].Name}.");
    }

    /// <summary>Breaks a pact: war is possible again after a truce of <see cref="GameRules.BrokenPactTruceDays"/> days.</summary>
    public CommandResult BreakPact(int playerId, int targetId)
    {
        if (!HavePact(playerId, targetId)) return CommandResult.Fail($"No tenéis un pacto con {Players[targetId].Name}.");
        _pacts.Remove(WarKey(playerId, targetId));
        long until = Date.Hours + GameRules.BrokenPactTruceDays * 24L;
        _truces[WarKey(playerId, targetId)] = Math.Max(until, _truces.GetValueOrDefault(WarKey(playerId, targetId)));
        Remember(targetId, playerId, "Rompió el pacto", GameRules.BrokenPactOpinion);
        if (targetId == HumanPlayerId) Notify(HumanPlayerId, $"{Players[playerId].Name} rompe su pacto de no agresión con nosotros.");
        return CommandResult.Success($"Pacto con {Players[targetId].Name} roto: podréis ir a la guerra dentro de {GameRules.BrokenPactTruceDays} días.");
    }

    // ------------------------------------------------------------------ military access

    public CommandResult CanAskAccess(int playerId, int targetId)
    {
        var check = CanAgree(playerId, targetId);
        if (!check.Ok) return check;
        if (AreAllied(playerId, targetId)) return CommandResult.Fail("Los aliados ya cruzan las tierras del otro.");
        if (GivesAccess(targetId, playerId)) return CommandResult.Fail($"{Players[targetId].Name} ya nos deja pasar.");
        return CommandResult.Success();
    }

    /// <summary>Asks another nation to let our armies through; a computer rival decides with <see cref="AI.AiPlayer.WouldGrantAccess"/>.</summary>
    public CommandResult AskAccess(int playerId, int targetId)
    {
        var check = CanAskAccess(playerId, targetId);
        if (!check.Ok) return check;
        var ai = _ais.FirstOrDefault(a => a.PlayerId == targetId);
        if (ai == null || !ai.WouldGrantAccess(playerId))
            return CommandResult.Fail($"{Players[targetId].Name} no nos deja pasar (su opinión de nosotros: {Opinion(targetId, playerId):0}; pide {GameRules.AccessAcceptOpinion:0}).");
        _access.Add((targetId, playerId));
        return CommandResult.Success($"{Players[targetId].Name} deja pasar a nuestros ejércitos.");
    }

    public CommandResult CanGrantAccess(int playerId, int targetId)
    {
        var check = CanAgree(playerId, targetId);
        if (!check.Ok) return check;
        if (AreAllied(playerId, targetId)) return CommandResult.Fail("Los aliados ya cruzan las tierras del otro.");
        if (GivesAccess(playerId, targetId)) return CommandResult.Fail($"Ya dejas pasar a {Players[targetId].Name}.");
        return CommandResult.Success();
    }

    /// <summary>Lets another nation's armies cross our land; it thinks better of us for it.</summary>
    public CommandResult GrantAccess(int playerId, int targetId)
    {
        var check = CanGrantAccess(playerId, targetId);
        if (!check.Ok) return check;
        _access.Add((playerId, targetId));
        if (targetId == HumanPlayerId) Notify(HumanPlayerId, $"{Players[playerId].Name} deja pasar a nuestros ejércitos por sus tierras.");
        return CommandResult.Success($"Los ejércitos de {Players[targetId].Name} pueden cruzar nuestras tierras.");
    }

    /// <summary>Withdraws the access we gave; the other nation's troops on our land walk back to theirs.</summary>
    public CommandResult RevokeAccess(int playerId, int targetId)
    {
        if (!_access.Remove((playerId, targetId))) return CommandResult.Fail($"No dejas pasar a {Players[targetId].Name}.");
        Evict(targetId, playerId);
        if (targetId == HumanPlayerId) Notify(HumanPlayerId, $"{Players[playerId].Name} ya no deja pasar a nuestros ejércitos.");
        return CommandResult.Success($"Los ejércitos de {Players[targetId].Name} ya no pueden cruzar nuestras tierras.");
    }

    /// <summary>The guest's troops standing on the host's land, which they may no longer cross, go home.</summary>
    private void Evict(int guestId, int hostId)
    {
        if (MayCross(guestId, hostId) || AtWar(guestId, hostId)) return;
        foreach (var unit in Units.Where(u => u.OwnerId == guestId && !u.IsAboard && Map.Provinces[u.ProvinceId].ControllerId == hostId).ToList())
            SendHome(unit);
    }

    /// <summary>A war ends any pact and any access between the two.</summary>
    private void EndAgreements(int a, int b)
    {
        _pacts.Remove(WarKey(a, b));
        _access.Remove((a, b));
        _access.Remove((b, a));
    }
}
