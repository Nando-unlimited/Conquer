using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Rules;

namespace Conquer.Game.Simulation;

/// <summary>Something a nation remembers about another: why, and how much it still weighs (it fades day by day).</summary>
public sealed class OpinionMemory(string reason, double value)
{
    public string Reason { get; } = reason;
    public double Value { get; internal set; } = value;
}

/// <summary>
/// What nations think of each other, and alliances. Opinion (-100 to 100) adds up what a nation sees now (a shared
/// border, a common enemy, an alliance, its people under the other's rule) and what it remembers (wars declared on it,
/// land taken, gifts, betrayals), which fades with time. Allies defend each other: when one is attacked, the others
/// join the war; their armies may cross each other's land.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>What each nation remembers about each other, by (who remembers, about whom).</summary>
    private readonly Dictionary<(int From, int To), List<OpinionMemory>> _memories = [];
    /// <summary>Pairs of allied nations, lower id first.</summary>
    private readonly HashSet<(int, int)> _alliances = [];

    public bool AreAllied(int a, int b) => a != b && a >= 0 && b >= 0 && _alliances.Contains(WarKey(a, b));

    public IEnumerable<Player> AlliesOf(int playerId) => Players.Where(p => AreAllied(playerId, p.Id));

    /// <summary>Why <paramref name="from"/> thinks what it does of <paramref name="about"/>: (reason, points).</summary>
    public List<(string Reason, double Points)> OpinionFactors(int from, int about)
    {
        var factors = new List<(string, double)>();
        if (from == about) return factors;
        if (AreAllied(from, about)) factors.Add(("Aliados", GameRules.AllianceOpinion));
        if (AtWar(from, about)) factors.Add(("En guerra", GameRules.AtWarOpinion));
        if (IsVassalOf(from, about)) factors.Add(("Somos su vasallo", GameRules.VassalOpinion));
        if (IsVassalOf(about, from)) factors.Add(("Nuestro vasallo", GameRules.OverlordOpinion));
        if (HavePact(from, about)) factors.Add(("Pacto de no agresión", GameRules.PactOpinion));
        if (GivesAccess(about, from)) factors.Add(("Nos deja pasar", GameRules.AccessOpinion));
        int deals = _trades.Count(t => t.Involves(from) && t.Involves(about));
        if (deals > 0) factors.Add(("Comercio", Math.Min(GameRules.MaxTradeOpinion, deals * GameRules.TradeOpinion)));
        var borders = BorderNations(from);
        if (borders.Contains(about)) factors.Add(("Frontera común", GameRules.BorderOpinion));
        // Both border a nation stronger than us: the natural reason to ally.
        if (borders.Any(c => c != about && BorderNations(about).Contains(c) && MilitaryPower(c) > MilitaryPower(from)))
            factors.Add(("Rival común", GameRules.CommonRivalOpinion));
        if (EnemiesOf(from).Any(e => AtWar(about, e.Id))) factors.Add(("Enemigo común", GameRules.CommonEnemyOpinion));
        int ruled = Players[about].Provinces.Count(id => Map.Provinces[id].CultureId == from && Map.Provinces[id].Population >= 1);
        if (ruled > 0) factors.Add(("Gobierna a nuestra gente", -Math.Min(GameRules.MaxRuledPeopleOpinion, ruled * GameRules.RuledProvinceOpinion)));
        foreach (var memory in _memories.GetValueOrDefault((from, about)) ?? [])
            if (Math.Abs(memory.Value) >= 0.5) factors.Add((memory.Reason, memory.Value));
        return factors;
    }

    /// <summary>What <paramref name="from"/> thinks of <paramref name="about"/>, from -100 to 100.</summary>
    public double Opinion(int from, int about) => Math.Clamp(OpinionFactors(from, about).Sum(f => f.Points), -100, 100);

    /// <summary>The nations whose land touches a nation's, worked out once a day.</summary>
    private HashSet<int> BorderNations(int playerId)
    {
        if (_bordersDay != Date.Days) { _borders.Clear(); _bordersDay = Date.Days; }
        if (!_borders.TryGetValue(playerId, out var set)) _borders[playerId] = set = NeighbourNations(Players[playerId]);
        return set;
    }

    private readonly Dictionary<int, HashSet<int>> _borders = [];
    private long _bordersDay = -1;

    /// <summary><paramref name="from"/> remembers something about <paramref name="about"/>; the same reason adds up, within ±100.</summary>
    internal void Remember(int from, int about, string reason, double value)
    {
        if (from == about) return;
        if (!_memories.TryGetValue((from, about), out var list)) _memories[(from, about)] = list = [];
        if (list.FirstOrDefault(m => m.Reason == reason) is { } memory) memory.Value = Math.Clamp(memory.Value + value, -100, 100);
        else list.Add(new OpinionMemory(reason, Math.Clamp(value, -100, 100)));
    }

    /// <summary>Every memory fades a little each day, and is forgotten once it no longer weighs anything.</summary>
    private void DailyMemories()
    {
        foreach (var list in _memories.Values)
        {
            foreach (var m in list) m.Value = m.Value > 0 ? Math.Max(0, m.Value - GameRules.OpinionFadePerDay) : Math.Min(0, m.Value + GameRules.OpinionFadePerDay);
            list.RemoveAll(m => m.Value == 0);
        }
    }

    // ------------------------------------------------------------------ gifts

    /// <summary>What a gift costs: a month of the nation's gold income, never less than the minimum.</summary>
    public static double GiftCost(Player player) =>
        Math.Max(GameRules.MinGiftGold, Math.Ceiling(player.LastDayNet[(int)ResourceType.Gold] * GameRules.GiftIncomeDays));

    /// <summary>Sends gold to another nation, which thinks better of the sender for a while.</summary>
    public CommandResult SendGift(int playerId, int targetId)
    {
        if (playerId == targetId || targetId < 0 || targetId >= Players.Count || Players[targetId].Eliminated) return CommandResult.Fail("Nación no válida.");
        if (AtWar(playerId, targetId)) return CommandResult.Fail("No se hacen regalos al enemigo.");
        double gold = GiftCost(Players[playerId]);
        if (Players[playerId].Stockpile[ResourceType.Gold] < gold) return CommandResult.Fail($"Un regalo cuesta {gold:0} de oro.");
        Players[playerId].Stockpile[ResourceType.Gold] -= gold;
        Players[targetId].Stockpile[ResourceType.Gold] += gold;
        Remember(targetId, playerId, "Regalos", GameRules.GiftOpinion);
        Raise(GameEventKind.GiftSent, playerId, targetId);
        if (targetId == HumanPlayerId) Notify(HumanPlayerId, $"{Players[playerId].Name} nos envía {gold:0} de oro.");
        return CommandResult.Success($"Regalo enviado a {Players[targetId].Name}: su opinión de nosotros sube.");
    }

    // ------------------------------------------------------------------ alliances

    public CommandResult CanProposeAlliance(int playerId, int targetId)
    {
        if (playerId == targetId || targetId < 0 || targetId >= Players.Count || Players[targetId].Eliminated) return CommandResult.Fail("Nación no válida.");
        if (AreAllied(playerId, targetId)) return CommandResult.Fail($"Ya sois aliados de {Players[targetId].Name}.");
        if (OverlordOf(playerId) is int overlord) return CommandResult.Fail($"Eres vasallo de {Players[overlord].Name}: no puedes aliarte con nadie.");
        if (OverlordOf(targetId) is int theirs) return CommandResult.Fail($"{Players[targetId].Name} es vasallo de {Players[theirs].Name}.");
        if (AtWar(playerId, targetId)) return CommandResult.Fail($"Estás en guerra con {Players[targetId].Name}.");
        if (AlliesOf(playerId).Count() >= GameRules.MaxAllies) return CommandResult.Fail($"No puedes tener más de {GameRules.MaxAllies} aliados.");
        return CommandResult.Success();
    }

    /// <summary>
    /// Offers an alliance. A computer rival accepts if it thinks well enough of the proposer
    /// (<see cref="GameRules.AllianceAcceptOpinion"/>) and is not at war with any of its allies; a human is never forced.
    /// </summary>
    public CommandResult ProposeAlliance(int playerId, int targetId)
    {
        var check = CanProposeAlliance(playerId, targetId);
        if (!check.Ok) return check;
        var ai = _ais.FirstOrDefault(a => a.PlayerId == targetId);
        if (ai == null || !ai.WouldAlly(playerId))
            return CommandResult.Fail($"{Players[targetId].Name} rechaza la alianza (su opinión de nosotros: {Opinion(targetId, playerId):0}; pide {GameRules.AllianceAcceptOpinion:0}).");
        _alliances.Add(WarKey(playerId, targetId));
        foreach (int id in new[] { playerId, targetId }.Where(id => id == HumanPlayerId))
            Notify(id, $"Alianza firmada con {Players[id == playerId ? targetId : playerId].Name}.");
        return CommandResult.Success($"Alianza firmada con {Players[targetId].Name}.");
    }

    /// <summary>Breaks an alliance; the former ally will not forget it soon.</summary>
    public CommandResult BreakAlliance(int playerId, int targetId)
    {
        if (!AreAllied(playerId, targetId)) return CommandResult.Fail($"No sois aliados de {Players[targetId].Name}.");
        _alliances.Remove(WarKey(playerId, targetId));
        Remember(targetId, playerId, "Rompió la alianza", GameRules.BrokenAllianceOpinion);
        Evict(playerId, targetId);
        Evict(targetId, playerId);
        if (targetId == HumanPlayerId) Notify(HumanPlayerId, $"{Players[playerId].Name} rompe su alianza con nosotros.");
        return CommandResult.Success($"Alianza con {Players[targetId].Name} rota.");
    }

    /// <summary>
    /// The defender's allies come to its aid: each one at peace with the attacker and not allied with it declares war
    /// on it too (truces do not hold them back). Returns those who joined.
    /// </summary>
    private List<Player> CallAllies(int attackerId, int defenderId)
    {
        var joined = new List<Player>();
        foreach (var ally in AlliesOf(defenderId).ToList())
        {
            // A pact with the attacker keeps it out of the war.
            if (ally.Id == attackerId || AtWar(ally.Id, attackerId) || HavePact(ally.Id, attackerId)) continue;
            if (AreAllied(ally.Id, attackerId))
            {
                // Allied with both: it stays out, and the attacker loses it as an ally.
                _alliances.Remove(WarKey(ally.Id, attackerId));
                Remember(ally.Id, attackerId, "Atacó a nuestro aliado", GameRules.BrokenAllianceOpinion / 2);
                continue;
            }
            StartWar(ally.Id, attackerId);
            Remember(attackerId, ally.Id, "Nos declaró la guerra", GameRules.DeclaredWarOpinion / 2);
            joined.Add(ally);
            Raise(GameEventKind.WarDeclared, ally.Id, attackerId);
            if (ally.Id == HumanPlayerId) Notify(HumanPlayerId, $"Entramos en guerra con {Players[attackerId].Name} para defender a nuestro aliado {Players[defenderId].Name}.");
            else if (attackerId == HumanPlayerId) Notify(HumanPlayerId, $"¡{ally.Name}, aliada de {Players[defenderId].Name}, nos declara la guerra!");
            else if (defenderId == HumanPlayerId) Notify(HumanPlayerId, $"{ally.Name} entra en la guerra a nuestro lado.");
        }
        return joined;
    }
}
