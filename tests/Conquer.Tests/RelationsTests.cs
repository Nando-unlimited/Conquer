using Conquer.Game.Economy;
using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Opinion between nations, gifts and alliances.</summary>
[Collection("World")]
public class RelationsTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>The human and two computer rivals with gold to give; the second rival thinks well of the first.</summary>
    private GameSession WithFriends()
    {
        var s = GameSession.Create(_map, 3, seed: 7);
        foreach (var p in s.Players) p.Stockpile[ResourceType.Gold] = 10_000;
        while (s.Opinion(2, 1) < GameRules.AllianceAcceptOpinion) Assert.True(s.SendGift(1, 2).Ok);
        return s;
    }

    [Fact]
    public void ADeclarationOfWarIsRemembered()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.DeclareWar(0, 1);
        var factors = s.OpinionFactors(1, 0);
        Assert.Contains(factors, f => f.Reason == "Nos declaró la guerra" && f.Points == GameRules.DeclaredWarOpinion);
        Assert.Contains(factors, f => f.Reason == "En guerra" && f.Points == GameRules.AtWarOpinion);
        Assert.Equal(Math.Max(-100, GameRules.DeclaredWarOpinion + GameRules.AtWarOpinion), s.Opinion(1, 0), 6);
        Assert.Equal(GameRules.AtWarOpinion, s.Opinion(0, 1), 6); // the aggressor holds nothing against its victim but the war
    }

    [Fact]
    public void GiftsCostGoldAndWinFriendsThatSlowlyForget()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        s.Human.Stockpile[ResourceType.Gold] = 500;
        double cost = GameSession.GiftCost(s.Human);
        Assert.Equal(GameRules.MinGiftGold, cost);

        Assert.True(s.SendGift(0, 1).Ok);
        Assert.Equal(500 - cost, s.Human.Stockpile[ResourceType.Gold]);
        Assert.Equal(GameRules.GiftOpinion, s.Opinion(1, 0), 6);
        for (int h = 0; h < 24 * 10; h++) s.Step();
        Assert.Equal(GameRules.GiftOpinion - 10 * GameRules.OpinionFadePerDay, s.Opinion(1, 0), 6);
    }

    [Fact]
    public void AlliesComeToTheDefenceAndOutlastASave()
    {
        var s = WithFriends();
        Assert.True(s.ProposeAlliance(1, 2).Ok);
        Assert.True(s.AreAllied(1, 2));

        Assert.True(s.DeclareWar(0, 1).Ok);
        Assert.True(s.AtWar(0, 2));
        Assert.Contains(s.Notifications, n => n.Text.Contains("nos declara la guerra"));

        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.True(loaded.AreAllied(1, 2));
        Assert.Equal(s.Opinion(2, 1), loaded.Opinion(2, 1), 6);
    }

    [Fact]
    public void AlliesCrossEachOthersLandButNeverAttackEachOther()
    {
        var s = WithFriends();
        s.ProposeAlliance(1, 2);
        var land = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && p.Neighbors.Length > 2);
        var scouts = s.AddRegiment(2, land.Id, BattalionType.Scouts);
        s.Claim(2, scouts.Id);
        var regiment = s.AddRegiment(1, land.Neighbors.First(n => _map.Provinces[n].IsClaimable), BattalionType.Warriors);

        Assert.True(s.CanUnitEnter(regiment, land.Id));
        Assert.False(s.CanDeclareWar(1, 2).Ok);

        Assert.True(s.BreakAlliance(1, 2).Ok);
        Assert.False(s.CanUnitEnter(regiment, land.Id));
        Assert.Contains(s.OpinionFactors(2, 1), f => f.Reason == "Rompió la alianza");
    }

    [Fact]
    public void RivalsOnlyAllyWithNationsTheyThinkWellOf()
    {
        var s = GameSession.Create(_map, 3, seed: 7);
        Assert.False(s.ProposeAlliance(1, 2).Ok);
        Assert.False(s.AreAllied(1, 2));
    }
}
