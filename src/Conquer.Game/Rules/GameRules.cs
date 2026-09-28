using Conquer.Game.Economy;

namespace Conquer.Game.Rules;

/// <summary>Tuning numbers for the whole game. Rates are per in-game day unless stated otherwise.</summary>
public static class GameRules
{
    // Starting conditions
    public const int StartingCitizens = 300;
    public const double StartingFood = 600;
    public const double StartingGold = 50;
    public const double StartingWood = 100;
    /// <summary>A band of settlers: <see cref="StartingCitizens"/> citizens from the city that sends them, plus this.</summary>
    public static readonly ResourceCost SettlersCost = new((ResourceType.Food, 150), (ResourceType.Wood, 50), (ResourceType.Gold, 20));

    // Movement
    /// <summary>How far a citizen walks in an hour on open ground; units and migrants travel at this pace.</summary>
    public const double CitizenSpeedKmh = 10;

    // Population
    /// <summary>Food each citizen eats per day (units and migrants on the road too).</summary>
    public const double FoodPerCitizen = 0.1;
    /// <summary>Food a worker grows per day on land of yield 1; most farmland feeds more people than work it.</summary>
    public const double FoodPerWorker = 0.13;
    /// <summary>Daily growth of a fed province (about +55% a year), damped as it fills up.</summary>
    public const double GrowthRate = 0.0012;
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
    /// <summary>Workers beyond the land's capacity still produce this share of normal food.</summary>
    public const double OvercrowdedFoodShare = 0.3;

    // Science
    /// <summary>Science points every city produces per day on top of its citizens'.</summary>
    public const double ScienceBasePerCity = 0.5;
    /// <summary>Science points each city dweller produces per day.</summary>
    public const double SciencePerCityCitizen = 0.001;

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
    Division,
    Headquarters,
}
