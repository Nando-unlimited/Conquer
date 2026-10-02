using Conquer.Game.Buildings;
using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Cultures and rebellions. Each province's people belong to the culture of the nation that settled it; under a
/// foreign ruler they are unhappy until they assimilate. A province in unrest with no garrison heads for revolt: it
/// rejoins the nation of its culture if that one still exists, or riots otherwise.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The nation whose culture a province's people share; null for empty land.</summary>
    public Player? CultureOf(Province p) => p.CultureId >= 0 ? Players[p.CultureId] : null;

    /// <summary>Whether the people are of a culture other than their ruler's.</summary>
    public static bool HasForeignCulture(Province p) => p.IsOwned && p.CultureId >= 0 && p.CultureId != p.OwnerId && p.Population >= 1;

    /// <summary>Mood a province loses for its foreign culture: all of it when just taken, none once assimilated.</summary>
    public static double ForeignCultureMood(Province p) =>
        HasForeignCulture(p) ? -GameRules.ForeignCultureMood * (1 - p.Assimilation) : 0;

    /// <summary>Share of the way to adopting the ruler's culture gained in a day: faster the happier the people.</summary>
    public static double DailyAssimilation(Province p) =>
        Math.Clamp(p.Mood / 50, GameRules.MinAssimilationPace, GameRules.MaxAssimilationPace) / (GameRules.AssimilationYears * 365);

    /// <summary>Whether a regiment of the province's ruler stands there, keeping order.</summary>
    public bool IsGarrisoned(Province p) =>
        Units.Any(u => u.IsMilitary && !u.IsAboard && u.OwnerId == p.OwnerId && u.ProvinceId == p.Id);

    /// <summary>How close a province is to revolt, from 0 to 1.</summary>
    public static double RevoltRisk(Province p) => Math.Clamp(p.RevoltProgress / GameRules.RevoltDays, 0, 1);

    /// <summary>
    /// Whether a revolt there would hand the province to the nation of its culture (otherwise it riots): its people
    /// are foreign, that nation still holds land, and the province is not its ruler's capital.
    /// </summary>
    public bool WouldSecede(Province p) =>
        HasForeignCulture(p) && Players[p.CultureId].Provinces.Count > 0
        && !(CityIn(p) is { } city && Players[p.OwnerId].CapitalCityId == city.Id);

    /// <summary>
    /// Each day: provinces assimilate toward their ruler's culture, empty ones take it at once, and those in unrest
    /// without a garrison draw closer to revolt (the angrier, the faster); calm ones settle down again.
    /// </summary>
    private void DailyUnrest(Player player)
    {
        var garrisoned = Units.Where(u => u.IsMilitary && !u.IsAboard && u.OwnerId == player.Id).Select(u => u.ProvinceId).ToHashSet();
        foreach (int id in player.Provinces.ToList())
        {
            var p = Map.Provinces[id];
            if (p.Population < 1)
            {
                p.CultureId = player.Id;
                p.Assimilation = 0;
                p.RevoltProgress = 0;
                continue;
            }
            if (p.IsOccupied) continue;
            if (HasForeignCulture(p) && (p.Assimilation += DailyAssimilation(p)) >= 1)
            {
                var old = Players[p.CultureId];
                p.CultureId = player.Id;
                p.Assimilation = 0;
                if (player.Id == HumanPlayerId) Notify(player.Id, $"{PlaceName(p)} deja atrás la cultura de {old.Name} y adopta la nuestra.");
            }

            if (p.Mood >= GameRules.UnrestMood)
                p.RevoltProgress = Math.Max(0, p.RevoltProgress - GameRules.RevoltCalmingPerDay);
            else if (!garrisoned.Contains(p.Id))
            {
                double before = p.RevoltProgress;
                p.RevoltProgress += 1 + (GameRules.UnrestMood - p.Mood) / GameRules.UnrestMood;
                if (player.Id == HumanPlayerId && before < GameRules.RevoltDays / 2 && p.RevoltProgress >= GameRules.RevoltDays / 2)
                    Notify(player.Id, $"{PlaceName(p)} está al borde de la rebelión. Sube su moral o envía tropas.");
                if (p.RevoltProgress >= GameRules.RevoltDays) Revolt(p);
            }
        }
    }

    /// <summary>
    /// The province rises. Foreign people rejoin the nation of their culture, happy to be home; otherwise the riots
    /// kill some of the people, burn a building and leave the province exhausted but calmer.
    /// </summary>
    private void Revolt(Province p)
    {
        p.RevoltProgress = 0;
        int ruler = p.OwnerId;
        Raise(GameEventKind.Revolt, ruler, p.CultureId);
        string place = PlaceName(p);
        if (WouldSecede(p))
        {
            int culture = p.CultureId;
            Cede(p, culture);
            p.Mood = Math.Max(p.Mood, GameRules.LiberatedMood);
            if (!AtWar(ruler, culture))
                foreach (var unit in Units.Where(u => u.OwnerId == ruler && u.ProvinceId == p.Id).ToList()) SendHome(unit);
            if (ruler == HumanPlayerId) Notify(ruler, $"¡{place} se subleva y se une a {Players[culture].Name}!");
            else if (culture == HumanPlayerId) Notify(culture, $"{place} se subleva contra {Players[ruler].Name} y vuelve con nosotros.");
            return;
        }

        p.Population *= 1 - GameRules.RevoltDeaths;
        BuildingType? burnt = p.Buildings.Count == 0 ? null : p.Buildings.Order().ElementAt(_random.Next(p.Buildings.Count));
        if (burnt is BuildingType b) p.RemoveBuilding(b);
        p.Mood = Math.Max(p.Mood, GameRules.AfterRiotMood);
        if (ruler == HumanPlayerId)
            Notify(ruler, $"Revuelta en {place}: muere el {GameRules.RevoltDeaths:P0} de su gente"
                + (burnt is BuildingType gone ? $" y arde su {gone.Info().Name.ToLowerInvariant()}." : "."));
    }

    /// <summary>A unit standing in land its nation no longer holds walks back to its nearest province, or disbands with nowhere to go.</summary>
    private void SendHome(Unit unit)
    {
        var here = Map.Provinces[unit.ProvinceId];
        unit.Path.Clear();
        unit.StepHours = unit.HoursToNext = 0;
        var home = Players[unit.OwnerId].Provinces.Where(id => Map.Provinces[id].ControllerId == unit.OwnerId)
            .OrderBy(id => Map.DistanceKm(here, Map.Provinces[id])).Select(id => (int?)id).FirstOrDefault();
        if (home is int h) unit.ProvinceId = h;
        else RemoveUnit(unit);
    }
}
