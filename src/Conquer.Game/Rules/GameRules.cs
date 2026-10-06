using Conquer.Game.Economy;
using Conquer.Game.Science;

namespace Conquer.Game.Rules;

/// <summary>Tuning numbers for the whole game. Rates are per in-game day unless stated otherwise.</summary>
public static class GameRules
{
    // Starting conditions
    /// <summary>The band each nation starts with, enough to found a capital that can raise an army soon.</summary>
    public const int StartingCitizens = 3000;
    /// <summary>Ten days of food for the starting band while it looks for a place to settle.</summary>
    public const double StartingFood = 3000;
    public const double StartingGold = 50;
    public const double StartingWood = 100;
    /// <summary>Stone every nation starts with, for its first stone buildings.</summary>
    public const double StartingStone = 50;
    /// <summary>A band of settlers a city sends out: this many of its citizens, plus <see cref="SettlersCost"/>.</summary>
    public const int SettlerCitizens = 300;
    /// <summary>What a band of settlers costs besides its <see cref="SettlerCitizens"/>.</summary>
    public static readonly ResourceCost SettlersCost = new((ResourceType.Food, 150), (ResourceType.Wood, 50), (ResourceType.Gold, 20));

    // Movement
    /// <summary>How far a citizen walks in an hour on open ground; units and migrants travel at this pace.</summary>
    public const double CitizenSpeedKmh = 10;
    /// <summary>Ships cross the sea this many times faster than people walk.</summary>
    public const double SailingSpeed = 2;

    // Population
    /// <summary>Food each citizen eats per day (units and migrants on the road too).</summary>
    public const double FoodPerCitizen = 0.1;
    /// <summary>Food a worker grows per day on land of yield 1; most farmland feeds more people than work it.</summary>
    public const double FoodPerWorker = 0.13;
    /// <summary>Daily growth of a fed province (about +1.5 % a year at normal fertility), damped as it fills up.</summary>
    public const double GrowthRate = 0.00004;
    /// <summary>
    /// Daily births in every populated province, on top of <see cref="GrowthRate"/>, so a handful of settlers still
    /// grows into a village; it does not grow with the land, or empty land would fill by itself.
    /// </summary>
    public const double BaseBirthsPerProvince = 0.02;
    /// <summary>Share of the stored food that spoils each day, so stores settle at about 100 days of surplus.</summary>
    public const double FoodSpoilage = 0.01;
    public const double StarvationRate = 0.01;
    /// <summary>A city feeds this many times the citizens of the surrounding land.</summary>
    public const double CityCapacityMultiplier = 2.5;
    /// <summary>
    /// Scales the citizens per km² each biome feeds at the start of history, so the whole world holds tens of millions
    /// and not hundreds; each age then raises it (<see cref="EraCapacity"/>).
    /// </summary>
    public const double LandCarryingScale = 0.1;

    /// <summary>How many times more people the land feeds in each age than in the Ancient one: better farming, trade and medicine.</summary>
    public static double EraCapacity(Era era) => era switch
    {
        Era.Ancient => 1,
        Era.Classical => 1.5,
        Era.Medieval => 2,
        Era.Renaissance => 3,
        Era.Industrial => 5,
        _ => 8,
    };

    /// <summary>Cities grow this many times faster than the countryside.</summary>
    public const double CityGrowthMultiplier = 2;

    // Production
    public const double TaxGoldPerCitizen = 0.002;
    /// <summary>A deposit gives its full output once this many citizens live in the province.</summary>
    public const double DepositFullWorkers = 1000;
    /// <summary>
    /// Scales the size of every deposit (pocket of resource) at the start of a game. The difficulty
    /// levels will change it: larger pockets on easy, smaller on hard.
    /// </summary>
    public const double DepositSizeMultiplier = 1;
    /// <summary>Runoff a stretch of land must gather to become a river (in cells of rainy land at the equator).</summary>
    public const float MinRiverFlow = 60;
    /// <summary>Rivers from this flow on are great rivers: only they make land fertile and hard to cross.</summary>
    public const float GreatRiverFlow = 300;
    /// <summary>A river's floodplain grows this many times the food, and feeds this many times the people.</summary>
    public const double RiverFertility = 1.25;

    /// <summary>Land above this height (in metres) is peaks: one province per range, crossable but never claimed (maps since 1.33.0).</summary>
    public const short PeakElevation = 5000;
    /// <summary>Patches of peaks smaller than this many pixels keep the biome they would have had.</summary>
    public const int MinPeakPixels = 30;
    /// <summary>Workers beyond the land's capacity still produce this share of normal food.</summary>
    public const double OvercrowdedFoodShare = 0.3;

    // Science
    /// <summary>Science points every city produces per day on top of its citizens'.</summary>
    public const double ScienceBasePerCity = 0.25;
    /// <summary>Science points each city dweller produces per day.</summary>
    public const double SciencePerCityCitizen = 0.001;
    /// <summary>Every advance costs this many times its listed points, so the ages last generations and not a few years.</summary>
    public const double ResearchCostMultiplier = 3;
    /// <summary>Each branch's priority goes from 0 to this; its share of science is its priority over the sum of all three.</summary>
    public const int MaxResearchPriority = 10;
    public const int DefaultResearchPriority = 1;
    /// <summary>A branch's next level opens once this share of the advances of the level below is known.</summary>
    public const double LevelUnlockShare = 0.5;

    // Institutions
    /// <summary>Urbanism is born in the first city this big...</summary>
    public const double UrbanismBirthPopulation = 5000;
    /// <summary>...and feudalism in the capital of the first nation with this many cities.</summary>
    public const int FeudalismBirthCities = 8;
    /// <summary>Daily chance that an institution passes to a settled province from each neighbour that has it...</summary>
    public const double InstitutionSpreadChance = 0.01;
    /// <summary>...this many times higher when the receiving province has a city.</summary>
    public const double CityInstitutionSpread = 3;
    /// <summary>A nation adopts an institution on its own once this share of its settled people have it.</summary>
    public const double InstitutionAdoptionShare = 0.5;
    /// <summary>Gold to adopt it earlier, per citizen who does not have it yet; never less than the minimum.</summary>
    public const double InstitutionGoldPerCitizen = 0.1;
    public const double MinInstitutionGold = 50;
    /// <summary>Until then, the advances of the age it opens cost this much more.</summary>
    public const double InstitutionPenalty = 0.5;
    /// <summary>An advance costs this much less for each bordering nation that already knows it...</summary>
    public const double NeighbourResearchDiscount = 0.1;
    /// <summary>...counting this many at most.</summary>
    public const int MaxNeighbourDiscounts = 3;

    // Migration
    /// <summary>Share of a city's population that may leave for new territories each day.</summary>
    public const double DailyEmigrationShare = 0.0001;
    /// <summary>Cities stop sending migrants below this population.</summary>
    public const int MinEmigrationCityPopulation = 200;
    /// <summary>A province counts as settled once this many citizens live there; until then it draws migrants first.</summary>
    public const int SettledPopulation = 10;
    /// <summary>Settled provinces attract migrants until they reach this share of their capacity.</summary>
    public const double MigrationTargetShare = 0.5;
    public const int MinCityPopulation = 100;
    /// <summary>A province needs this many citizens before they can build themselves a city.</summary>
    public const int CityBuildingPopulation = 500;
    public static readonly ResourceCost CityCost = new((ResourceType.Wood, 150), (ResourceType.Gold, 50));
    public const int CityBuildingDays = 60;
    public const int MaxCityNameLength = 24;
    /// <summary>Gold paid per 10 citizens moved by a forced migration.</summary>
    public const double ForcedMigrationGoldPerTen = 1;

    public static double ForcedMigrationCost(int citizens) => Math.Ceiling(citizens / 10.0) * ForcedMigrationGoldPerTen;

    // Mood (0 to 100). Each day a province's mood closes part of the gap to the sum of its mood factors.
    public const double StartingMood = 60;
    public const double BaseMood = 50;
    public const double CityMood = 10;
    public const double CapitalMood = 15;
    /// <summary>Provinces lose a mood point per this many km from the capital, up to <see cref="MaxDistanceMoodPenalty"/>.</summary>
    public const double KmPerMoodPoint = 50;
    public const double MaxDistanceMoodPenalty = 20;
    /// <summary>Penalty once a province holds twice what its land feeds (proportionally less below that).</summary>
    public const double MaxOvercrowdingMoodPenalty = 20;
    public const double StarvingMood = -40;
    /// <summary>Forced migrants arrive this much unhappier than the province they were taken from.</summary>
    public const double ForcedMigrantMoodPenalty = 20;
    /// <summary>Full food-reserve bonus once the stockpile lasts <see cref="FoodReserveFullDays"/> days (proportionally less below).</summary>
    public const double FoodReserveMood = 10;
    public const double FoodReserveFullDays = 30;
    /// <summary>Festivals lift a city's mood for a while in exchange for gold.</summary>
    public const double FestivalMood = 20;
    public const int FestivalDays = 30;
    public const double FestivalGoldPerHundred = 2;
    public const double MinFestivalCost = 10;
    public const double MoodChangePerDay = 0.1;
    /// <summary>Below this mood a province is in unrest and pays no taxes.</summary>
    public const double UnrestMood = 25;

    // Peace treaties. A province's worth at the peace table is 1, plus 1 per PeoplePerValuePoint people (up to
    // MaxPopulationValue), plus its city; the war score is the share of the enemy's worth occupied, plus battles won.
    public const double PeoplePerValuePoint = 2000;
    public const double MaxPopulationValue = 5;
    public const double CityValue = 2;
    public const double CapitalValue = 6;
    public const double WarScorePerVictory = 2;
    public const double MaxBattleWarScore = 25;
    /// <summary>Days after a peace before the same two nations may go to war again.</summary>
    public const int TruceDays = 730;
    // Opinion between nations (-100 to 100): what each sees now, plus memories that fade by OpinionFadePerDay.
    public const double AllianceOpinion = 30;
    public const double AtWarOpinion = -50;
    public const double BorderOpinion = -10;
    public const double CommonEnemyOpinion = 25;
    /// <summary>Both border a nation stronger than the one judging.</summary>
    public const double CommonRivalOpinion = 20;
    /// <summary>Per province of its culture the other nation rules, up to <see cref="MaxRuledPeopleOpinion"/>.</summary>
    public const double RuledProvinceOpinion = 5;
    public const double MaxRuledPeopleOpinion = 30;
    public const double DeclaredWarOpinion = -60;
    /// <summary>Per province a treaty takes from the nation, remembered against the taker.</summary>
    public const double ProvinceTakenOpinion = -5;
    public const double BrokenAllianceOpinion = -60;
    public const double GiftOpinion = 15;
    public const double OpinionFadePerDay = 0.1;
    /// <summary>A gift costs this many days of the giver's gold income, and never less than <see cref="MinGiftGold"/>.</summary>
    public const double GiftIncomeDays = 30;
    public const double MinGiftGold = 50;
    /// <summary>A computer rival allies with nations it thinks this well of, at most <see cref="MaxAllies"/> at a time.</summary>
    public const double AllianceAcceptOpinion = 40;
    public const int MaxAllies = 3;
    /// <summary>Mood a province loses when a treaty hands it to another nation.</summary>
    public const double CededMoodPenalty = 20;
    // Reparations and vassals. A treaty may make the loser pay ReparationsShare of its gold income for ReparationsDays,
    // or become a vassal: it pays VassalTributeShare of its income, fights its overlord's wars and may be annexed after
    // VassalAnnexYears. Each costs a fixed war score.
    public const double ReparationsWarScore = 30;
    public const double ReparationsShare = 0.25;
    public const int ReparationsDays = 5 * 365;
    public const double ReparationsOpinion = -30;
    public const double VassalWarScore = 60;
    public const double VassalTributeShare = 0.1;
    public const double VassalAnnexYears = 10;
    /// <summary>What a vassal thinks of its overlord for being one, and the overlord of its vassal.</summary>
    public const double VassalOpinion = -10;
    public const double OverlordOpinion = 10;
    public const double ReleasedOpinion = 40;
    // Non-aggression pacts and military access. A computer rival signs a pact with nations it thinks at least
    // PactAcceptOpinion of (FearedPactOpinion if their army is stronger), and lets armies through if it thinks
    // AccessAcceptOpinion of them. Breaking a pact leaves a truce of BrokenPactTruceDays.
    public const double PactOpinion = 10;
    public const double PactAcceptOpinion = 0;
    public const double FearedPactOpinion = -25;
    public const double BrokenPactOpinion = -30;
    public const int BrokenPactTruceDays = 365;
    public const double AccessOpinion = 10;
    public const double AccessAcceptOpinion = 20;
    // Religion. People of another faith than their ruler's lose OtherFaithMood until they convert, in about
    // ConversionYears (faster when happy, with a temple and with Theology). Nations of one faith get on better.
    public const double OtherFaithMood = 10;
    public const double ConversionYears = 25;
    public const double TempleConversion = 1;
    public const double TheologyConversion = 0.5;
    public const double SameFaithOpinion = 10;
    public const double OtherFaithOpinion = -10;

    // Epidemics. A city may fall ill each day (more likely the bigger it is); the sickness lasts PlagueDays, kills
    // PlagueDeathRate of the people a day and costs PlagueMood, and spreads to neighbours (far more along roads and
    // between ports). Medicine and hospitals resist it. A province that has had it is immune for PlagueImmunityYears.
    public const double PlagueOutbreakChance = 1.0 / (365 * 40);
    public const double PlagueOutbreakPeople = 20000;
    public const int PlagueDays = 90;
    public const double PlagueDeathRate = 0.001;
    public const double PlagueMood = 15;
    public const double PlagueSpread = 0.005;
    public const double PlagueRoadSpread = 0.03;
    public const double PlaguePortSpread = 0.005;
    public const double PlaguePortKm = 2000;
    public const double PlagueImmunityYears = 10;
    public const double MaxPlagueResistance = 0.9;

    // Decisions. Each nation with a city meets about DecisionsPerYear events a year, each with two choices; one left
    // unanswered for DecisionDays takes its default. What is at stake grows with the square root of the nation's
    // people over DecisionPeople. Mood they change lasts DecisionMoodDays.
    public const double DecisionsPerYear = 1.5;
    public const int DecisionDays = 30;
    public const double DecisionPeople = 20000;
    public const int DecisionMoodDays = 365;

    // The ledger: every HistoryDays days each nation's figures are written down for the statistics graphs.
    public const int HistoryDays = 30;

    // Objectives: each one met pays ObjectiveGold; the population one asks for ObjectivePeople.
    public const double ObjectiveGold = 50;
    public const double ObjectivePeople = 10000;

    // Victory: by domination, by science or, at the start of ScoreVictoryYear, by score (one point per ScorePeople
    // people, plus ScorePerProvince, ScorePerCity and ScorePerTech for each province, city and advance).
    public const int ScoreVictoryYear = 300;
    public const double ScorePeople = 1000;
    public const double ScorePerProvince = 5;
    public const double ScorePerCity = 20;
    public const double ScorePerTech = 10;

    // Manpower. A nation's reserve of recruits holds BaseManpower plus ManpowerShare of its settled people (more with
    // advances) and refills in ManpowerRecoveryYears. Training, raising HQs and reinforcing draw on it.
    public const double BaseManpower = 500;
    public const double ManpowerShare = 0.06;
    public const double ManpowerRecoveryYears = 2;

    // Trade. A nation sells TradeSurplusShare of what it gains of a resource each day, at its gold value
    // (ResourceValue); a computer rival keeps a margin of up to MaxTradeMargin, less the better it thinks of the
    // other. Deals last TradeDealDays and each one lifts opinion a little on both sides.
    public const double TradeSurplusShare = 0.5;
    public const double MaxTradeMargin = 0.5;
    public const double MinTradeGoldPerDay = 1;
    public const int TradeDealDays = 365;
    public const int MaxTradeDeals = 6;
    public const double TradeAcceptOpinion = -25;
    public const double TradeOpinion = 5;
    public const double MaxTradeOpinion = 15;
    public const double CancelledTradeOpinion = -10;

    /// <summary>What a unit of each resource is worth in gold, for trade.</summary>
    public static double ResourceValue(ResourceType resource) => resource switch
    {
        ResourceType.Food => 0.1,
        ResourceType.Wood => 0.5,
        ResourceType.Coal or ResourceType.Copper => 1.5,
        ResourceType.Stone => 0.5,
        ResourceType.Iron or ResourceType.Silver or ResourceType.Sulfur => 2,
        ResourceType.Rubber => 3,
        ResourceType.Silicon or ResourceType.Oil or ResourceType.Aluminium => 4,
        _ => 1,
    };

    /// <summary>The share of the value a computer rival keeps for itself: half when it hates the other, nothing when it loves it.</summary>
    public static double TradeMargin(double opinion) => Math.Clamp(MaxTradeMargin / 2 - opinion / 400, 0, MaxTradeMargin);

    // Seasons. Winter bites from MildWinterLatitude and is at its worst from HarshWinterLatitude; spring and autumn
    // bring MudShare of its slowdown as mud. Armies lose men to cold out of their cities, and to the desert all year.
    public const double MildWinterLatitude = 30;
    public const double HarshWinterLatitude = 60;
    public const double MudShare = 0.5;
    /// <summary>Marching through the deepest snow takes this much longer (1 = twice as long).</summary>
    public const double WinterSlowdown = 1;
    /// <summary>Share of its men a regiment loses each day in the harshest winter, out of its nation's cities.</summary>
    public const double WinterAttrition = 0.005;
    public const double DesertAttrition = 0.002;
    /// <summary>Share of its men a regiment loses each day on the peaks and the polar ice, and in the high mountains, all year.</summary>
    public const double PeakAttrition = 0.004;
    public const double HighMountainAttrition = 0.001;

    // Culture and revolt. People under a foreign ruler lose ForeignCultureMood, less as they assimilate over about
    // AssimilationYears (faster when happy). A province in unrest with no garrison revolts after RevoltDays or fewer.
    public const double ForeignCultureMood = 25;
    public const double AssimilationYears = 15;
    public const double MinAssimilationPace = 0.25;
    public const double MaxAssimilationPace = 2;
    public const double RevoltDays = 60;
    public const double RevoltCalmingPerDay = 2;
    /// <summary>Share of the people a riot kills.</summary>
    public const double RevoltDeaths = 0.1;
    /// <summary>Mood a province is left with at least after a riot, or after rejoining the nation of its culture.</summary>
    public const double AfterRiotMood = 35;
    public const double LiberatedMood = 60;

    // Fertility (birth-rate multiplier, 1 = normal). It follows mood and food, more slowly than mood.
    public const double FertilityChangePerDay = 0.03;
    /// <summary>Share of normal fertility left while the nation starves.</summary>
    public const double StarvingFertility = 0.2;

    public static double FestivalCost(double population) =>
        Math.Max(MinFestivalCost, Math.Ceiling(population / 100) * FestivalGoldPerHundred);

    /// <summary>Output multiplier of a province's workers: 0.75 when furious, 1 at mood 50, 1.25 when delighted.</summary>
    public static double MoodProductivity(double mood) => 0.75 + mood / 200;

    /// <summary>Fertility a province tends to: 0.5 at mood 0, 1 at mood 50, 1.5 at mood 100; hunger cuts it.</summary>
    public static double TargetFertility(double mood, bool starving) => (0.5 + mood / 100) * (starving ? StarvingFertility : 1);

    /// <summary>Mood levels from worst to best, as <see cref="MoodLevel"/> numbers them.</summary>
    public static readonly string[] MoodNames = ["Descontento", "Inquieto", "Tranquilo", "Contento"];

    public static int MoodLevel(double mood) => mood switch
    {
        < UnrestMood => 0,
        < 45 => 1,
        < 65 => 2,
        _ => 3,
    };

    public static string MoodName(double mood) => MoodNames[MoodLevel(mood)];
}

public enum UnitType
{
    Settlers,
    Regiment,
    Headquarters,
    /// <summary>Ships: warships fight other fleets, transports carry troops over the sea.</summary>
    Fleet,
}

/// <summary>What scouts left on their own do.</summary>
public enum ScoutOrders
{
    None,
    /// <summary>They walk to the nearest land the nation has never seen, without claiming anything.</summary>
    Explore,
    /// <summary>They walk to the best free province along the nation's borders and claim it.</summary>
    Claim,
}
