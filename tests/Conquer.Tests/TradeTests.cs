using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>Trade deals between nations, and the Comercio tab.</summary>
[Collection("World")]
public class TradeTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>The rival gains 10 copper a day with 100 in store; the human gains 20 gold a day with 100 in store.</summary>
    private static void CopperForGold(GameSession s)
    {
        var rival = s.Players[1];
        rival.LastDayNet[(int)ResourceType.Copper] = 10;
        rival.Stockpile[ResourceType.Copper] = 100;
        rival.Stockpile[ResourceType.Gold] = 0;
        s.Human.LastDayNet[(int)ResourceType.Gold] = 20;
        s.Human.Stockpile[ResourceType.Gold] = 100;
        s.Human.Stockpile[ResourceType.Copper] = 0;
    }

    [Fact]
    public void ARivalSellsHalfItsSurplusAtItsValuePlusAMargin()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        CopperForGold(s);
        var offer = s.TradeOffer(0, 1, ResourceType.Copper, ResourceType.Gold)!;
        Assert.Equal(5, offer.GoodsPerDay, 6);
        double price = 5 * GameRules.ResourceValue(ResourceType.Copper) * (1 + GameRules.TradeMargin(0));
        Assert.Equal(Math.Round(price, 2), offer.PaymentPerDay, 6);

        // Gold it would rather keep for itself: the human can spare only half its income, so the deal shrinks to fit.
        s.Human.LastDayNet[(int)ResourceType.Gold] = 4;
        Assert.Equal(2, s.TradeOffer(0, 1, ResourceType.Copper, ResourceType.Gold)!.PaymentPerDay, 6);
    }

    [Fact]
    public void ADealDeliversEveryDayUntilAWarBreaksIt()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        CopperForGold(s);
        double pay = s.TradeOffer(0, 1, ResourceType.Copper, ResourceType.Gold)!.PaymentPerDay;
        Assert.True(s.Trade(0, 1, ResourceType.Copper, ResourceType.Gold).Ok);
        Assert.False(s.CanTrade(0, 1, ResourceType.Copper, ResourceType.Gold).Ok); // one deal per resource
        Assert.Contains(s.OpinionFactors(1, 0), f => f.Reason == "Comercio");

        for (int h = 0; h < 24; h++) s.Step();
        Assert.Equal(95, s.Players[1].Stockpile[ResourceType.Copper], 6);
        Assert.Equal(5, s.Human.Stockpile[ResourceType.Copper], 6);
        Assert.Equal(pay, s.Players[1].Stockpile[ResourceType.Gold], 6);
        Assert.Single(GameSession.Load(_map, s.ToSave("test")).Trades);

        s.DeclareWar(0, 1);
        for (int h = 0; h < 24; h++) s.Step();
        Assert.Empty(s.Trades);
    }

    [Fact]
    public void CancellingADealIsResented()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        CopperForGold(s);
        s.Trade(0, 1, ResourceType.Copper, ResourceType.Gold);
        Assert.True(s.CancelTrade(0, s.Trades[0].Id).Ok);
        Assert.Empty(s.Trades);
        Assert.Contains(s.OpinionFactors(1, 0), f => f.Reason == "Canceló un acuerdo comercial");
    }

    [Fact]
    public void TheTradeTabOffersWhatRivalsCanSpareAndSignsIt()
    {
        var game = new GameController(GameSession.Create(_map, 2, seed: 7));
        CopperForGold(game.Session);
        game.Nation.Visible = true;
        game.Nation.Tab = NationTab.Trade;
        var row = ((TablePage)game.Nation.Page()).Table.Rows.First(r => r.OfType<TextCell>().Any(c => c.Text == "Te vende")
                                                                       && r.OfType<TextCell>().Any(c => c.Text.Contains("cobre")));
        var sign = row.OfType<ButtonsCell>().Single().Buttons.Single();
        Assert.True(sign.Enabled);
        sign.Press();
        Assert.Single(game.Session.TradesOf(0));
        var first = ((TablePage)game.Nation.Page()).Table.Rows[0];
        Assert.Equal("Cancelar", first.OfType<ButtonsCell>().Single().Buttons.Single().Text);
    }
}
