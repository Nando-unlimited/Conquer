using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Rules;

namespace Conquer.Game.Simulation;

/// <summary>War reparations: the payer hands part of its gold income to the receiver until a given hour.</summary>
public sealed record Reparations(int PayerId, int ReceiverId, long UntilHours);

/// <summary>
/// What a lost war can cost beyond land: reparations (part of the gold income for some years) and vassalage (a nation
/// that pays tribute, fights its overlord's wars and may in time be annexed). A nation whose cities are all occupied
/// capitulates: its enemies keep what they hold and the rest goes to whoever took its capital. A nation left without
/// land is out of the game.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>Each vassal's overlord and the hour it became one.</summary>
    private readonly Dictionary<int, (int Overlord, long Since)> _vassals = [];
    private readonly List<Reparations> _reparations = [];

    public int? OverlordOf(int playerId) => _vassals.TryGetValue(playerId, out var v) ? v.Overlord : null;

    public bool IsVassalOf(int vassalId, int overlordId) => OverlordOf(vassalId) == overlordId;

    public IEnumerable<Player> VassalsOf(int playerId) => Players.Where(p => OverlordOf(p.Id) == playerId);

    /// <summary>Whether one is the other's vassal, either way round.</summary>
    public bool InVassalage(int a, int b) => IsVassalOf(a, b) || IsVassalOf(b, a);

    /// <summary>Years a nation has been a vassal (0 if it is not one).</summary>
    public double VassalYears(int vassalId) => _vassals.TryGetValue(vassalId, out var v) ? (Date.Hours - v.Since) / 24.0 / 365 : 0;

    public IReadOnlyList<Reparations> AllReparations => _reparations;

    /// <summary>Days the payer still owes the receiver reparations (0 if it owes none).</summary>
    public double ReparationsDaysLeft(int payerId, int receiverId) =>
        _reparations.Where(r => r.PayerId == payerId && r.ReceiverId == receiverId).Select(r => (r.UntilHours - Date.Hours) / 24.0).DefaultIfEmpty(0).Max();

    /// <summary>
    /// The nation becomes the other's vassal: its own vassals go free, it leaves its alliances and makes peace with its
    /// new overlord's allies and vassals.
    /// </summary>
    internal void MakeVassal(int vassalId, int overlordId)
    {
        foreach (var v in VassalsOf(vassalId).ToList()) _vassals.Remove(v.Id);
        _alliances.RemoveWhere(a => a.Item1 == vassalId || a.Item2 == vassalId);
        _pacts.RemoveWhere(a => a.Item1 == vassalId || a.Item2 == vassalId);
        _access.RemoveWhere(a => a.Granter == vassalId || a.Grantee == vassalId);
        _vassals[vassalId] = (overlordId, Date.Hours);
        foreach (var friend in AlliesOf(overlordId).Concat(VassalsOf(overlordId)).Where(f => AtWar(f.Id, vassalId)).ToList())
            MakePeace(friend.Id, vassalId);
    }

    /// <summary>
    /// When a war breaks out, the attacker's vassals join it, and the defender's overlord and vassals come to its aid
    /// (an overlord's own wars are its vassals'). Those allied with or vassals of the other side stay out.
    /// </summary>
    private void CallVassals(int attackerId, int defenderId)
    {
        var joining = VassalsOf(attackerId).Select(v => (v.Id, Enemy: defenderId))
            .Concat(VassalsOf(defenderId).Select(v => (v.Id, Enemy: attackerId)))
            .Concat(OverlordOf(defenderId) is int o ? [(o, attackerId)] : []);
        foreach (var (id, enemy) in joining.ToList())
        {
            if (id == enemy || AtWar(id, enemy) || AreAllied(id, enemy) || InVassalage(id, enemy) || HavePact(id, enemy) || Players[id].Eliminated) continue;
            StartWar(id, enemy);
            Raise(GameEventKind.WarDeclared, id, enemy);
            if (id == HumanPlayerId) Notify(HumanPlayerId, $"Entramos en guerra con {Players[enemy].Name} por el vasallaje que nos une a {Players[enemy == attackerId ? defenderId : attackerId].Name}.");
            else if (enemy == HumanPlayerId) Notify(HumanPlayerId, $"¡{Players[id].Name} entra en la guerra contra nosotros!");
        }
    }

    // ------------------------------------------------------------------ tribute

    /// <summary>
    /// Each day, vassals and those paying reparations hand over their share of the day's gold income (when there is
    /// one). Reparations that have run their course end.
    /// </summary>
    private void DailyTributes()
    {
        _reparations.RemoveAll(r => r.UntilHours <= Date.Hours || Players[r.PayerId].Eliminated || Players[r.ReceiverId].Eliminated);
        foreach (var (vassal, (overlord, _)) in _vassals) Pay(vassal, overlord, GameRules.VassalTributeShare);
        foreach (var r in _reparations) Pay(r.PayerId, r.ReceiverId, GameRules.ReparationsShare);

        void Pay(int payerId, int receiverId, double share)
        {
            var payer = Players[payerId];
            double gold = Math.Min(payer.Stockpile[ResourceType.Gold], Math.Max(0, payer.LastDayNet[(int)ResourceType.Gold]) * share);
            if (gold <= 0) return;
            payer.Stockpile[ResourceType.Gold] -= gold;
            Players[receiverId].Stockpile[ResourceType.Gold] += gold;
            payer.LastDayNet[(int)ResourceType.Gold] -= gold;
            Players[receiverId].LastDayNet[(int)ResourceType.Gold] += gold;
        }
    }

    /// <summary>Gold a nation pays (negative) or receives each day in tribute and reparations, at the last day's income.</summary>
    public double DailyTribute(int playerId)
    {
        double Income(int id) => Math.Max(0, Players[id].LastDayNet[(int)ResourceType.Gold]);
        double total = 0;
        foreach (var (vassal, (overlord, _)) in _vassals)
        {
            if (vassal == playerId) total -= Income(vassal) * GameRules.VassalTributeShare;
            if (overlord == playerId) total += Income(vassal) * GameRules.VassalTributeShare;
        }
        foreach (var r in _reparations)
        {
            if (r.PayerId == playerId) total -= Income(r.PayerId) * GameRules.ReparationsShare;
            if (r.ReceiverId == playerId) total += Income(r.PayerId) * GameRules.ReparationsShare;
        }
        return total;
    }

    // ------------------------------------------------------------------ vassals

    public CommandResult CanAnnexVassal(int overlordId, int vassalId)
    {
        if (!IsVassalOf(vassalId, overlordId)) return CommandResult.Fail($"{Players[vassalId].Name} no es vasallo tuyo.");
        if (AtWar(overlordId, vassalId) || EnemiesOf(vassalId).Any()) return CommandResult.Fail("No mientras haya guerra.");
        double years = VassalYears(vassalId);
        if (years < GameRules.VassalAnnexYears)
            return CommandResult.Fail($"Hace falta que sea vasallo {GameRules.VassalAnnexYears:0} años; lleva {years:0.#}.");
        return CommandResult.Success();
    }

    /// <summary>The overlord takes its vassal's land, cities and people; the vassal is no more.</summary>
    public CommandResult AnnexVassal(int overlordId, int vassalId)
    {
        var check = CanAnnexVassal(overlordId, vassalId);
        if (!check.Ok) return check;
        int count = Players[vassalId].Provinces.Count;
        foreach (int id in Players[vassalId].Provinces.ToList()) Cede(Map.Provinces[id], overlordId);
        Eliminate(vassalId);
        string text = $"{Players[vassalId].Name} pasa a formar parte de {Players[overlordId].Name} ({Provinces(count)}).";
        Notify(HumanPlayerId, text);
        return CommandResult.Success(text);
    }

    /// <summary>The overlord sets its vassal free, which the vassal will remember kindly.</summary>
    public CommandResult ReleaseVassal(int overlordId, int vassalId)
    {
        if (!IsVassalOf(vassalId, overlordId)) return CommandResult.Fail($"{Players[vassalId].Name} no es vasallo tuyo.");
        _vassals.Remove(vassalId);
        Remember(vassalId, overlordId, "Nos dio la libertad", GameRules.ReleasedOpinion);
        if (vassalId == HumanPlayerId) Notify(HumanPlayerId, $"{Players[overlordId].Name} nos libera del vasallaje.");
        return CommandResult.Success($"{Players[vassalId].Name} ya no es vasallo nuestro.");
    }

    // ------------------------------------------------------------------ capitulation

    /// <summary>
    /// A nation at war whose every city is held by its enemies capitulates: each enemy keeps the provinces it occupies,
    /// and whoever holds its capital (or else the most) takes the rest. The nation is out of the game.
    /// </summary>
    private void DailyCapitulations()
    {
        foreach (var loser in Players.Where(p => !p.Eliminated && EnemiesOf(p.Id).Any()).ToList())
        {
            var cities = Cities.Where(c => c.OwnerId == loser.Id).Select(c => Map.Provinces[c.ProvinceId]).ToList();
            if (cities.Count == 0 || cities.Any(p => p.ControllerId == loser.Id || !AtWar(loser.Id, p.ControllerId))) continue;
            Capitulate(loser.Id);
        }
    }

    private void Capitulate(int loserId)
    {
        var loser = Players[loserId];
        var capital = loser.CapitalCityId is int c && CityById(c) is { } city ? Map.Provinces[city.ProvinceId] : null;
        int victor = capital != null && AtWar(loserId, capital.ControllerId) ? capital.ControllerId
            : EnemiesOf(loserId).MaxBy(e => OccupiedBy(e.Id, loserId).Count)!.Id;
        // What nobody occupies falls to the victor with the rest.
        foreach (int id in loser.Provinces)
        {
            var p = Map.Provinces[id];
            if (p.ControllerId == loserId) p.ControllerId = victor;
        }
        Notify(HumanPlayerId, loserId == HumanPlayerId ? "Hemos capitulado: el enemigo ocupa todas nuestras ciudades."
            : $"{loser.Name} capitula: el enemigo ocupa todas sus ciudades.");
        foreach (var enemy in EnemiesOf(loserId).OrderBy(e => e.Id == victor).ToList())
            MakePeace(enemy.Id, loserId, PeaceTerms.TakeOccupied);
        if (!loser.Eliminated) Eliminate(loserId);
    }

    /// <summary>
    /// Takes a nation out of the game: its remaining wars, alliances, vassalage and reparations end, and its units and
    /// migrants vanish.
    /// </summary>
    internal void Eliminate(int playerId)
    {
        var player = Players[playerId];
        player.Eliminated = true;
        foreach (var key in _wars.Keys.Where(k => k.Item1 == playerId || k.Item2 == playerId).ToList()) _wars.Remove(key);
        foreach (var battle in _battles.Where(b => b.AttackerId == playerId || b.DefenderId == playerId).ToList())
        {
            foreach (var unit in battle.Attackers.Select(UnitById).OfType<Unit>()) unit.AttackingProvinceId = null;
            _battles.Remove(battle);
        }
        EndSieges(s => s.AttackerId == playerId);
        _alliances.RemoveWhere(a => a.Item1 == playerId || a.Item2 == playerId);
        _pacts.RemoveWhere(a => a.Item1 == playerId || a.Item2 == playerId);
        _access.RemoveWhere(a => a.Granter == playerId || a.Grantee == playerId);
        _vassals.Remove(playerId);
        foreach (var v in VassalsOf(playerId).ToList()) _vassals.Remove(v.Id);
        _reparations.RemoveAll(r => r.PayerId == playerId || r.ReceiverId == playerId);
        _trades.RemoveAll(t => t.Involves(playerId));
        foreach (var unit in Units.Where(u => u.OwnerId == playerId).ToList()) RemoveUnit(unit, officerSurvives: false);
        Migrations.RemoveAll(m => m.OwnerId == playerId);
        foreach (var r in Resources.All) player.Stockpile[r] = 0;
        player.CapitalCityId = null;
        // Occupied land of others it still held goes back to its owners.
        foreach (var p in Map.Provinces.Where(p => p.ControllerId == playerId && p.OwnerId != playerId))
        {
            p.ControllerId = p.OwnerId;
            OwnershipChanged?.Invoke(p.Id);
        }
        Raise(GameEventKind.NationEliminated, playerId);
        if (playerId != HumanPlayerId) Notify(HumanPlayerId, $"{player.Name} ha desaparecido.");
    }
}
