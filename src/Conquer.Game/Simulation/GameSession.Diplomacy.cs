using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>What a peace treaty settles, seen from the nation that proposes it.</summary>
public enum PeaceTerms
{
    /// <summary>Nobody gains anything: occupied provinces go back to their owners.</summary>
    White,
    /// <summary>The proposer keeps the enemy provinces it occupies.</summary>
    TakeOccupied,
    /// <summary>The proposer hands over its provinces the enemy occupies.</summary>
    CedeOccupied,
}

/// <summary>A war between two nations: when it began and the battles each side has won.</summary>
internal sealed class War(long startHours)
{
    public long StartHours { get; } = startHours;
    public Dictionary<int, int> Victories { get; } = [];
}

/// <summary>
/// War and peace between nations. Armies may only enter the land of nations they are at war with. Each war has a
/// score (occupied land and battles won) that pays for what a peace treaty takes from the loser.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>Pairs of nations at war (lower id first).</summary>
    private readonly Dictionary<(int, int), War> _wars = [];

    private static (int, int) WarKey(int a, int b) => a < b ? (a, b) : (b, a);

    public bool AtWar(int a, int b) => a != b && a >= 0 && b >= 0 && _wars.ContainsKey(WarKey(a, b));

    public IEnumerable<Player> EnemiesOf(int playerId) => Players.Where(p => AtWar(playerId, p.Id));

    /// <summary>Days since the war between two nations began (0 at peace).</summary>
    public double WarDays(int a, int b) => _wars.TryGetValue(WarKey(a, b), out var war) ? (Date.Hours - war.StartHours) / 24.0 : 0;

    /// <summary>Battles a nation has won in its war with another (0 at peace).</summary>
    public int WarVictories(int playerId, int enemyId) =>
        _wars.TryGetValue(WarKey(playerId, enemyId), out var war) ? war.Victories.GetValueOrDefault(playerId) : 0;

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
        _wars[WarKey(playerId, targetId)] = new War(Date.Hours);
        if (playerId == HumanPlayerId) Notify(HumanPlayerId, $"Declaramos la guerra a {Players[targetId].Name}.");
        else if (targetId == HumanPlayerId) Notify(HumanPlayerId, $"¡{Players[playerId].Name} nos declara la guerra!");
        return CommandResult.Success();
    }

    /// <summary>Counts a battle won in the war between the winner and the loser.</summary>
    private void RecordVictory(int winnerId, int loserId)
    {
        if (_wars.TryGetValue(WarKey(winnerId, loserId), out var war))
            war.Victories[winnerId] = war.Victories.GetValueOrDefault(winnerId) + 1;
    }

    // ------------------------------------------------------------------ war score

    /// <summary>
    /// What a province is worth at the peace table: a little for the land, more for its people, a city and above all
    /// the capital.
    /// </summary>
    public double ProvinceValue(Province p)
    {
        double value = 1 + Math.Min(GameRules.MaxPopulationValue, p.Population / GameRules.PeoplePerValuePoint);
        if (CityIn(p) is { } city)
            value += Players[city.OwnerId].CapitalCityId == city.Id ? GameRules.CapitalValue : GameRules.CityValue;
        return value;
    }

    /// <summary>Share (0 to 100) of a nation's worth that its enemy occupies.</summary>
    private double OccupiedShare(int ownerId, int occupierId)
    {
        double total = 0, held = 0;
        foreach (int id in Players[ownerId].Provinces)
        {
            var p = Map.Provinces[id];
            double value = ProvinceValue(p);
            total += value;
            if (p.ControllerId == occupierId) held += value;
        }
        return total <= 0 ? 0 : 100 * held / total;
    }

    /// <summary>
    /// How the war goes for <paramref name="playerId"/>, from -100 (lost) to 100 (won): the share of the enemy's worth
    /// it occupies less the share of its own the enemy occupies, plus a few points per battle won (and minus per lost).
    /// </summary>
    public double WarScore(int playerId, int enemyId)
    {
        if (!AtWar(playerId, enemyId)) return 0;
        double occupation = OccupiedShare(enemyId, playerId) - OccupiedShare(playerId, enemyId);
        double battles = Math.Clamp(GameRules.WarScorePerVictory * (WarVictories(playerId, enemyId) - WarVictories(enemyId, playerId)),
            -GameRules.MaxBattleWarScore, GameRules.MaxBattleWarScore);
        return Math.Clamp(occupation + battles, -100, 100);
    }

    /// <summary>The enemy's provinces a nation occupies and could keep with the peace.</summary>
    public List<Province> OccupiedBy(int occupierId, int ownerId) =>
        [.. Players[ownerId].Provinces.Select(id => Map.Provinces[id]).Where(p => p.ControllerId == occupierId)];

    /// <summary>War score the terms cost the proposer: keeping occupied land costs its share of the enemy's worth.</summary>
    public double PeaceCost(int playerId, int enemyId, PeaceTerms terms) =>
        terms == PeaceTerms.TakeOccupied ? OccupiedShare(enemyId, playerId) : 0;

    /// <summary>Whether a nation may propose these terms at all (not whether the enemy will accept them).</summary>
    public CommandResult CanProposePeace(int playerId, int targetId, PeaceTerms terms)
    {
        if (!AtWar(playerId, targetId)) return CommandResult.Fail($"No estás en guerra con {Players[targetId].Name}.");
        switch (terms)
        {
            case PeaceTerms.TakeOccupied:
                if (OccupiedBy(playerId, targetId).Count == 0) return CommandResult.Fail($"No ocupas ninguna provincia de {Players[targetId].Name}.");
                double cost = PeaceCost(playerId, targetId, terms), score = WarScore(playerId, targetId);
                if (score < cost) return CommandResult.Fail($"Quedarte con lo ocupado cuesta {cost:0} de puntuación de guerra y tienes {score:0}.");
                break;
            case PeaceTerms.CedeOccupied:
                if (OccupiedBy(targetId, playerId).Count == 0) return CommandResult.Fail($"{Players[targetId].Name} no ocupa ninguna provincia tuya.");
                break;
        }
        return CommandResult.Success();
    }

    /// <summary>
    /// Offers peace to an enemy on the given terms. A computer rival accepts what the war score pays for, or white
    /// peace if the war is going badly for it or has dragged on; a human player is never forced into it.
    /// </summary>
    public CommandResult ProposePeace(int playerId, int targetId, PeaceTerms terms = PeaceTerms.White)
    {
        var check = CanProposePeace(playerId, targetId, terms);
        if (!check.Ok) return check;
        var ai = _ais.FirstOrDefault(a => a.PlayerId == targetId);
        if (ai == null || !ai.WouldAcceptPeace(playerId, terms)) return CommandResult.Fail($"{Players[targetId].Name} rechaza la paz.");
        var (gained, lost) = MakePeace(playerId, targetId, terms);
        string detail = gained > 0 ? $" Ganamos {Provinces(gained)}." : lost > 0 ? $" Cedemos {Provinces(lost)}." : "";
        return CommandResult.Success($"Paz firmada con {Players[targetId].Name}.{detail}");
    }

    private static string Provinces(int count) => count == 1 ? "1 provincia" : $"{count} provincias";

    /// <summary>
    /// Ends a war: battles between the two stop, the provinces the terms hand over change owner, every other occupied
    /// province goes back to its owner and the armies standing in the other's land walk back to their nearest
    /// province. Returns how many provinces <paramref name="a"/> gained and lost.
    /// </summary>
    internal (int Gained, int Lost) MakePeace(int a, int b, PeaceTerms terms = PeaceTerms.White)
    {
        var ceded = terms switch
        {
            PeaceTerms.TakeOccupied => OccupiedBy(a, b),
            PeaceTerms.CedeOccupied => OccupiedBy(b, a),
            _ => [],
        };
        int receiver = terms == PeaceTerms.TakeOccupied ? a : b;
        int giver = receiver == a ? b : a;
        _wars.Remove(WarKey(a, b));
        foreach (var battle in _battles.Where(x => WarKey(x.AttackerId, x.DefenderId) == WarKey(a, b)).ToList())
        {
            foreach (var unit in battle.Attackers.Select(UnitById).OfType<Unit>()) unit.AttackingProvinceId = null;
            _battles.Remove(battle);
        }
        foreach (var p in ceded) Cede(p, receiver);
        foreach (var p in Map.Provinces.Where(p => p.IsOccupied && WarKey(p.OwnerId, p.ControllerId) == WarKey(a, b)))
        {
            p.ControllerId = p.OwnerId;
            OwnershipChanged?.Invoke(p.Id);
        }
        foreach (var unit in Units.ToList())
        {
            var here = Map.Provinces[unit.ProvinceId];
            if ((unit.OwnerId == a || unit.OwnerId == b) && here.IsOwned && here.ControllerId != unit.OwnerId) SendHome(unit);
        }

        foreach (int id in new[] { a, b }.Where(id => id == HumanPlayerId))
        {
            int other = a == id ? b : a;
            string detail = ceded.Count == 0 ? "" : receiver == id ? $": ganamos {Provinces(ceded.Count)}" : $": cedemos {Provinces(ceded.Count)}";
            Notify(id, $"Paz firmada con {Players[other].Name}{detail}.");
        }
        if (ceded.Count > 0 && Players[giver].Provinces.Count == 0)
            Notify(HumanPlayerId, giver == HumanPlayerId ? "Hemos perdido todas nuestras tierras." : $"{Players[giver].Name} ha sido anexionada por {Players[receiver].Name}.");
        else if (ceded.Count > 0 && a != HumanPlayerId && b != HumanPlayerId)
            Notify(HumanPlayerId, $"{Players[giver].Name} cede {Provinces(ceded.Count)} a {Players[receiver].Name}.");
        return receiver == a ? (ceded.Count, 0) : (0, ceded.Count);
    }

    /// <summary>
    /// A province changes owner by treaty, with its city and buildings. What was being trained or built there is lost,
    /// the people resent their new rulers, and a capital handed over is replaced by the old owner's largest city.
    /// </summary>
    private void Cede(Province p, int receiverId)
    {
        var giver = Players[p.OwnerId];
        p.Training.Clear();
        p.Constructing = null;
        p.ConstructionDaysLeft = 0;
        p.Mood = Math.Max(0, p.Mood - GameRules.CededMoodPenalty);
        SetOwner(p, receiverId);
        if (CityIn(p) is not { } city) return;
        city.OwnerId = receiverId;
        city.FestivalUntilHours = 0;
        if (giver.CapitalCityId == city.Id)
            giver.CapitalCityId = Cities.Where(c => c.OwnerId == giver.Id)
                .OrderByDescending(c => Map.Provinces[c.ProvinceId].Population).Select(c => (int?)c.Id).FirstOrDefault();
        Players[receiverId].CapitalCityId ??= city.Id;
    }
}
