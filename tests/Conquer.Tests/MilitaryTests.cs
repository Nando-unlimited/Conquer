using Conquer.Game.Economy;
using Conquer.Game.Military;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.Simulation;
using Conquer.Game.World;

namespace Conquer.Tests;

[Collection("World")]
public class MilitaryTests(WorldFixture world)
{
    private readonly WorldMap _map = world.Map;

    /// <summary>A grassland province and a grassland neighbour, both with several neighbours.</summary>
    private (Province A, Province B) Pair() =>
        _map.Provinces
            .Where(p => p.Biome == Biome.Grassland && p.Neighbors.Length > 3)
            .SelectMany(p => p.Neighbors.Select(n => (A: p, B: _map.Provinces[n])))
            .First(t => t.B.Biome == Biome.Grassland && t.B.Neighbors.Length > 3);

    /// <summary>
    /// Player 0 (human) has its capital in A; player 1 owns the neighbouring B. No computer rivals, so
    /// only the test moves units.
    /// </summary>
    private (GameSession S, Province A, Province B) TwoNations()
    {
        var s = GameSession.Create(_map, 2, seed: 7, computerRivals: false);
        var (a, b) = Pair();
        s.FoundCity(0, s.AddUnit(0, UnitType.Settlers, a.Id, 300).Id);
        var claimer = s.AddDivision(1, b.Id, BrigadeType.Warriors);
        s.Claim(1, claimer.Id);
        s.Disband(1, claimer.Id);
        return (s, a, b);
    }

    private static void RunHours(GameSession s, int hours)
    {
        for (int i = 0; i < hours; i++) s.Step();
    }

    private static void RunUntil(GameSession s, Func<bool> done, int maxHours)
    {
        for (int i = 0; i < maxHours && !done(); i++) s.Step();
    }

    [Fact]
    public void BrigadesTakeMenResourcesAndDaysToTrain()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 1000;
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 500;
        var info = BrigadeType.Warriors.Info();

        Assert.True(s.Train(0, city.Id, BrigadeType.Warriors).Ok);
        Assert.Equal(1000 - info.Men, a.Population);
        Assert.Equal(500 - 30, s.Human.Stockpile[ResourceType.Wood]);
        Assert.Single(city.Training);

        RunHours(s, 24 * info.TrainingDays);
        Assert.Empty(city.Training);
        var division = Assert.Single(s.Units, u => u.IsMilitary && u.OwnerId == 0);
        Assert.Equal(a.Id, division.ProvinceId);
        Assert.Equal(info.Men, division.Citizens);
        Assert.Equal("1.ª División", division.Name);
    }

    [Fact]
    public void AdvancedBrigadesNeedTheirAdvance()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 1000;
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 500;
        s.Human.Stockpile[ResourceType.Copper] = 100;

        Assert.True(s.Train(0, city.Id, BrigadeType.Warriors).Ok); // warriors need nothing
        Assert.False(s.Train(0, city.Id, BrigadeType.Archers).Ok);
        s.Human.Learn(Tech.Archery);
        Assert.True(s.Train(0, city.Id, BrigadeType.Archers).Ok);

        Assert.False(s.Train(0, city.Id, BrigadeType.Horsemen).Ok);
        s.Human.Learn(Tech.HorsebackRiding);
        Assert.True(s.Train(0, city.Id, BrigadeType.Horsemen).Ok);
        Assert.False(s.Train(0, city.Id, BrigadeType.Chariots).Ok); // needs the wheel

        s.Human.Learn(Tech.TheWheel);
        Assert.True(s.Train(0, city.Id, BrigadeType.ChariotArchers).Ok); // the wheel and archery
        s.Human.Techs.Remove(Tech.Archery);
        var noBows = s.CanTrain(city, BrigadeType.ChariotArchers);
        Assert.False(noBows.Ok);
        Assert.Contains("tiro con arco", noBows.Message);
    }

    [Fact]
    public void DivisionsMergeSplitAndMarchAtTheSlowestPace()
    {
        var (s, a, _) = TwoNations();
        var riders = s.AddDivision(0, a.Id, BrigadeType.Horsemen);
        var foot = s.AddDivision(0, a.Id, BrigadeType.Warriors, BrigadeType.Archers);
        Assert.Equal(1.8, riders.Speed);

        Assert.True(s.Merge(0, riders.Id, foot.Id).Ok);
        Assert.Null(s.UnitById(foot.Id));
        Assert.Equal(3, riders.Brigades.Count);
        Assert.Equal(1, riders.Speed);

        var more = s.AddDivision(0, a.Id, BrigadeType.Warriors, BrigadeType.Warriors);
        Assert.False(s.Merge(0, riders.Id, more.Id).Ok); // five brigades is too many

        Assert.True(s.Split(0, riders.Id, 0).Ok);
        Assert.Equal(2, riders.Brigades.Count);
        Assert.Contains(s.Units, u => u.IsMilitary && u.Brigades.Count == 1 && u.Brigades[0].Type == BrigadeType.Horsemen);
    }

    [Fact]
    public void ArmiesOnlyEnterTheLandOfNationsAtWar()
    {
        var (s, a, b) = TwoNations();
        var division = s.AddDivision(0, a.Id, BrigadeType.Warriors);

        Assert.False(s.MoveUnit(0, division.Id, b.Id).Ok);
        var settlers = s.AddUnit(0, UnitType.Settlers, a.Id, 300);
        Assert.True(s.DeclareWar(0, 1).Ok);
        Assert.True(s.AtWar(1, 0));
        Assert.True(s.MoveUnit(0, division.Id, b.Id).Ok);
        Assert.False(s.MoveUnit(0, settlers.Id, b.Id).Ok); // only armies go into enemy land
    }

    [Fact]
    public void MarchingIntoUndefendedEnemyLandOccupiesIt()
    {
        var (s, a, b) = TwoNations();
        var division = s.AddDivision(0, a.Id, BrigadeType.Warriors);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, division.Id, b.Id);

        RunUntil(s, () => division.ProvinceId == b.Id, 24 * 5);
        Assert.Equal(b.Id, division.ProvinceId);
        Assert.Equal(0, b.ControllerId);
        Assert.Equal(1, b.OwnerId);
        Assert.True(b.IsOccupied);
        Assert.Contains(s.MoodFactors(b), f => f.Points == MilitaryRules.OccupiedMood);
    }

    [Fact]
    public void AStrongAttackWinsTheProvince()
    {
        var (s, a, b) = TwoNations();
        var defender = s.AddDivision(1, b.Id, BrigadeType.Warriors);
        var attackers = Enumerable.Range(0, 3)
            .Select(_ => s.AddDivision(0, a.Id, BrigadeType.IronInfantry, BrigadeType.IronInfantry, BrigadeType.IronInfantry, BrigadeType.IronInfantry))
            .ToList();
        s.DeclareWar(0, 1);
        foreach (var unit in attackers) s.MoveUnit(0, unit.Id, b.Id);

        RunUntil(s, () => s.BattleIn(b.Id) != null, 24 * 5);
        Assert.NotNull(s.BattleIn(b.Id));
        Assert.All(attackers, u => Assert.Equal(a.Id, u.ProvinceId)); // attackers wait at the border

        RunUntil(s, () => s.BattleIn(b.Id) == null, 24 * 10);
        Assert.Null(s.BattleIn(b.Id));
        Assert.All(attackers, u => Assert.Equal(b.Id, u.ProvinceId));
        Assert.Equal(0, b.ControllerId);
        Assert.True(s.UnitById(defender.Id) is null || s.UnitById(defender.Id)!.ProvinceId != b.Id);
    }

    [Fact]
    public void AWeakAttackBreaksAndGivesUp()
    {
        var (s, a, b) = TwoNations();
        s.AddDivision(1, b.Id, BrigadeType.IronInfantry, BrigadeType.IronInfantry, BrigadeType.IronInfantry, BrigadeType.IronInfantry);
        var attacker = s.AddDivision(0, a.Id, BrigadeType.Archers);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, attacker.Id, b.Id);

        RunUntil(s, () => s.BattleIn(b.Id) != null, 24 * 5);
        RunUntil(s, () => s.BattleIn(b.Id) == null, 24 * 10);
        Assert.Equal(a.Id, attacker.ProvinceId);
        Assert.Null(attacker.AttackingProvinceId);
        Assert.True(attacker.OrganisationShare < MilitaryRules.BreakingOrganisation);
        Assert.Equal(1, b.ControllerId);
    }

    [Fact]
    public void SurroundedDefendersAreDestroyed()
    {
        var (s, a, b) = TwoNations();
        // Player 0 holds every land neighbour of B, so there is nowhere to retreat to.
        foreach (int n in b.Neighbors.Where(n => !_map.Provinces[n].IsWater && !_map.Provinces[n].IsOwned))
            s.Claim(0, s.AddDivision(0, n, BrigadeType.Warriors).Id);
        var defender = s.AddDivision(1, b.Id, BrigadeType.Warriors);
        s.DeclareWar(0, 1);
        foreach (int n in b.Neighbors.Where(n => _map.Provinces[n].ControllerId == 0 && !_map.Provinces[n].IsOccupied))
        {
            var unit = s.AddDivision(0, n, BrigadeType.IronInfantry, BrigadeType.IronInfantry, BrigadeType.IronInfantry);
            s.MoveUnit(0, unit.Id, b.Id);
        }

        RunUntil(s, () => s.UnitById(defender.Id) is null, 24 * 10);
        Assert.Null(s.UnitById(defender.Id));
        RunHours(s, 2); // the attackers march in once the battle is over
        Assert.Equal(0, b.ControllerId);
    }

    [Fact]
    public void PeaceReturnsOccupiedLandAndSendsArmiesHome()
    {
        var (s, a, b) = TwoNations();
        var division = s.AddDivision(0, a.Id, BrigadeType.Warriors);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, division.Id, b.Id);
        RunUntil(s, () => b.IsOccupied, 24 * 5);
        Assert.True(b.IsOccupied);

        s.MakePeace(0, 1);
        Assert.False(s.AtWar(0, 1));
        Assert.False(b.IsOccupied);
        Assert.Equal(0, _map.Provinces[division.ProvinceId].ControllerId);
    }

    [Fact]
    public void ComputerRivalsOnlyAcceptPeaceAfterAWhile()
    {
        var s = GameSession.Create(_map, 2, seed: 7);
        // A human army at least as strong as the rival's, so the rival is not winning.
        var home = s.Units.First(u => u.OwnerId == 0).ProvinceId;
        s.AddDivision(0, home, BrigadeType.IronInfantry, BrigadeType.IronInfantry, BrigadeType.IronInfantry, BrigadeType.IronInfantry);
        s.DeclareWar(0, 1);
        Assert.False(s.ProposePeace(0, 1).Ok);
        RunHours(s, 24 * 61);
        Assert.True(s.ProposePeace(0, 1).Ok);
        Assert.False(s.AtWar(0, 1));
    }

    [Fact]
    public void DivisionsWitherWithoutSupply()
    {
        var (s, a, _) = TwoNations();
        var home = s.AddDivision(0, a.Id, BrigadeType.Warriors);
        var far = _map.Provinces.First(p => p.IsClaimable && _map.DistanceKm(p, a) > 6000);
        var lost = s.AddDivision(0, far.Id, BrigadeType.Warriors);

        RunHours(s, 24);
        Assert.True(s.IsInSupply(home));
        Assert.False(s.IsInSupply(lost));
        Assert.Equal(1, home.OrganisationShare, 6);
        Assert.True(lost.OrganisationShare < 1);
        Assert.True(lost.Citizens < BrigadeType.Warriors.Info().Men);
    }

    [Fact]
    public void DivisionsInSupplyRecoverAndAreReinforcedFromTheCapital()
    {
        var (s, a, _) = TwoNations();
        a.Population = 1000;
        var division = s.AddDivision(0, a.Id, BrigadeType.Warriors);
        var brigade = division.Brigades[0];
        brigade.Organisation = 0;
        brigade.Strength = 50;

        RunHours(s, 24);
        Assert.Equal(brigade.Info.MaxOrganisation * MilitaryRules.OrganisationRecovery, brigade.Organisation, 6);
        double reinforcement = brigade.Info.Men * MilitaryRules.ReinforcementRate;
        Assert.Equal(50 + reinforcement, brigade.Strength, 6);
    }

    [Fact]
    public void HeadquartersInRangeGiveABonusUpTheChain()
    {
        var (s, a, _) = TwoNations();
        var division = s.AddDivision(0, a.Id, BrigadeType.Warriors);
        var corps = s.AddHeadquarters(0, a.Id, 1);
        var army = s.AddHeadquarters(0, a.Id, 2);
        Assert.Equal("I Cuerpo", corps.Name);

        Assert.False(s.Attach(0, division.Id, army.Id).Ok); // divisions report to a corps
        Assert.True(s.Attach(0, division.Id, corps.Id).Ok);
        Assert.Equal(MilitaryRules.CommandBonus, s.CommandBonus(division), 6);
        Assert.True(s.Attach(0, corps.Id, army.Id).Ok);
        Assert.Equal(MilitaryRules.CommandBonus + MilitaryRules.HigherCommandBonus, s.CommandBonus(division), 6);

        corps.ProvinceId = _map.Provinces.First(p => p.IsClaimable && _map.DistanceKm(p, a) > 2000).Id;
        Assert.Equal(0, s.CommandBonus(division));
    }

    [Fact]
    public void ACorpsCommandsAtMostFiveDivisions()
    {
        var (s, a, _) = TwoNations();
        var corps = s.AddHeadquarters(0, a.Id, 1);
        for (int i = 0; i < 5; i++) Assert.True(s.Attach(0, s.AddDivision(0, a.Id, BrigadeType.Warriors).Id, corps.Id).Ok);
        Assert.False(s.Attach(0, s.AddDivision(0, a.Id, BrigadeType.Warriors).Id, corps.Id).Ok);
    }
}
