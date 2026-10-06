using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Reparations, vassals and capitulation.</summary>
[Collection("World")]
public class TreatyTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private (Province A, Province B) Pair() =>
        _map.Provinces
            .Where(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3)
            .SelectMany(p => p.Neighbors.Select(n => (A: p, B: _map.Provinces[n])))
            .First(t => t.B.Biome == Biome.Grassland && t.B.Neighbors.Length > 3);

    /// <summary>
    /// The human's capital in A; player 1's capital in C, one province beyond its land B; player 2 owns a far province.
    /// No computer rivals, so only the test moves units.
    /// </summary>
    private (GameSession S, Province A, Province B, Province C) ThreeNations()
    {
        var s = GameSession.Create(_map, 3, seed: 7, computerRivals: false);
        var (a, b) = Pair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        var c = b.Neighbors.Select(n => _map.Provinces[n])
            .First(p => p.IsClaimable && !p.IsOwned && p.Neighbors.All(n => n != a.Id && !_map.Provinces[n].CityId.HasValue));
        Assert.True(s.FoundCity(1, s.AddUnit(1, UnitType.Settlers, c.Id, 300).Id).Ok);
        Claim(s, 1, b);
        Claim(s, 2, _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && _map.DistanceKm(p, a) > 3000));
        return (s, a, b, c);
    }

    private static void Claim(GameSession s, int playerId, Province p)
    {
        var scouts = s.AddRegiment(playerId, p.Id, BattalionType.Scouts);
        Assert.True(s.Claim(playerId, scouts.Id).Ok);
        s.Disband(playerId, scouts.Id);
    }

    private static void RunHours(GameSession s, int hours)
    {
        for (int i = 0; i < hours; i++) s.Step();
    }

    private static void RunUntil(GameSession s, Func<bool> done, int maxHours)
    {
        for (int i = 0; i < maxHours && !done(); i++) s.Step();
    }

    /// <summary>The human occupies B and C, the whole of player 1: enough war score for any treaty.</summary>
    private static void OccupyAll(GameSession s, Province a, Province c)
    {
        s.DeclareWar(0, 1);
        var regiment = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        s.MoveUnit(0, regiment.Id, c.Id);
        RunUntil(s, () => c.IsOccupied, 24 * 10);
        Assert.True(c.IsOccupied);
    }

    [Fact]
    public void ReparationsCostWarScoreAndPayPartOfTheGoldIncome()
    {
        var (s, a, b, c) = ThreeNations();
        c.Population = 3000;
        s.DeclareWar(0, 1);
        Assert.False(s.CanProposePeace(0, 1, PeaceTerms.Reparations).Ok);
        s.MakePeace(0, 1);
        RunHours(s, 1);

        s.Human.Stockpile[ResourceType.Gold] = 0;
        s.Players[1].Stockpile[ResourceType.Gold] = 1000;
        s.MakePeace(0, 1, PeaceTerms.Reparations);
        Assert.Equal(GameRules.ReparationsDays, s.ReparationsDaysLeft(1, 0), 6);
        Assert.Contains(s.OpinionFactors(1, 0), f => f.Reason == "Nos impuso reparaciones");

        RunHours(s, 24);
        Assert.True(s.DailyTribute(0) > 0);
        Assert.Equal(s.DailyTribute(0), -s.DailyTribute(1), 6);

        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(s.ReparationsDaysLeft(1, 0), loaded.ReparationsDaysLeft(1, 0), 6);
    }

    [Fact]
    public void AVassalPaysTributeFightsItsOverlordsWarsAndCanBeAnnexed()
    {
        var (s, a, b, c) = ThreeNations();
        OccupyAll(s, a, c);
        Assert.True(s.WarScore(0, 1) >= GameRules.VassalWarScore);
        Assert.True(s.CanProposePeace(0, 1, PeaceTerms.Vassalize).Ok);

        s.MakePeace(0, 1, PeaceTerms.Vassalize);
        Assert.True(s.IsVassalOf(1, 0));
        Assert.Equal(1, b.ControllerId);
        Assert.False(s.CanDeclareWar(1, 2).Ok);
        Assert.False(s.CanDeclareWar(0, 1).Ok);
        Assert.False(s.CanProposeAlliance(1, 2).Ok);
        Assert.True(s.CanUnitEnter(s.AddRegiment(0, a.Id, BattalionType.LightInfantry), b.Id));

        // Attacked, the overlord brings its vassal along.
        Assert.True(s.DeclareWar(2, 0).Ok);
        Assert.True(s.AtWar(2, 1));
        s.MakePeace(0, 2);
        Assert.False(s.AtWar(2, 1));

        Assert.False(s.CanAnnexVassal(0, 1).Ok);
        var save = s.ToSave("test") with { Vassals = [new VassalSave(1, 0, s.Date.Hours - (long)(GameRules.VassalAnnexYears * 365 * 24))] };
        var later = GameSession.Load(_map, save);
        Assert.True(later.IsVassalOf(1, 0));
        Assert.True(later.AnnexVassal(0, 1).Ok);
        Assert.True(later.Players[1].Eliminated);
        Assert.Contains(c.Id, later.Human.Provinces);
        Assert.Equal(0, later.CityIn(c)!.OwnerId);
    }

    [Fact]
    public void ANationWhoseCitiesAreAllOccupiedCapitulates()
    {
        var (s, a, b, c) = ThreeNations();
        var far = _map.Provinces.First(p => p.IsClaimable && !p.IsOwned && _map.DistanceKm(p, c) > 3000);
        var straggler = s.AddRegiment(1, far.Id, BattalionType.LightInfantry);
        s.DeclareWar(0, 1);
        var regiment = s.AddRegiment(0, a.Id, BattalionType.LightInfantry);
        s.MoveUnit(0, regiment.Id, c.Id);
        RunUntil(s, () => c.IsOccupied, 24 * 10);
        Assert.True(c.IsOccupied);
        b.ControllerId = 1; // the countryside still free

        RunHours(s, 25);
        var rival = s.Players[1];
        Assert.True(rival.Eliminated);
        Assert.Empty(rival.Provinces);
        Assert.Equal(0, b.OwnerId);
        Assert.Equal(0, c.OwnerId);
        Assert.Null(s.UnitById(straggler.Id));
        Assert.False(s.AtWar(0, 1));
        Assert.False(s.CanDeclareWar(0, 1).Ok);
        Assert.True(GameSession.Load(_map, s.ToSave("test")).Players[1].Eliminated);
    }
}
