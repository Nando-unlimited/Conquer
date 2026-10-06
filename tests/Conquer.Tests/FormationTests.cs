using Conquer.Game.Entities;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

/// <summary>Regiments, brigades and divisions: how units are arranged, joined, taken in, let go and split.</summary>
[Collection("World")]
public class FormationTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    private (GameSession S, Province Here) Game()
    {
        var s = GameSession.Create(_map, 1, seed: 7, computerRivals: false);
        return (s, _map.Provinces[s.Units.First().ProvinceId]);
    }

    private static Unit Regiment(GameSession s, Province p, int battalions) =>
        s.AddRegiment(0, p.Id, [.. Enumerable.Repeat(BattalionType.LightInfantry, battalions)]);

    [Fact]
    public void BattalionsAreArrangedIntoRegimentsBrigadesAndDivisions()
    {
        var (s, p) = Game();
        var regiment = Regiment(s, p, 5);
        Assert.Equal(Echelon.Regiment, regiment.Size);
        Assert.EndsWith("Regimiento", regiment.Name);

        var brigade = Regiment(s, p, 12);
        Assert.Equal(Echelon.Brigade, brigade.Size);
        Assert.Equal([5, 5, 2], brigade.Regiments.Select(r => r.Battalions.Count));
        Assert.EndsWith("Brigada", brigade.Name);
        Assert.Equal(12, brigade.Battalions.Count);

        var division = Regiment(s, p, 25);
        Assert.Equal(Echelon.Division, division.Size);
        Assert.Single(division.Brigades);
        Assert.Equal(4, division.Brigades[0].Regiments.Count);
        Assert.Single(division.Regiments);
        Assert.Equal(25, division.Battalions.Count);
        Assert.Equal("XX", division.Echelon);
    }

    [Fact]
    public void TwoRegimentsJoinTheirBattalionsUpToFive()
    {
        var (s, p) = Game();
        var a = Regiment(s, p, 2);
        var b = Regiment(s, p, 3);
        Assert.True(s.Merge(0, a.Id, b.Id).Ok);
        Assert.Equal(5, a.Battalions.Count);
        Assert.Null(s.UnitById(b.Id));

        var c = Regiment(s, p, 1);
        var full = s.CanMerge(a, c);
        Assert.False(full.Ok);
        Assert.Contains("incorpóralo", full.Message);
    }

    [Fact]
    public void RegimentsMakeBrigadesAndBrigadesMakeDivisions()
    {
        var (s, p) = Game();
        var a = Regiment(s, p, 5);
        var b = Regiment(s, p, 4);
        string bName = b.Name;
        s.Human.OfficerReserve.Add(Officer.Recruit(99, new Random(1), OfficerRank.Colonel));
        Assert.True(s.AssignOfficer(0, a.Id, 99).Ok);

        // Two regiments make a brigade; each keeps its name inside, and the officer becomes a brigadier.
        Assert.True(s.Incorporate(0, a.Id, b.Id).Ok);
        Assert.Equal(Echelon.Brigade, a.Size);
        Assert.EndsWith("Brigada", a.Name);
        Assert.Contains(bName, a.Regiments.Select(r => r.Name));
        Assert.Equal(OfficerRank.Brigadier, a.Officer!.Rank);
        Assert.Equal(OfficerRank.Brigadier, a.RequiredRank);

        // Up to four regiments; a fifth makes a division of the brigade and the newcomer.
        Assert.True(s.Incorporate(0, a.Id, Regiment(s, p, 1).Id).Ok);
        Assert.True(s.Incorporate(0, a.Id, Regiment(s, p, 1).Id).Ok);
        Assert.Equal(4, a.Regiments.Count);
        Assert.True(s.Incorporate(0, a.Id, Regiment(s, p, 1).Id).Ok);
        Assert.Equal(Echelon.Division, a.Size);
        Assert.Single(a.Brigades);
        Assert.Single(a.Regiments);
        Assert.Equal(OfficerRank.MajorGeneral, a.Officer.Rank);

        // A regiment asked to take in a division goes into it instead.
        var lone = Regiment(s, p, 1);
        Assert.True(s.Incorporate(0, lone.Id, a.Id).Ok);
        Assert.Null(s.UnitById(lone.Id));
        Assert.Equal(3, a.Parts);

        // Two brigades make a division; a division never goes into another.
        var x = Regiment(s, p, 10);
        var y = Regiment(s, p, 10);
        Assert.True(s.Incorporate(0, x.Id, y.Id).Ok);
        Assert.Equal(Echelon.Division, x.Size);
        Assert.Equal(2, x.Brigades.Count);
        Assert.Contains("no cabe", s.CanIncorporate(a, x).Message);
    }

    [Fact]
    public void ADivisionTakesFivePartsAndTenThousandMenAtMost()
    {
        var (s, p) = Game();
        var division = Regiment(s, p, 25); // a brigade of four regiments and a regiment
        for (int i = 0; i < MilitaryRules.MaxDivisionParts - 2; i++) Assert.True(s.Incorporate(0, division.Id, Regiment(s, p, 1).Id).Ok);
        Assert.Equal(MilitaryRules.MaxDivisionParts, division.Parts);
        Assert.Contains("como mucho", s.CanIncorporate(division, Regiment(s, p, 1)).Message);

        // Five full brigades of hundred-men battalions come to the ten thousand exactly.
        var big = Regiment(s, p, 80); // a division of 4 brigades: 8,000 men
        Assert.Equal(8000, big.FullMen);
        Assert.True(s.Incorporate(0, big.Id, Regiment(s, p, 20).Id).Ok);
        Assert.Equal(MilitaryRules.MaxDivisionMen, big.FullMen);
        Assert.False(s.CanIncorporate(big, Regiment(s, p, 1)).Ok);
    }

    [Fact]
    public void PartsLeaveWithTheirOwnNumberAndName()
    {
        var (s, p) = Game();
        var a = Regiment(s, p, 3);
        var b = Regiment(s, p, 2);
        Assert.True(s.RenameUnit(0, b.Id, "Los Tercios").Ok);
        Assert.True(s.Incorporate(0, a.Id, b.Id).Ok);
        var parts = GameSession.PartsOf(a);
        Assert.Equal(2, parts.Count);
        int index = parts.ToList().FindIndex(x => x.Name == "Los Tercios");

        Assert.True(s.Detach(0, a.Id, index).Ok);
        var back = s.Units.Single(u => u.Name == "Los Tercios");
        Assert.Equal(Echelon.Regiment, back.Size);
        Assert.Equal(2, back.Battalions.Count);
        Assert.Null(back.Officer);
        // The brigade keeps its last regiment, but cannot let it go.
        Assert.Single(a.Regiments);
        Assert.False(s.Detach(0, a.Id, 0).Ok);

        // A division lets a whole brigade go.
        var division = Regiment(s, p, 25);
        Assert.True(s.Detach(0, division.Id, 0).Ok);
        Assert.Contains(s.Units, u => u.Size == Echelon.Brigade && u.Regiments.Count == 4 && u.Id != division.Id);
        Assert.Single(division.Regiments);
    }

    [Fact]
    public void SplitBattalionsFormANewRegimentAndEmptyPartsAreDisbanded()
    {
        var (s, p) = Game();
        var brigade = Regiment(s, p, 7); // regiments of 5 and 2
        Assert.False(s.Split(0, brigade.Id, [0, 1, 2, 3, 4, 5]).Ok); // more than a regiment
        Assert.True(s.Split(0, brigade.Id, [5, 6]).Ok); // the whole second regiment
        Assert.Single(brigade.Regiments);
        Assert.Equal(5, brigade.Battalions.Count);
        var split = s.Units.Last();
        Assert.Equal(Echelon.Regiment, split.Size);
        Assert.Equal(2, split.Battalions.Count);
    }

    [Fact]
    public void FormationsAreSaved()
    {
        var (s, p) = Game();
        var division = Regiment(s, p, 25);
        Assert.True(s.RenameUnit(0, division.Id, "La Azul").Ok);
        var loaded = GameSession.Load(_map, s.ToSave("test"));
        var back = loaded.UnitById(division.Id)!;
        Assert.Equal("La Azul", back.Name);
        Assert.Equal(Echelon.Division, back.Size);
        Assert.Equal(division.Brigades.Select(b => b.Name), back.Brigades.Select(b => b.Name));
        Assert.Equal(division.Regiments.Select(r => r.Name), back.Regiments.Select(r => r.Name));
        Assert.Equal(25, back.Battalions.Count);
    }
}
