using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Rules;

namespace Conquer.Game.Simulation;

/// <summary>
/// A trade deal: each day until <see cref="UntilHours"/> the seller hands over <see cref="GoodsPerDay"/> of
/// <see cref="Goods"/> and the buyer pays <see cref="PaymentPerDay"/> of <see cref="Payment"/> (gold, or another resource).
/// </summary>
public sealed record TradeDeal(int Id, int SellerId, int BuyerId, ResourceType Goods, double GoodsPerDay, ResourceType Payment, double PaymentPerDay, long UntilHours)
{
    public bool Involves(int playerId) => SellerId == playerId || BuyerId == playerId;
}

/// <summary>
/// Trade in resources between nations at peace. A nation can spare half of what it gains of a resource each day; it sells
/// it for gold or for another resource at its gold value, and a computer rival keeps a margin that shrinks the better it
/// thinks of the other side. Deals run for a year and lapse if either side cannot deliver.
/// </summary>
public sealed partial class GameSession
{
    private readonly List<TradeDeal> _trades = [];
    private int _nextTradeId;

    public IReadOnlyList<TradeDeal> Trades => _trades;

    public IEnumerable<TradeDeal> TradesOf(int playerId) => _trades.Where(t => t.Involves(playerId));

    /// <summary>What a nation can spare of a resource each day: half of what it gained of it the last day.</summary>
    public static double Surplus(Player player, ResourceType resource) =>
        Math.Max(0, player.LastDayNet[(int)resource]) * GameRules.TradeSurplusShare;

    /// <summary>
    /// The deal the seller would offer: its surplus of the goods, paid at their value plus the computer side's margin,
    /// and cut down to what the buyer can spare of the payment. Null when it would be worth too little.
    /// </summary>
    public TradeDeal? TradeOffer(int buyerId, int sellerId, ResourceType goods, ResourceType payment)
    {
        if (buyerId == sellerId || goods == payment) return null;
        double perDay = Surplus(Players[sellerId], goods);
        double factor = !Players[sellerId].IsHuman ? 1 + GameRules.TradeMargin(Opinion(sellerId, buyerId))
            : !Players[buyerId].IsHuman ? 1 - GameRules.TradeMargin(Opinion(buyerId, sellerId))
            : 1;
        double price = perDay * GameRules.ResourceValue(goods) * factor / GameRules.ResourceValue(payment);
        double means = Surplus(Players[buyerId], payment);
        if (price > means)
        {
            if (price <= 0) return null;
            perDay *= means / price;
            price = means;
        }
        if (perDay * GameRules.ResourceValue(goods) < GameRules.MinTradeGoldPerDay) return null;
        return new TradeDeal(-1, sellerId, buyerId, goods, Math.Round(perDay, 2), payment, Math.Round(price, 2), Date.Hours + GameRules.TradeDealDays * 24L);
    }

    public CommandResult CanTrade(int buyerId, int sellerId, ResourceType goods, ResourceType payment)
    {
        if (buyerId == sellerId || Players[buyerId].Eliminated || Players[sellerId].Eliminated) return CommandResult.Fail("Nación no válida.");
        int other = Players[buyerId].IsHuman ? sellerId : buyerId;
        if (AtWar(buyerId, sellerId)) return CommandResult.Fail("No se comercia con el enemigo.");
        if (!Players[buyerId].Knows(goods) || !Players[sellerId].Knows(payment)) return CommandResult.Fail("Hace falta conocer los dos recursos.");
        if (_trades.Any(t => t.SellerId == sellerId && t.BuyerId == buyerId && t.Goods == goods))
            return CommandResult.Fail($"Ya hay un acuerdo de {goods.Name().ToLowerInvariant()}.");
        if (TradesOf(buyerId).Count() >= GameRules.MaxTradeDeals || TradesOf(sellerId).Count() >= GameRules.MaxTradeDeals)
            return CommandResult.Fail($"Cada nación tiene como mucho {GameRules.MaxTradeDeals} acuerdos comerciales.");
        if (!Players[other].IsHuman && Opinion(other, other == buyerId ? sellerId : buyerId) < GameRules.TradeAcceptOpinion)
            return CommandResult.Fail($"{Players[other].Name} no quiere comerciar con nosotros (pide {GameRules.TradeAcceptOpinion:0} de opinión).");
        if (TradeOffer(buyerId, sellerId, goods, payment) is not { } offer) return CommandResult.Fail("No hay suficiente que comerciar.");
        if (Players[buyerId].Stockpile[payment] < offer.PaymentPerDay) return CommandResult.Fail($"No hay {payment.Name().ToLowerInvariant()} para pagar.");
        return CommandResult.Success();
    }

    /// <summary>Signs the deal the seller offers (see <see cref="TradeOffer"/>) for <see cref="GameRules.TradeDealDays"/> days.</summary>
    public CommandResult Trade(int buyerId, int sellerId, ResourceType goods, ResourceType payment)
    {
        var check = CanTrade(buyerId, sellerId, goods, payment);
        if (!check.Ok) return check;
        var deal = TradeOffer(buyerId, sellerId, goods, payment)! with { Id = _nextTradeId++ };
        _trades.Add(deal);
        return CommandResult.Success($"Acuerdo comercial: {Players[sellerId].Name} da {deal.GoodsPerDay:0.##} de {goods.Name().ToLowerInvariant()} al día "
                                     + $"a {Players[buyerId].Name} por {deal.PaymentPerDay:0.##} de {payment.Name().ToLowerInvariant()}, durante {GameRules.TradeDealDays} días.");
    }

    /// <summary>Ends a deal before its time; the other side resents it.</summary>
    public CommandResult CancelTrade(int playerId, int dealId)
    {
        if (_trades.FirstOrDefault(t => t.Id == dealId && t.Involves(playerId)) is not { } deal) return CommandResult.Fail("Acuerdo no válido.");
        _trades.Remove(deal);
        int other = deal.SellerId == playerId ? deal.BuyerId : deal.SellerId;
        Remember(other, playerId, "Canceló un acuerdo comercial", GameRules.CancelledTradeOpinion);
        if (other == HumanPlayerId) Notify(HumanPlayerId, $"{Players[playerId].Name} cancela su acuerdo comercial de {deal.Goods.Name().ToLowerInvariant()} con nosotros.");
        return CommandResult.Success("Acuerdo cancelado.");
    }

    /// <summary>
    /// Each day every deal delivers: the goods one way, the payment the other (also counted in each side's daily balance).
    /// Deals that have run their course, are between enemies or that a side cannot honour come to an end.
    /// </summary>
    private void DailyTrade()
    {
        foreach (var deal in _trades.ToList())
        {
            var seller = Players[deal.SellerId];
            var buyer = Players[deal.BuyerId];
            string? end = deal.UntilHours <= Date.Hours ? "ha terminado"
                : AtWar(deal.SellerId, deal.BuyerId) || seller.Eliminated || buyer.Eliminated ? "se rompe"
                : seller.Stockpile[deal.Goods] < deal.GoodsPerDay ? $"se rompe: {seller.Name} no tiene {deal.Goods.Name().ToLowerInvariant()}"
                : buyer.Stockpile[deal.Payment] < deal.PaymentPerDay ? $"se rompe: {buyer.Name} no tiene {deal.Payment.Name().ToLowerInvariant()} para pagar"
                : null;
            if (end != null)
            {
                _trades.Remove(deal);
                if (deal.Involves(HumanPlayerId)) Notify(HumanPlayerId, $"El acuerdo comercial de {deal.Goods.Name().ToLowerInvariant()} entre {seller.Name} y {buyer.Name} {end}.");
                continue;
            }
            Move(seller, buyer, deal.Goods, deal.GoodsPerDay);
            Move(buyer, seller, deal.Payment, deal.PaymentPerDay);
        }

        static void Move(Player from, Player to, ResourceType r, double amount)
        {
            from.Stockpile[r] -= amount;
            to.Stockpile[r] += amount;
            from.LastDayNet[(int)r] -= amount;
            to.LastDayNet[(int)r] += amount;
            from.Record(ResourceFlow.Exchange, r, -amount);
            to.Record(ResourceFlow.Exchange, r, amount);
        }
    }

    /// <summary>What a nation's deals bring it each day, by resource: positive what it receives, negative what it gives.</summary>
    public double[] DailyTradeBalance(int playerId)
    {
        var balance = new double[Resources.All.Length];
        foreach (var deal in TradesOf(playerId))
        {
            int sign = deal.SellerId == playerId ? -1 : 1;
            balance[(int)deal.Goods] += sign * deal.GoodsPerDay;
            balance[(int)deal.Payment] -= sign * deal.PaymentPerDay;
        }
        return balance;
    }
}
