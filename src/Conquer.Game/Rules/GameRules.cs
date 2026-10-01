using Conquer.Game.Economy;

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
    /// <summary>Daily growth of a fed province (about +55% a year), damped as it fills up.</summary>
    public const double GrowthRate = 0.0012;
    /// <summary>
    /// Daily births in every populated province per citizen its land can feed, on top of <see cref="GrowthRate"/>,
    /// so the countryside grows by its own fertility and not only through migrants.
    /// </summary>
    public const double BaseBirthsPerCapacity = 0.00002;
    public const double StarvationRate = 0.01;
    /// <summary>A city feeds this many times the citizens of the surrounding land.</summary>
    public const double CityCapacityMultiplier = 2.5;
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
    public const double ScienceBasePerCity = 0.5;
    /// <summary>Science points each city dweller produces per day.</summary>
    public const double SciencePerCityCitizen = 0.001;
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
    public const double DailyEmigrationShare = 0.0015;
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
    /// <summary>Mood a province loses when a treaty hands it to another nation.</summary>
    public const double CededMoodPenalty = 20;

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
