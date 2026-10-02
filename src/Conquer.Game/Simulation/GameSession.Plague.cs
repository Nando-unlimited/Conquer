using Conquer.Game.Buildings;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Epidemics. Now and then a city falls ill, more likely the bigger it is. For <see cref="GameRules.PlagueDays"/> days the
/// sickness kills part of the people every day and saddens the rest, and it spreads to neighbouring provinces, far
/// more readily along roads and between ports. Medicine, sanitation, antibiotics, herbalists and hospitals resist it;
/// a province that has had it is immune for some years.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>Epidemics draw on a generator of their own, so the rest of the game keeps its course.</summary>
    private Random? _plagueRandom;

    private Random PlagueRandom => _plagueRandom ??= new Random(_seed ^ 0x9A6E);

    public static bool IsSick(Province p) => p.PlagueDaysLeft > 0;

    /// <summary>Share of an epidemic's deaths and spread avoided in a province: its owner's advances and its own buildings, up to 90 %.</summary>
    public double PlagueResistance(Province p) => Math.Min(GameRules.MaxPlagueResistance, BonusesOf(p).PlagueResistance);

    /// <summary>Share of its people a sick province loses each day.</summary>
    public double PlagueDeaths(Province p) => GameRules.PlagueDeathRate * (1 - PlagueResistance(p));

    /// <summary>
    /// Each day: sick provinces lose people and the sickness spreads from them; then a few healthy cities fall ill on
    /// their own. Provinces whose epidemic has run its course become immune for a while.
    /// </summary>
    private void DailyPlague()
    {
        var sick = Map.Provinces.Where(IsSick).ToList();
        var ports = Map.Provinces.Where(p => p.CityId.HasValue && p.Buildings.Contains(BuildingType.Port)).ToList();
        foreach (var p in sick)
        {
            p.Population *= 1 - PlagueDeaths(p);
            foreach (int n in p.Neighbors)
            {
                var next = Map.Provinces[n];
                double chance = Roads.Between(p.Id, n) is not null ? GameRules.PlagueRoadSpread : GameRules.PlagueSpread;
                TryInfect(next, chance * (1 - PlagueResistance(p)));
            }
            if (p.CityId.HasValue && p.Buildings.Contains(BuildingType.Port))
                foreach (var port in ports.Where(q => q.Id != p.Id && Map.DistanceKm(p, q) <= GameRules.PlaguePortKm))
                    TryInfect(port, GameRules.PlaguePortSpread * (1 - PlagueResistance(p)));
            if (--p.PlagueDaysLeft > 0) continue;
            p.PlagueImmuneUntil = Date.Hours + (long)(GameRules.PlagueImmunityYears * 365 * 24);
            if (p.OwnerId == HumanPlayerId) Notify(HumanPlayerId, $"La epidemia de {PlaceName(p)} ha terminado.");
        }
        foreach (var city in Cities)
        {
            var p = Map.Provinces[city.ProvinceId];
            double chance = GameRules.PlagueOutbreakChance * (1 + p.Population / GameRules.PlagueOutbreakPeople) * (1 - PlagueResistance(p));
            if (!IsSick(p) && TryInfect(p, chance) && p.OwnerId == HumanPlayerId)
                Notify(HumanPlayerId, $"¡Epidemia en {PlaceName(p)}! Durará unos {GameRules.PlagueDays} días y puede extenderse por los caminos y los puertos.");
        }
    }

    /// <summary>A healthy, populated province not yet immune falls ill with the given chance. Returns whether it did.</summary>
    private bool TryInfect(Province p, double chance)
    {
        if (IsSick(p) || p.Population < 1 || p.PlagueImmuneUntil > Date.Hours || PlagueRandom.NextDouble() >= chance) return false;
        StartPlague(p);
        return true;
    }

    /// <summary>The province falls ill for <see cref="GameRules.PlagueDays"/> days.</summary>
    internal void StartPlague(Province p)
    {
        bool spread = Map.Provinces.Any(IsSick);
        p.PlagueDaysLeft = GameRules.PlagueDays;
        if (p.OwnerId == HumanPlayerId && spread && !p.CityId.HasValue)
            Notify(HumanPlayerId, $"La epidemia llega a {PlaceName(p)}.");
    }
}
