using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>The events that ask a nation to choose.</summary>
public enum DecisionKind
{
    Drought,
    GoldVein,
    Refugees,
    Bandits,
    Harvest,
    Inventor,
}

/// <summary>
/// An event waiting for its nation's answer: what happened, where, how much is at stake (<see cref="Scale"/>, from the
/// nation's size when it happened) and when its default answer is taken.
/// </summary>
public sealed record Decision(int Id, DecisionKind Kind, int PlayerId, int ProvinceId, double Scale, long ExpiresHours);

/// <summary>One answer to a decision: its label, what it does, and what it costs, if anything.</summary>
public sealed record DecisionOption(string Label, string Effect, ResourceCost? Cost);

/// <summary>Mood that a decision left in a province, for a time.</summary>
public sealed record MoodEvent(int ProvinceId, string Reason, double Points, long UntilHours);

/// <summary>
/// Events with decisions. Now and then something happens in one of a nation's provinces (a drought, a gold vein,
/// refugees at the border...) and its ruler chooses between two answers, each with its price. Rivals answer at once;
/// the human has <see cref="GameRules.DecisionDays"/> days, after which the default answer (the first) is taken.
/// </summary>
public sealed partial class GameSession
{
    private readonly List<Decision> _decisions = [];
    private readonly List<MoodEvent> _moodEvents = [];
    private int _nextDecisionId = 1;
    private Random? _decisionRandom;

    /// <summary>Decisions draw on a generator of their own, so the rest of the game keeps its course.</summary>
    private Random DecisionRandom => _decisionRandom ??= new Random(_seed ^ 0xDEC1);

    public IReadOnlyList<Decision> Decisions => _decisions;
    public IReadOnlyList<MoodEvent> MoodEvents => _moodEvents;

    public IReadOnlyList<Decision> PendingDecisions(int playerId) => [.. _decisions.Where(d => d.PlayerId == playerId)];

    public Decision? DecisionById(int id) => _decisions.FirstOrDefault(d => d.Id == id);

    /// <summary>What is at stake for a nation: grows with the square root of its people, and never below 1.</summary>
    public double DecisionScale(int playerId) =>
        Math.Max(1, Math.Sqrt(Players[playerId].Provinces.Sum(id => Map.Provinces[id].Population) / GameRules.DecisionPeople));

    /// <summary>Whether a decision of this kind can happen in the province: it needs people, and some kinds a city.</summary>
    private bool Fits(DecisionKind kind, Province p) =>
        p.Population >= 100 && !p.IsOccupied && (kind is not (DecisionKind.Harvest or DecisionKind.Inventor) || p.CityId.HasValue);

    public string DecisionTitle(Decision d) => d.Kind switch
    {
        DecisionKind.Drought => "Sequía",
        DecisionKind.GoldVein => "Un filón de oro",
        DecisionKind.Refugees => "Refugiados",
        DecisionKind.Bandits => "Bandidos",
        DecisionKind.Harvest => "Una gran cosecha",
        _ => "Un inventor",
    };

    public string DecisionText(Decision d)
    {
        string place = PlaceName(Map.Provinces[d.ProvinceId]);
        return d.Kind switch
        {
            DecisionKind.Drought => $"La lluvia no llega a {place} y los campos se secan. Los campesinos piden que se abran los graneros de la corona.",
            DecisionKind.GoldVein => $"Unos mineros han dado con un filón de oro cerca de {place}. ¿Para quién será?",
            DecisionKind.Refugees => $"Familias que huyen de la guerra y del hambre llegan a {place} y piden quedarse.",
            DecisionKind.Bandits => $"Una banda de bandidos asalta los caminos de {place}: roba a los viajeros y quema granjas.",
            DecisionKind.Harvest => $"Este año la cosecha de {place} ha sido la mejor que se recuerda.",
            _ => $"Un inventor de {place} asegura que, con dinero, sus máquinas cambiarán el mundo.",
        };
    }

    private static int Amount(Decision d, double baseAmount) => (int)Math.Round(baseAmount * d.Scale);

    /// <summary>The people a decision moves: a share of the province's, but at least a few hundred.</summary>
    private int People(Decision d, double share) => (int)Math.Round(Math.Max(300, Map.Provinces[d.ProvinceId].Population * share));

    /// <summary>The two answers. The first is the default, taken when nobody answers in time.</summary>
    public IReadOnlyList<DecisionOption> DecisionOptions(Decision d)
    {
        string place = PlaceName(Map.Provinces[d.ProvinceId]);
        var player = Players[d.PlayerId];
        return d.Kind switch
        {
            DecisionKind.Drought =>
            [
                new("Guardar el grano", $"-20 de moral en {place} durante un año y muere el 3 % de su gente.", null),
                new("Repartir el grano", $"+15 de moral en {place} durante un año.", new ResourceCost((ResourceType.Food, Amount(d, 100)))),
            ],
            DecisionKind.GoldVein =>
            [
                new("Para la corona", $"+{Amount(d, 150)} de oro, pero -10 de moral en {place} durante un año.", null),
                new("Para los mineros", $"+{Amount(d, 50)} de oro y +10 de moral en {place} durante un año.", null),
            ],
            DecisionKind.Refugees =>
            [
                new("Cerrar las puertas", "Se marchan a otra parte.", null),
                new("Acogerlos", $"{People(d, 0.05)} personas se instalan en {place}, que pierde 5 de moral durante un año.", null),
            ],
            DecisionKind.Bandits =>
            [
                new("Dejarlos", $"-10 de moral en {place} durante un año y se pierde el 2 % de su gente.", null),
                new("Enviar soldados", $"+5 de moral en {place} durante un año.", new ResourceCost((ResourceType.Gold, Amount(d, 40)))),
            ],
            DecisionKind.Harvest =>
            [
                new("Vender el excedente", $"+{Amount(d, 80)} de oro.", null),
                new("Celebrarla", $"{place} está de fiesta {GameRules.FestivalDays} días (+{GameRules.FestivalMood:0} de moral).", null),
            ],
            _ =>
            [
                new("Despedirlo", "No pasa nada.", null),
                new("Financiarlo", $"+{InventorScience(player):0} de ciencia para lo que investigas.", new ResourceCost((ResourceType.Gold, Amount(d, 60)))),
            ],
        };
    }

    /// <summary>The science an inventor brings: a month of the nation's research, and some at least.</summary>
    private static double InventorScience(Entities.Player player) => Math.Max(20, player.LastDayScience * 30);

    public CommandResult CanChoose(int playerId, int decisionId, int option)
    {
        if (DecisionById(decisionId) is not { } d || d.PlayerId != playerId) return CommandResult.Fail("Esa decisión ya no está pendiente.");
        var options = DecisionOptions(d);
        if (option < 0 || option >= options.Count) return CommandResult.Fail("Esa respuesta no existe.");
        if (options[option].Cost is { } cost && !Players[playerId].Stockpile.Has(cost)) return CommandResult.Fail($"Necesitas {cost}.");
        return CommandResult.Success();
    }

    /// <summary>Answers a decision: pays its cost and applies what it does.</summary>
    public CommandResult Choose(int playerId, int decisionId, int option)
    {
        var can = CanChoose(playerId, decisionId, option);
        if (!can.Ok) return can;
        var d = DecisionById(decisionId)!;
        var chosen = DecisionOptions(d)[option];
        if (chosen.Cost is { } cost) Players[playerId].Stockpile.TrySpend(cost);
        Apply(d, option);
        _decisions.Remove(d);
        return CommandResult.Success($"{DecisionTitle(d)}: {chosen.Label.ToLowerInvariant()}.");
    }

    private void Apply(Decision d, int option)
    {
        var p = Map.Provinces[d.ProvinceId];
        var player = Players[d.PlayerId];
        string title = DecisionTitle(d);
        switch (d.Kind, option)
        {
            case (DecisionKind.Drought, 0):
                AddMoodEvent(p, title, -20);
                p.Population *= 0.97;
                break;
            case (DecisionKind.Drought, _):
                AddMoodEvent(p, title, 15);
                break;
            case (DecisionKind.GoldVein, 0):
                player.Stockpile[ResourceType.Gold] += Amount(d, 150);
                AddMoodEvent(p, title, -10);
                break;
            case (DecisionKind.GoldVein, _):
                player.Stockpile[ResourceType.Gold] += Amount(d, 50);
                AddMoodEvent(p, title, 10);
                break;
            case (DecisionKind.Refugees, 0):
                break;
            case (DecisionKind.Refugees, _):
                p.Population += People(d, 0.05);
                AddMoodEvent(p, title, -5);
                break;
            case (DecisionKind.Bandits, 0):
                AddMoodEvent(p, title, -10);
                p.Population *= 0.98;
                break;
            case (DecisionKind.Bandits, _):
                AddMoodEvent(p, title, 5);
                break;
            case (DecisionKind.Harvest, 0):
                player.Stockpile[ResourceType.Gold] += Amount(d, 80);
                break;
            case (DecisionKind.Harvest, _):
                if (CityIn(p) is { } city) city.FestivalUntilHours = Math.Max(city.FestivalUntilHours, Date.Hours) + GameRules.FestivalDays * 24;
                break;
            case (DecisionKind.Inventor, 0):
                break;
            case (DecisionKind.Inventor, _):
                player.SpareScience += InventorScience(player);
                break;
        }
    }

    /// <summary>Mood that the province keeps for <see cref="GameRules.DecisionMoodDays"/> days; a new one of the same reason replaces the old.</summary>
    private void AddMoodEvent(Province p, string reason, double points)
    {
        _moodEvents.RemoveAll(m => m.ProvinceId == p.Id && m.Reason == reason);
        _moodEvents.Add(new MoodEvent(p.Id, reason, points, Date.Hours + GameRules.DecisionMoodDays * 24L));
    }

    /// <summary>The mood events still running in the province, for <see cref="MoodFactors"/>.</summary>
    private IEnumerable<MoodEvent> MoodEventsIn(Province p) => _moodEvents.Where(m => m.ProvinceId == p.Id && m.UntilHours > Date.Hours);

    /// <summary>
    /// Each day: mood events that have run out end, unanswered decisions past their time take their default, and some
    /// nations meet a new event. Rivals answer theirs at once.
    /// </summary>
    private void DailyDecisions()
    {
        _moodEvents.RemoveAll(m => m.UntilHours <= Date.Hours);
        foreach (var d in _decisions.Where(d => d.ExpiresHours <= Date.Hours || Players[d.PlayerId].Eliminated).ToList())
        {
            _decisions.Remove(d);
            if (Players[d.PlayerId].Eliminated) continue;
            Apply(d, 0);
            if (d.PlayerId == HumanPlayerId)
                Notify(HumanPlayerId, $"{DecisionTitle(d)} en {PlaceName(Map.Provinces[d.ProvinceId])}: sin respuesta, se ha optado por «{DecisionOptions(d)[0].Label.ToLowerInvariant()}».");
        }
        foreach (var player in Players.Where(p => !p.Eliminated && p.CapitalCityId.HasValue))
        {
            if (DecisionRandom.NextDouble() >= GameRules.DecisionsPerYear / 365) continue;
            var kind = (DecisionKind)DecisionRandom.Next(Enum.GetValues<DecisionKind>().Length);
            var places = player.Provinces.Select(id => Map.Provinces[id]).Where(p => Fits(kind, p)).ToList();
            if (places.Count == 0) continue;
            var d = StartDecision(player.Id, kind, places[DecisionRandom.Next(places.Count)].Id);
            if (!player.IsHuman) Choose(player.Id, d.Id, AiAnswer(d));
        }
    }

    /// <summary>A nation meets an event in one of its provinces.</summary>
    public Decision StartDecision(int playerId, DecisionKind kind, int provinceId)
    {
        var d = new Decision(_nextDecisionId++, kind, playerId, provinceId, DecisionScale(playerId), Date.Hours + GameRules.DecisionDays * 24L);
        _decisions.Add(d);
        return d;
    }

    /// <summary>
    /// How a rival answers: it pays when it can and the price is worth it, keeps the peace in unhappy provinces, and
    /// takes the gold otherwise.
    /// </summary>
    private int AiAnswer(Decision d)
    {
        var p = Map.Provinces[d.ProvinceId];
        bool affordable = CanChoose(d.PlayerId, d.Id, 1).Ok;
        return d.Kind switch
        {
            DecisionKind.GoldVein or DecisionKind.Harvest => p.Mood < GameRules.UnrestMood + 15 ? 1 : 0,
            DecisionKind.Refugees => 1,
            _ => affordable ? 1 : 0,
        };
    }

    private void LoadDecisions(SaveGame save)
    {
        _decisions.AddRange(save.Decisions ?? []);
        _moodEvents.AddRange(save.MoodEvents ?? []);
        _nextDecisionId = Math.Max(1, save.NextDecisionId);
    }
}
