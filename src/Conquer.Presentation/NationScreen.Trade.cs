using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>The Comercio tab of the nation screen: the player's trade deals, then what every nation at peace offers.</summary>
public sealed partial class NationScreen
{
    /// <summary>
    /// The deals under way (with the days left and a button to cancel each), then the offers: what each nation at peace
    /// can spare, for gold or for something we have to spare and it lacks, and what of ours it would buy for gold.
    /// </summary>
    private TablePage Trade()
    {
        Column[] columns = [new("Nación", 160), new("Das", 230), new("Recibes", 230), new("Estado", 210), new("", 130)];
        var rows = new List<IReadOnlyList<Cell>>();
        foreach (var deal in Session.TradesOf(Player.Id))
        {
            var other = Session.Players[deal.SellerId == Player.Id ? deal.BuyerId : deal.SellerId];
            bool selling = deal.SellerId == Player.Id;
            rows.Add(
            [
                new TextCell(other.Name, Bold: true, Swatch: other.Color),
                Amount(selling ? deal.Goods : deal.Payment, selling ? deal.GoodsPerDay : deal.PaymentPerDay),
                Amount(selling ? deal.Payment : deal.Goods, selling ? deal.PaymentPerDay : deal.GoodsPerDay),
                new TextCell($"En curso, quedan {Math.Ceiling((deal.UntilHours - Session.Date.Hours) / 24.0):0} d", Tone.Good, TextSize.Small),
                new ButtonsCell([new Button("Cancelar", () => Show(Session.CancelTrade(Player.Id, deal.Id)),
                    Tooltip: $"Termina el acuerdo ya. {other.Name} lo recordará ({GameRules.CancelledTradeOpinion:0} de opinión).", Size: TextSize.Small)]),
            ]);
        }
        foreach (var other in Session.Players.Where(p => p.Id != Player.Id && !p.Eliminated && !Session.AtWar(Player.Id, p.Id)))
            foreach (var (goods, payment, buying) in Offers(other))
                if (OfferRow(other, goods, payment, buying) is { } row) rows.Add(row);

        string title = $"{TextFormat.Plural(Session.TradesOf(Player.Id).Count(), "acuerdo", "acuerdos")} de {GameRules.MaxTradeDeals} · "
                       + $"cada nación vende la mitad de lo que gana al día de un recurso; los rivales se quedan un margen que baja cuanto mejor te ven";
        return new TablePage(new Table(columns, rows, Empty: "Nadie tiene nada que comerciar contigo ahora. Vuelve cuando las naciones produzcan de más."), title);
    }

    /// <summary>
    /// What can be traded with a nation: each resource it has to spare, for gold and for the resource of ours it most
    /// lacks; and each resource we have to spare that it lacks, for its gold.
    /// </summary>
    private IEnumerable<(ResourceType Goods, ResourceType Payment, bool Buying)> Offers(Player other)
    {
        var known = Resources.All.Where(r => Player.Knows(r) && other.Knows(r)).ToList();
        var barter = known.Where(r => r != ResourceType.Gold && GameSession.Surplus(Player, r) > 0 && other.LastDayNet[(int)r] <= 0)
            .OrderByDescending(r => GameSession.Surplus(Player, r) * GameRules.ResourceValue(r)).ToList();
        foreach (var goods in known.Where(r => r != ResourceType.Gold && GameSession.Surplus(other, r) > 0))
        {
            yield return (goods, ResourceType.Gold, true);
            if (barter.FirstOrDefault(r => r != goods) is var payment && payment != ResourceType.Gold && barter.Contains(payment))
                yield return (goods, payment, true);
        }
        foreach (var goods in barter) yield return (goods, ResourceType.Gold, false);
    }

    /// <summary>An offer as a row, or null if there is too little to trade; the button explains why it cannot be signed.</summary>
    private IReadOnlyList<Cell>? OfferRow(Player other, ResourceType goods, ResourceType payment, bool buying)
    {
        int buyer = buying ? Player.Id : other.Id, seller = buying ? other.Id : Player.Id;
        if (Session.TradeOffer(buyer, seller, goods, payment) is not { } offer) return null;
        var can = Session.CanTrade(buyer, seller, goods, payment);
        double margin = GameRules.TradeMargin(Session.Opinion(other.Id, Player.Id));
        string tip = $"Durante {GameRules.TradeDealDays} días. {other.Name} se queda un margen del {margin:P0} (su opinión de nosotros: "
                     + $"{Session.Opinion(other.Id, Player.Id):0}). El comercio sube la opinión de los dos {GameRules.TradeOpinion:0} por acuerdo."
                     + (can.Ok ? "" : $"\n{can.Message}");
        return
        [
            new TextCell(other.Name, Swatch: other.Color),
            Amount(buying ? payment : goods, buying ? offer.PaymentPerDay : offer.GoodsPerDay),
            Amount(buying ? goods : payment, buying ? offer.GoodsPerDay : offer.PaymentPerDay),
            new TextCell(buying ? "Te vende" : "Te compra", Tone.Dim, TextSize.Small),
            new ButtonsCell([new Button("Firmar", () => Show(Session.Trade(buyer, seller, goods, payment)), can.Ok, Tooltip: tip, Size: TextSize.Small)]),
        ];
    }

    private static TextCell Amount(ResourceType resource, double perDay) =>
        new($"{perDay:0.##} de {resource.Name().ToLowerInvariant()} al día", Tone.Normal, TextSize.Small,
            Tooltip: $"Vale {perDay * GameRules.ResourceValue(resource):0.#} de oro al día ({resource.Name()}: {GameRules.ResourceValue(resource):0.##} de oro la unidad).");
}
