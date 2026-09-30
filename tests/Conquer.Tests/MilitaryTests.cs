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
        var claimer = s.AddRegiment(1, b.Id, BattalionType.Warriors);
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
    public void BattalionsTakeMenResourcesAndDaysToTrain()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 1000;
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 500;
        var info = BattalionType.Warriors.Info();

        Assert.True(s.Train(0, city.Id, BattalionType.Warriors).Ok);
        Assert.Equal(1000 - info.Men, a.Population);
        Assert.Equal(500 - 30, s.Human.Stockpile[ResourceType.Wood]);
        Assert.Single(city.Training);

        RunHours(s, 24 * info.TrainingDays);
        Assert.Empty(city.Training);
        var regiment = Assert.Single(s.Units, u => u.IsMilitary && u.OwnerId == 0);
        Assert.Equal(a.Id, regiment.ProvinceId);
        Assert.Equal(info.Men, regiment.Citizens);
        Assert.Equal("1.er Regimiento", regiment.Name);
    }

    [Fact]
    public void AdvancedBattalionsNeedTheirAdvance()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 1000;
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 500;
        s.Human.Stockpile[ResourceType.Copper] = 100;

        Assert.True(s.Train(0, city.Id, BattalionType.Warriors).Ok); // warriors need nothing
        Assert.False(s.Train(0, city.Id, BattalionType.Archers).Ok);
        s.Human.Learn(Tech.Archery);
        Assert.True(s.Train(0, city.Id, BattalionType.Archers).Ok);

        Assert.False(s.Train(0, city.Id, BattalionType.Horsemen).Ok);
        s.Human.Learn(Tech.HorsebackRiding);
        Assert.True(s.Train(0, city.Id, BattalionType.Horsemen).Ok);
        Assert.False(s.Train(0, city.Id, BattalionType.Chariots).Ok); // needs the wheel

        s.Human.Learn(Tech.TheWheel);
        Assert.True(s.Train(0, city.Id, BattalionType.ChariotArchers).Ok); // the wheel and archery
        s.Human.Techs.Remove(Tech.Archery);
        var noBows = s.CanTrain(city, BattalionType.ChariotArchers);
        Assert.False(noBows.Ok);
        Assert.Contains("tiro con arco", noBows.Message);
    }

    [Fact]
    public void ClassicalBattalionsNeedTheirAdvances()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 2000;
        foreach (var r in new[] { ResourceType.Wood, ResourceType.Gold, ResourceType.Copper, ResourceType.Iron }) s.Human.Stockpile[r] = 1000;

        Assert.False(s.Train(0, city.Id, BattalionType.Legionaries).Ok);
        s.Human.Learn(Tech.MilitaryTactics);
        Assert.True(s.Train(0, city.Id, BattalionType.Legionaries).Ok);

        Assert.False(s.Train(0, city.Id, BattalionType.Catapults).Ok);
        s.Human.Learn(Tech.SiegeEngines);
        Assert.True(s.Train(0, city.Id, BattalionType.Catapults).Ok);

        Assert.False(s.Train(0, city.Id, BattalionType.Cataphracts).Ok);
        s.Human.Learn(Tech.HeavyCavalry);
        Assert.True(s.Train(0, city.Id, BattalionType.Cataphracts).Ok);
    }

    [Fact]
    public void DivisionsMergeSplitAndMarchAtTheSlowestPace()
    {
        var (s, a, _) = TwoNations();
        var riders = s.AddRegiment(0, a.Id, BattalionType.Horsemen);
        var foot = s.AddRegiment(0, a.Id, BattalionType.Warriors, BattalionType.Archers);
        Assert.Equal(1.8, riders.Speed);

        Assert.True(s.Merge(0, riders.Id, foot.Id).Ok);
        Assert.Null(s.UnitById(foot.Id));
        Assert.Equal(3, riders.Battalions.Count);
        Assert.Equal(1, riders.Speed);

        var more = s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.Warriors, 10)]);
        Assert.False(s.Merge(0, riders.Id, more.Id).Ok); // thirteen battalions is too many

        Assert.True(s.Split(0, riders.Id, 0).Ok);
        Assert.Equal(2, riders.Battalions.Count);
        Assert.Contains(s.Units, u => u.IsMilitary && u.Battalions.Count == 1 && u.Battalions[0].Type == BattalionType.Horsemen);
    }

    [Fact]
    public void ArmiesOnlyEnterTheLandOfNationsAtWar()
    {
        var (s, a, b) = TwoNations();
        var regiment = s.AddRegiment(0, a.Id, BattalionType.Warriors);

        Assert.False(s.MoveUnit(0, regiment.Id, b.Id).Ok);
        var settlers = s.AddUnit(0, UnitType.Settlers, a.Id, 300);
        Assert.True(s.DeclareWar(0, 1).Ok);
        Assert.True(s.AtWar(1, 0));
        Assert.True(s.MoveUnit(0, regiment.Id, b.Id).Ok);
        Assert.False(s.MoveUnit(0, settlers.Id, b.Id).Ok); // only armies go into enemy land
    }

    [Fact]
    public void MarchingIntoUndefendedEnemyLandOccupiesIt()
    {
        var (s, a, b) = TwoNations();
        var regiment = s.AddRegiment(0, a.Id, BattalionType.Warriors);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, regiment.Id, b.Id);

        RunUntil(s, () => regiment.ProvinceId == b.Id, 24 * 5);
        Assert.Equal(b.Id, regiment.ProvinceId);
        Assert.Equal(0, b.ControllerId);
        Assert.Equal(1, b.OwnerId);
        Assert.True(b.IsOccupied);
        Assert.Contains(s.MoodFactors(b), f => f.Points == MilitaryRules.OccupiedMood);
    }

    [Fact]
    public void AStrongAttackWinsTheProvince()
    {
        var (s, a, b) = TwoNations();
        var defender = s.AddRegiment(1, b.Id, BattalionType.Warriors);
        var attackers = Enumerable.Range(0, 3)
            .Select(_ => s.AddRegiment(0, a.Id, BattalionType.IronInfantry, BattalionType.IronInfantry, BattalionType.IronInfantry, BattalionType.IronInfantry))
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
        s.AddRegiment(1, b.Id, BattalionType.IronInfantry, BattalionType.IronInfantry, BattalionType.IronInfantry, BattalionType.IronInfantry);
        var attacker = s.AddRegiment(0, a.Id, BattalionType.Archers);
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
            s.Claim(0, s.AddRegiment(0, n, BattalionType.Warriors).Id);
        var defender = s.AddRegiment(1, b.Id, BattalionType.Warriors);
        s.DeclareWar(0, 1);
        foreach (int n in b.Neighbors.Where(n => _map.Provinces[n].ControllerId == 0 && !_map.Provinces[n].IsOccupied))
        {
            var unit = s.AddRegiment(0, n, BattalionType.IronInfantry, BattalionType.IronInfantry, BattalionType.IronInfantry);
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
        var regiment = s.AddRegiment(0, a.Id, BattalionType.Warriors);
        s.DeclareWar(0, 1);
        s.MoveUnit(0, regiment.Id, b.Id);
        RunUntil(s, () => b.IsOccupied, 24 * 5);
        Assert.True(b.IsOccupied);

        s.MakePeace(0, 1);
        Assert.False(s.AtWar(0, 1));
        Assert.False(b.IsOccupied);
        Assert.Equal(0, _map.Provinces[regiment.ProvinceId].ControllerId);
    }

    [Fact]
    public void ComputerRivalsOnlyAcceptPeaceAfterAWhile()
    {
        var s = GameSession.Create(_map, 2, seed: 7);
        // A human army at least as strong as the rival's, so the rival is not winning.
        var home = s.Units.First(u => u.OwnerId == 0).ProvinceId;
        s.AddRegiment(0, home, [.. Enumerable.Repeat(BattalionType.IronInfantry, 12)]);
        s.Human.Stockpile[ResourceType.Gold] = s.Human.Stockpile[ResourceType.Iron] = 1000; // to pay for it
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
        var home = s.AddRegiment(0, a.Id, BattalionType.Warriors);
        var far = _map.Provinces.First(p => p.IsClaimable && _map.DistanceKm(p, a) > 6000);
        var lost = s.AddRegiment(0, far.Id, BattalionType.Warriors);

        RunHours(s, 24);
        Assert.True(s.IsInSupply(home));
        Assert.False(s.IsInSupply(lost));
        Assert.Equal(1, home.OrganisationShare, 6);
        Assert.True(lost.OrganisationShare < 1);
        Assert.True(lost.Citizens < BattalionType.Warriors.Info().Men);
    }

    [Fact]
    public void DivisionsInSupplyRecoverAndAreReinforcedFromTheCapital()
    {
        var (s, a, _) = TwoNations();
        a.Population = 1000;
        var regiment = s.AddRegiment(0, a.Id, BattalionType.Warriors);
        var battalion = regiment.Battalions[0];
        battalion.Organisation = 0;
        battalion.Strength = 50;

        RunHours(s, 24);
        Assert.Equal(battalion.Info.MaxOrganisation * MilitaryRules.OrganisationRecovery, battalion.Organisation, 6);
        double reinforcement = battalion.Info.Men * MilitaryRules.ReinforcementRate;
        Assert.Equal(50 + reinforcement, battalion.Strength, 6);
    }

    [Fact]
    public void HeadquartersInRangeGiveABonusUpTheChain()
    {
        var (s, a, _) = TwoNations();
        var regiment = s.AddRegiment(0, a.Id, BattalionType.Warriors);
        var corps = s.AddHeadquarters(0, a.Id, 1);
        var army = s.AddHeadquarters(0, a.Id, 2);
        Assert.Equal("I Cuerpo", corps.Name);
        Assert.Equal("1.er Ejército", army.Name);

        Assert.False(s.Attach(0, regiment.Id, army.Id).Ok); // combat units report to a corps
        Assert.True(s.Attach(0, regiment.Id, corps.Id).Ok);
        Assert.Equal(MilitaryRules.CommandBonus, s.CommandBonus(regiment), 6);
        Assert.True(s.Attach(0, corps.Id, army.Id).Ok);
        Assert.Equal(MilitaryRules.CommandBonus + MilitaryRules.HigherCommandBonus, s.CommandBonus(regiment), 6);

        corps.ProvinceId = _map.Provinces.First(p => p.IsClaimable && _map.DistanceKm(p, a) > 2000).Id;
        Assert.Equal(0, s.CommandBonus(regiment));
    }

    [Fact]
    public void EveryNationStartsWithATemplateOfTwoWarriors()
    {
        var s = GameSession.Create(_map, 3, seed: 7, computerRivals: false);
        Assert.All(s.Players, p =>
        {
            var template = Assert.Single(p.Templates);
            Assert.Equal("Plantilla I", template.Name);
            Assert.Equal([BattalionType.Warriors, BattalionType.Warriors], template.Battalions);
        });
    }

    [Fact]
    public void TemplatesAreEditedWithinTheirLimits()
    {
        var (s, _, _) = TwoNations();
        Assert.True(s.CreateTemplate(0).Ok);
        var template = s.Human.Templates[^1];
        Assert.Equal("Plantilla II", template.Name);
        Assert.Equal([BattalionType.Warriors], template.Battalions);

        Assert.False(s.AddToTemplate(0, template.Id, BattalionType.Archers).Ok); // archery not known yet
        s.Human.Learn(Tech.Archery);
        for (int i = 1; i < MilitaryRules.MaxBattalionsPerUnit; i++) Assert.True(s.AddToTemplate(0, template.Id, BattalionType.Archers).Ok);
        Assert.False(s.AddToTemplate(0, template.Id, BattalionType.Warriors).Ok); // full

        Assert.True(s.DuplicateTemplate(0, template.Id).Ok);
        Assert.Equal(template.Battalions, s.Human.Templates[^1].Battalions);
        for (int i = 0; i < MilitaryRules.MaxBattalionsPerUnit - 1; i++) Assert.True(s.RemoveFromTemplate(0, template.Id, 0).Ok);
        Assert.False(s.RemoveFromTemplate(0, template.Id, 0).Ok); // at least one battalion

        foreach (var t in s.Human.Templates.Skip(1).ToList()) Assert.True(s.DeleteTemplate(0, t.Id).Ok);
        Assert.False(s.DeleteTemplate(0, s.Human.Templates[0].Id).Ok); // at least one template
    }

    [Fact]
    public void ATemplateTrainsAWholeRegimentAtOnce()
    {
        var (s, a, _) = TwoNations();
        var city = s.CityIn(a)!;
        a.Population = 2000;
        s.Human.Stockpile[ResourceType.Wood] = s.Human.Stockpile[ResourceType.Gold] = 500;
        s.Human.Learn(Tech.Archery);
        var template = s.Human.Templates[0];
        s.AddToTemplate(0, template.Id, BattalionType.Archers);

        Assert.Equal(20, template.TrainingDays); // the archers are the slowest
        Assert.True(s.TrainTemplate(0, city.Id, template.Id).Ok);
        Assert.Equal(2000 - 300, a.Population);
        Assert.Equal(500 - 90, s.Human.Stockpile[ResourceType.Wood]);
        Assert.Equal(500 - 50, s.Human.Stockpile[ResourceType.Gold]);

        RunHours(s, 24 * 20);
        var regiment = Assert.Single(s.Units, u => u.IsMilitary && u.OwnerId == 0);
        Assert.Equal([BattalionType.Warriors, BattalionType.Warriors, BattalionType.Archers], regiment.Battalions.Select(b => b.Type));
        Assert.Equal(300, regiment.Citizens);
    }

    [Fact]
    public void CombatUnitsAreNamedByTheirSize()
    {
        Assert.Equal("3.er Regimiento", Formations.CombatUnitName(3, 3));
        Assert.Equal("12.º Regimiento", Formations.CombatUnitName(12, 1));
        Assert.Equal("3.ª Brigada", Formations.CombatUnitName(3, 4));
        Assert.Equal("3.ª División", Formations.CombatUnitName(3, 7));
        Assert.Equal("IV Cuerpo", Formations.HeadquartersName(1, 4));
        Assert.Equal("1.er Ejército", Formations.HeadquartersName(2, 1));
        Assert.Equal("2.º Grupo de ejércitos", Formations.HeadquartersName(3, 2));
        Assert.Equal("Batallón de arqueros", Formations.BattalionName(BattalionType.Archers.Info()));
        Assert.Equal("Trirreme", Formations.BattalionName(BattalionType.Trireme.Info()));

        // A regiment that grows into a division keeps its number.
        var (s, a, _) = TwoNations();
        var unit = s.AddRegiment(0, a.Id, BattalionType.Warriors);
        string number = unit.Name.Split(' ')[0].TrimEnd('º', '.', 'e', 'r');
        var other = s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.Warriors, 6)]);
        Assert.True(s.Merge(0, unit.Id, other.Id).Ok);
        Assert.Equal(7, unit.Battalions.Count);
        Assert.EndsWith("División", unit.Name);
        Assert.StartsWith(number, unit.Name);
    }

    [Fact]
    public void ForcesCostUpkeepEveryDay()
    {
        var (s, a, _) = TwoNations();
        s.AddRegiment(0, a.Id, BattalionType.Warriors, BattalionType.IronInfantry);
        s.AddHeadquarters(0, a.Id, 1);
        var upkeep = s.Upkeep(s.Human);

        double gold = (15 + 30 + 60) * MilitaryRules.UpkeepGoldShare; // two battalions and a corps
        Assert.Equal(gold, upkeep[(int)ResourceType.Gold], 6);
        Assert.Equal(20 * MilitaryRules.UpkeepResourceShare, upkeep[(int)ResourceType.Iron], 6);
        Assert.Equal(0, upkeep[(int)ResourceType.Wood]); // wood only goes into raising them

        s.Human.Stockpile[ResourceType.Gold] = s.Human.Stockpile[ResourceType.Iron] = 1000;
        RunHours(s, 24);
        Assert.False(s.Human.ArmyUnpaid);
        Assert.Equal(1000 - 20 * MilitaryRules.UpkeepResourceShare, s.Human.Stockpile[ResourceType.Iron], 6);
    }

    [Fact]
    public void AnUnpaidArmyLosesHeartAndMen()
    {
        var (s, a, _) = TwoNations();
        var unit = s.AddRegiment(0, a.Id, BattalionType.IronInfantry);
        s.Human.Stockpile[ResourceType.Iron] = 0; // nothing to keep its iron weapons
        var b = unit.Battalions[0];

        RunHours(s, 24);

        Assert.True(s.Human.ArmyUnpaid);
        Assert.Equal(0, s.Human.Stockpile[ResourceType.Iron]);
        Assert.True(b.Strength < b.Info.Men);
        Assert.Contains(s.Notifications, n => n.Text.StartsWith("No hay con qué pagar al ejército"));
    }

    [Fact]
    public void ACorpsCommandsAtMostFiveUnits()
    {
        var (s, a, _) = TwoNations();
        var corps = s.AddHeadquarters(0, a.Id, 1);
        for (int i = 0; i < 5; i++) Assert.True(s.Attach(0, s.AddRegiment(0, a.Id, BattalionType.Warriors).Id, corps.Id).Ok);
        Assert.False(s.Attach(0, s.AddRegiment(0, a.Id, BattalionType.Warriors).Id, corps.Id).Ok);
    }

    [Fact]
    public void EveryHeadquartersHasAGeneralOfItsRankWhoLeadsTheUnitsInRange()
    {
        var (s, a, _) = TwoNations();
        var corps = s.AddHeadquarters(0, a.Id, 1);
        var unit = s.AddRegiment(0, a.Id, BattalionType.Warriors);
        Assert.NotNull(corps.Officer);
        Assert.Equal(OfficerRank.LieutenantGeneral, corps.Officer!.Rank);
        Assert.InRange(corps.Officer.Skill, 1, 3);
        Assert.Null(s.GeneralOf(unit));
        Assert.True(s.Attach(0, unit.Id, corps.Id).Ok);
        Assert.Same(corps.Officer, s.GeneralOf(unit));
    }

    [Fact]
    public void OfficersEarnStarsWithVictoriesAndHelpByTheirVirtues()
    {
        var general = new Officer(0, "Álvaro Castro", [OfficerTrait.Offensive], 1);
        general.Victories = Officer.VictoriesPerStar;
        Assert.Equal(2, general.Skill);
        general.Victories = 100;
        Assert.Equal(Officer.MaxSkill, general.Skill);
        Assert.Equal(Officer.AttackPerStar * Officer.MaxSkill, general.FireBonus(attacking: true), 6);
        Assert.Equal(0, general.FireBonus(attacking: false));
        Assert.Equal(0, general.OrganisationLoss);
        Assert.Equal(-Officer.MaxShield, new Officer(1, "Inés Lara", [OfficerTrait.Tactician], 5).OrganisationLoss, 6);
    }

    [Fact]
    public void BattlesGiveExperienceAndTheWinnersGeneralAVictory()
    {
        var (s, a, b) = TwoNations();
        s.AddRegiment(1, b.Id, BattalionType.Warriors);
        var attacker = s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.IronInfantry, 6)]);
        var corps = s.AddHeadquarters(0, a.Id, 1);
        s.Attach(0, attacker.Id, corps.Id);
        int victories = corps.Officer!.Victories;
        s.DeclareWar(0, 1);
        s.MoveUnit(0, attacker.Id, b.Id);

        RunUntil(s, () => s.BattleIn(b.Id) != null, 24 * 5);
        RunUntil(s, () => s.BattleIn(b.Id) == null, 24 * 10);
        Assert.Equal(0, b.ControllerId);
        Assert.All(attacker.Battalions, x => Assert.True(x.Experience > 0));
        Assert.Equal(victories + 1, corps.Officer.Victories);
    }

    [Fact]
    public void RecruitsWaterDownExperience()
    {
        var (s, a, _) = TwoNations();
        a.Population = 1000;
        var battalion = s.AddRegiment(0, a.Id, BattalionType.Warriors).Battalions[0];
        battalion.Strength = 50;
        battalion.Experience = 0.5;

        RunHours(s, 24);
        Assert.Equal(0.5 * 50 / battalion.Strength, battalion.Experience, 6);
    }

    [Fact]
    public void OnlyAFrontsWorthOfBattalionsFightWithSupportBehind()
    {
        var (s, a, _) = TwoNations();
        var infantry = s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.IronInfantry, 12)]);
        var more = s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.IronInfantry, 6)]);
        var siege = s.AddRegiment(0, a.Id, [.. Enumerable.Repeat(BattalionType.Catapults, 12)]);
        var engaged = s.Engage([infantry, more, siege], a, attacking: true);
        int width = MilitaryRules.FrontWidth(a.Biome);
        Assert.Equal(width, engaged.Count(e => e.Role == BattalionRole.Infantry));
        Assert.Equal(width / 2, engaged.Count(e => e.Role == BattalionRole.Artillery));
        Assert.All(engaged.Where(e => e.Role == BattalionRole.Artillery), e => Assert.Equal(MilitaryRules.SupportExposure, e.Exposure));

        var peaks = _map.Provinces.First(p => p.Biome == Biome.HighMountains);
        Assert.True(MilitaryRules.FrontWidth(peaks.Biome) < width);
        Assert.Equal(MilitaryRules.FrontWidth(peaks.Biome), s.Engage([infantry], peaks, attacking: false).Count);
    }

    [Fact]
    public void MixingKindsOfTroopsHitsHarder()
    {
        Assert.Equal(0, GameSession.CombinedArms([BattalionRole.Infantry, BattalionRole.Infantry]));
        Assert.Equal(MilitaryRules.CombinedArmsBonus, GameSession.CombinedArms([BattalionRole.Infantry, BattalionRole.Cavalry]), 6);
        Assert.Equal(MilitaryRules.MaxCombinedArmsBonus, GameSession.CombinedArms(Enum.GetValues<BattalionRole>()), 6);
    }

    [Fact]
    public void GeneralsAndExperienceAreSavedAndOldHeadquartersGetAGeneral()
    {
        var (s, a, _) = TwoNations();
        var corps = s.AddHeadquarters(0, a.Id, 1);
        var unit = s.AddRegiment(0, a.Id, BattalionType.Warriors);
        unit.Battalions[0].Experience = 0.4;
        corps.Officer!.Victories = 4;

        var save = s.ToSave("test");
        var loaded = GameSession.Load(_map, save);
        Assert.Equal(0.4, loaded.UnitById(unit.Id)!.Battalions[0].Experience, 6);
        var general = loaded.UnitById(corps.Id)!.Officer!;
        Assert.Equal((corps.Officer.Name, corps.Officer.Skill, corps.Officer.Rank), (general.Name, general.Skill, general.Rank));
        Assert.Equal(corps.Officer.Traits, general.Traits);

        // A general as saves before officers wrote it becomes an officer of the HQ's rank.
        int corpsIndex = save.Units.FindIndex(u => u.Id == corps.Id);
        save.Units[corpsIndex] = save.Units[corpsIndex] with { Officer = null, General = new GeneralSave("Olga Haro", OfficerTrait.Organiser, 2, 1) };
        var old = GameSession.Load(_map, save).UnitById(corps.Id)!.Officer!;
        Assert.Equal(("Olga Haro", OfficerTrait.Organiser, OfficerRank.LieutenantGeneral), (old.Name, old.Traits.Single(), old.Rank));

        for (int i = 0; i < save.Units.Count; i++) save.Units[i] = save.Units[i] with { Officer = null, General = null };
        Assert.NotNull(GameSession.Load(_map, save).UnitById(corps.Id)!.Officer);
    }
}
