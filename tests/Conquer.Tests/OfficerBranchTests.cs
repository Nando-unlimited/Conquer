using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>The three arms: army, navy and air force, each with its names for the same ranks and its own units.</summary>
[Collection("World")]
public class OfficerBranchTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private (GameSession S, Province Home) Nation()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var home = _map.Provinces.First(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, home.Id, 300).Id);
        s.Players[0].Stockpile[ResourceType.Gold] = 1000;
        return (s, home);
    }

    private Province Sea() => _map.Provinces.First(p => p.IsWater);

    [Fact]
    public void EachArmHasItsNamesForTheSameRanks()
    {
        Assert.Equal("Coronel", Officer.RankName(OfficerRank.Colonel));
        Assert.Equal("Capitán de navío", Officer.RankName(OfficerRank.Colonel, OfficerBranch.Navy));
        Assert.Equal("Contraalmirante", Officer.RankName(OfficerRank.MajorGeneral, OfficerBranch.Navy));
        Assert.Equal("Gran almirante", Officer.RankName(OfficerRank.Marshal, OfficerBranch.Navy));
        Assert.Equal("Coronel de aviación", Officer.RankName(OfficerRank.Colonel, OfficerBranch.Air));
        Assert.Equal("Mariscal del aire", Officer.RankName(OfficerRank.Marshal, OfficerBranch.Air));
        var officer = new Officer(1, "Leonor Bazán", [OfficerTrait.Offensive], 1, rank: OfficerRank.Brigadier, branch: OfficerBranch.Navy);
        Assert.Equal("Comodoro Leonor Bazán", officer.Title);
    }

    [Fact]
    public void FleetsAreLedByNavalOfficersAndAircraftByAirmen()
    {
        var (s, home) = Nation();
        var fleet = s.AddFleet(0, Sea().Id, BattalionType.LineShip);
        Assert.True(fleet.HasOfficer);
        Assert.Equal(OfficerBranch.Navy, fleet.OfficerBranch);
        Assert.Equal(OfficerBranch.Air, s.AddRegiment(0, home.Id, BattalionType.Bombers).OfficerBranch);
        Assert.Equal(OfficerBranch.Army, s.AddRegiment(0, home.Id, BattalionType.LightInfantry).OfficerBranch);

        Assert.True(s.RecruitOfficer(0).Ok);
        var soldier = s.Players[0].OfficerReserve[^1];
        var wrong = s.AssignOfficer(0, fleet.Id, soldier.Id);
        Assert.False(wrong.Ok);
        Assert.Contains("oficial de marina", wrong.Message);

        Assert.True(s.RecruitOfficer(0, OfficerBranch.Navy).Ok);
        var sailor = s.Players[0].OfficerReserve[^1];
        Assert.Equal(OfficerBranch.Navy, sailor.Branch);
        Assert.True(s.AssignOfficer(0, fleet.Id, sailor.Id).Ok);
        Assert.Same(sailor, fleet.Officer);
    }

    [Fact]
    public void AirmenNeedAviationAndSailorsAFleetOrAPort()
    {
        var (s, _) = Nation();
        Assert.False(s.CanRecruitOfficer(s.Human, OfficerBranch.Air).Ok);
        s.Human.Learn(Tech.Aviation);
        Assert.True(s.CanRecruitOfficer(s.Human, OfficerBranch.Air).Ok);
        Assert.False(s.CanRecruitOfficer(s.Human, OfficerBranch.Navy).Ok);
        s.AddFleet(0, Sea().Id, BattalionType.LineShip);
        Assert.True(s.CanRecruitOfficer(s.Human, OfficerBranch.Navy).Ok);
    }

    [Fact]
    public void ANavalOfficerAddsToTheFleetsFire()
    {
        var (s, _) = Nation();
        var fleet = s.AddFleet(0, Sea().Id, BattalionType.LineShip, BattalionType.LineShip);
        double plain = GameSession.ExpectedNavalFire(fleet);
        s.Human.OfficerReserve.Add(new Officer(50, "Sancho Haro", [OfficerTrait.Offensive], 3, branch: OfficerBranch.Navy));
        Assert.True(s.AssignOfficer(0, fleet.Id, 50).Ok);
        Assert.Equal(plain * (1 + Officer.AttackPerStar * 3 / 2), GameSession.ExpectedNavalFire(fleet), 6);
    }

    [Fact]
    public void TheArmIsSaved()
    {
        var (s, _) = Nation();
        var fleet = s.AddFleet(0, Sea().Id, BattalionType.LineShip);
        s.RecruitOfficer(0, OfficerBranch.Navy);
        s.AssignOfficer(0, fleet.Id, s.Human.OfficerReserve[^1].Id);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        Assert.Equal(OfficerBranch.Navy, loaded.UnitById(fleet.Id)!.Officer!.Branch);
    }
}
