using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

[Collection("World")]
public class OfficerTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>A human nation with a city on grassland and plenty of gold, and no computer rivals.</summary>
    private (GameSession S, Province Home) Nation()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        var home = _map.Provinces.First(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3);
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, home.Id, 300).Id);
        s.Players[0].Stockpile[ResourceType.Gold] = 1000;
        return (s, home);
    }

    private static Officer Recruit(GameSession s)
    {
        Assert.True(s.RecruitOfficer(0).Ok);
        return s.Players[0].OfficerReserve[^1];
    }

    [Fact]
    public void RecruitingCostsGoldAndFillsTheReserve()
    {
        var (s, _) = Nation();
        var officer = Recruit(s);
        Assert.Equal(1000 - MilitaryRules.OfficerCost, s.Players[0].Stockpile[ResourceType.Gold], 6);
        Assert.Equal(OfficerRank.Colonel, officer.Rank);
        Assert.InRange(officer.Traits.Count(t => !Officer.IsFlaw(t)), 1, 2);
        Assert.InRange(officer.Traits.Count(Officer.IsFlaw), 0, 1);

        s.Players[0].Stockpile[ResourceType.Gold] = MilitaryRules.OfficerCost - 1;
        Assert.False(s.RecruitOfficer(0).Ok);
        Assert.Single(s.Players[0].OfficerReserve);
    }

    [Fact]
    public void RecruitsNeverHaveAFlawThatUndoesTheirVirtue()
    {
        var random = new Random(3);
        for (int i = 0; i < 500; i++)
        {
            var traits = Officer.Recruit(i, random).Traits;
            Assert.Equal(traits.Count, traits.Distinct().Count());
            Assert.All(traits.Where(Officer.IsFlaw), flaw => Assert.DoesNotContain((OfficerTrait)((int)flaw - 6), traits));
        }
    }

    [Fact]
    public void OfficersAreAssignedRelievedAndRetiredThroughTheReserve()
    {
        var (s, home) = Nation();
        var unit = s.AddRegiment(0, home.Id, BattalionType.LightInfantry);
        var first = Recruit(s);
        var second = Recruit(s);

        Assert.True(s.AssignOfficer(0, unit.Id, first.Id).Ok);
        Assert.Same(first, unit.Officer);
        Assert.DoesNotContain(first, s.Players[0].OfficerReserve);
        Assert.False(s.AssignOfficer(0, unit.Id, first.Id).Ok); // no longer in the reserve

        Assert.True(s.AssignOfficer(0, unit.Id, second.Id).Ok);
        Assert.Same(second, unit.Officer);
        Assert.Contains(first, s.Players[0].OfficerReserve);

        Assert.True(s.RelieveOfficer(0, unit.Id).Ok);
        Assert.Null(unit.Officer);
        Assert.Equal(2, s.Players[0].OfficerReserve.Count);

        Assert.True(s.RetireOfficer(0, first.Id).Ok);
        Assert.Equal([second], s.Players[0].OfficerReserve);
    }

    [Fact]
    public void OfficersArePromotedAsTheirUnitGrows()
    {
        var (s, home) = Nation();
        var unit = s.AddRegiment(0, home.Id, [.. Enumerable.Repeat(BattalionType.LightInfantry, 3)]);
        var officer = Recruit(s);
        s.AssignOfficer(0, unit.Id, officer.Id);
        Assert.Equal(OfficerRank.Colonel, officer.Rank);

        // Taking in a regiment makes a brigade; taking in a brigade, a division.
        Assert.True(s.Incorporate(0, unit.Id, s.AddRegiment(0, home.Id, [.. Enumerable.Repeat(BattalionType.LightInfantry, 3)]).Id).Ok);
        Assert.Equal(OfficerRank.Brigadier, officer.Rank);
        Assert.True(s.Incorporate(0, unit.Id, s.AddRegiment(0, home.Id, [.. Enumerable.Repeat(BattalionType.LightInfantry, 7)]).Id).Ok);
        Assert.Equal(OfficerRank.MajorGeneral, officer.Rank);

        // Promotions stick when the unit shrinks, and a senior officer keeps their rank at the head of a smaller unit.
        Assert.True(s.Detach(0, unit.Id, 0).Ok);
        Assert.Equal(OfficerRank.MajorGeneral, officer.Rank);
    }

    [Fact]
    public void MergingKeepsTheHostsOfficerOrTakesTheOthers()
    {
        var (s, home) = Nation();
        var host = s.AddRegiment(0, home.Id, BattalionType.LightInfantry);
        var other = s.AddRegiment(0, home.Id, BattalionType.LightInfantry);
        var hostOfficer = Recruit(s);
        var otherOfficer = Recruit(s);
        s.AssignOfficer(0, host.Id, hostOfficer.Id);
        s.AssignOfficer(0, other.Id, otherOfficer.Id);

        Assert.True(s.Merge(0, host.Id, other.Id).Ok);
        Assert.Same(hostOfficer, host.Officer);
        Assert.Contains(otherOfficer, s.Players[0].OfficerReserve);

        var leaderless = s.AddRegiment(0, home.Id, BattalionType.LightInfantry);
        Assert.True(s.Merge(0, leaderless.Id, host.Id).Ok);
        Assert.Same(hostOfficer, leaderless.Officer);
    }

    [Fact]
    public void SeveralBattalionsSplitOffTogetherWithoutAnOfficer()
    {
        var (s, home) = Nation();
        var unit = s.AddRegiment(0, home.Id, BattalionType.LightInfantry, BattalionType.RangedInfantry, BattalionType.LightInfantry, BattalionType.RangedInfantry);
        var officer = Recruit(s);
        s.AssignOfficer(0, unit.Id, officer.Id);

        Assert.True(s.Split(0, unit.Id, [1, 3]).Ok);
        var split = s.Units.Single(u => u.IsMilitary && u.Id != unit.Id);
        Assert.All(split.Battalions, b => Assert.Equal(BattalionType.RangedInfantry, b.Type));
        Assert.All(unit.Battalions, b => Assert.Equal(BattalionType.LightInfantry, b.Type));
        Assert.Same(officer, unit.Officer);
        Assert.Null(split.Officer);

        Assert.False(s.Split(0, unit.Id, [0, 1]).Ok); // someone has to stay
        Assert.False(s.Split(0, unit.Id, []).Ok);
    }

    [Fact]
    public void UnitsCanBeRenamedAndGetTheirAutomaticNameBack()
    {
        var (s, home) = Nation();
        var unit = s.AddRegiment(0, home.Id, BattalionType.LightInfantry);
        string automatic = unit.Name;

        Assert.True(s.RenameUnit(0, unit.Id, "  Los Tercios  ").Ok);
        Assert.Equal("Los Tercios", unit.Name);
        Assert.False(s.RenameUnit(0, unit.Id, new string('x', MilitaryRules.MaxUnitNameLength + 1)).Ok);
        Assert.Equal("Los Tercios", unit.Name);

        // A custom name stays when battalions join; an empty one brings back the automatic name.
        Assert.True(s.Merge(0, unit.Id, s.AddRegiment(0, home.Id, [.. Enumerable.Repeat(BattalionType.LightInfantry, 3)]).Id).Ok);
        Assert.Equal("Los Tercios", unit.Name);
        // Grown into a brigade, the regiment keeps its name inside it, and the brigade is named for its own number.
        Assert.True(s.Incorporate(0, unit.Id, s.AddRegiment(0, home.Id, BattalionType.LightInfantry).Id).Ok);
        Assert.EndsWith("Brigada", unit.Name);
        Assert.Contains("Los Tercios", unit.Regiments.Select(r => r.Name));
        Assert.True(s.RenameUnit(0, unit.Id, "La Vieja").Ok);
        Assert.True(s.RenameUnit(0, unit.Id, "").Ok);
        Assert.Null(unit.CustomName);
        Assert.NotEqual(automatic, unit.Name);
        Assert.Equal(unit.AutomaticName, unit.Name);
    }

    [Fact]
    public void FlawsHinderTheUnitTheyLead()
    {
        var (s, home) = Nation();
        var unit = s.AddRegiment(0, home.Id, BattalionType.LightInfantry);
        double speed = unit.Speed, upkeep = s.Upkeep(s.Players[0])[(int)ResourceType.Gold];
        double fire = s.Engage([unit], home, attacking: true).Sum(e => e.Fire);

        unit.Officer = new Officer(0, "Pedro Téllez", [OfficerTrait.Timid, OfficerTrait.Slow, OfficerTrait.Corrupt], 3);
        Assert.Equal(speed * (1 - Officer.SlowSpeed), unit.Speed, 6);
        Assert.Equal(upkeep * (1 + Officer.CorruptUpkeep), s.Upkeep(s.Players[0])[(int)ResourceType.Gold], 6);
        Assert.Equal(fire * (1 - Officer.TimidAttack), s.Engage([unit], home, attacking: true).Sum(e => e.Fire), 6);

        unit.Officer = new Officer(1, "Leonor Guzmán", [OfficerTrait.Offensive, OfficerTrait.Marcher], 2);
        Assert.Equal(speed * (1 + 2 * Officer.SpeedPerStar), unit.Speed, 6);
        Assert.Equal(fire * (1 + 2 * Officer.AttackPerStar), s.Engage([unit], home, attacking: true).Sum(e => e.Fire), 6);
    }

    [Fact]
    public void DisbandingSendsTheOfficerBackToTheReserve()
    {
        var (s, home) = Nation();
        var unit = s.AddRegiment(0, home.Id, BattalionType.LightInfantry);
        var officer = Recruit(s);
        s.AssignOfficer(0, unit.Id, officer.Id);
        Assert.True(s.Disband(0, unit.Id).Ok);
        Assert.Contains(officer, s.Players[0].OfficerReserve);
    }

    [Fact]
    public void OfficersReserveAndNamesAreSaved()
    {
        var (s, home) = Nation();
        var unit = s.AddRegiment(0, home.Id, BattalionType.LightInfantry);
        var officer = Recruit(s);
        s.AssignOfficer(0, unit.Id, officer.Id);
        officer.Victories = 5;
        var spare = Recruit(s);
        s.RenameUnit(0, unit.Id, "Guardia Real");

        var loaded = GameSession.Load(_map, s.ToSave("test"));
        var loadedUnit = loaded.UnitById(unit.Id)!;
        Assert.Equal("Guardia Real", loadedUnit.Name);
        Assert.Equal((officer.Id, officer.Name, officer.Skill, officer.Rank), (loadedUnit.Officer!.Id, loadedUnit.Officer.Name, loadedUnit.Officer.Skill, loadedUnit.Officer.Rank));
        Assert.Equal(officer.Traits, loadedUnit.Officer.Traits);
        Assert.Equal([spare.Id], loaded.Players[0].OfficerReserve.Select(o => o.Id));

        // New officers keep numbering after the loaded ones.
        loaded.Players[0].Stockpile[ResourceType.Gold] = 1000;
        loaded.RecruitOfficer(0);
        Assert.True(loaded.Players[0].OfficerReserve[^1].Id > spare.Id);
    }
}
