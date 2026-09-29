using Conquer.Game.Entities;
using Conquer.Game.Rules;
using Conquer.Game.Science;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>Institutions: where they are born, how they spread from province to province and when nations adopt them.</summary>
public sealed partial class GameSession
{
    /// <summary>Where and when each institution was born; absent until it is.</summary>
    private readonly Dictionary<Institution, (int ProvinceId, long Hours)> _institutionBirths = [];

    public bool IsBorn(Institution institution) => _institutionBirths.ContainsKey(institution);

    /// <summary>The province it was born in, or null before its birth.</summary>
    public int? BirthplaceOf(Institution institution) =>
        _institutionBirths.TryGetValue(institution, out var birth) ? birth.ProvinceId : null;

    private void DailyInstitutions()
    {
        foreach (var institution in Institutions.All.Where(i => !IsBorn(i)))
            if (Birthplace(institution) is Province p) Born(institution, p);
        SpreadInstitutions();
        foreach (var player in Players) AdoptWhenWidespread(player);
    }

    /// <summary>Where an institution is born today, if its conditions are met anywhere.</summary>
    private Province? Birthplace(Institution institution) => institution switch
    {
        Institution.Urbanism => Cities.Select(c => Map.Provinces[c.ProvinceId])
            .Where(p => p.Population >= GameRules.UrbanismBirthPopulation).MaxBy(p => p.Population),
        Institution.Feudalism => Players.Where(p => Cities.Count(c => c.OwnerId == p.Id) >= GameRules.FeudalismBirthCities)
            .Select(p => p.CapitalCityId is int id ? CityById(id) : null).OfType<City>().Select(c => Map.Provinces[c.ProvinceId]).FirstOrDefault(),
        _ => null,
    };

    private void Born(Institution institution, Province p)
    {
        _institutionBirths[institution] = (p.Id, Date.Hours);
        var info = institution.Info();
        Notify(HumanPlayerId, $"Nace {info.The} en {PlaceName(p)}. Se extenderá por las provincias vecinas, y tu nación adoptará " +
                              $"esta institución cuando llegue a la mitad de tu población, o antes pagando oro. {info.Description}");
        Reach(p, institution);
    }

    /// <summary>
    /// Each settled province without an institution may catch it from every neighbour that has it; cities
    /// catch it faster. Decided for all provinces before any changes, so it moves one step a day at most.
    /// </summary>
    private void SpreadInstitutions()
    {
        foreach (var institution in Institutions.All.Where(IsBorn))
        {
            var reached = new List<Province>();
            foreach (var p in Map.Provinces)
            {
                if (p.Institutions.Contains(institution) || p.Population < GameRules.SettledPopulation) continue;
                int sources = p.Neighbors.Count(n => Map.Provinces[n].Institutions.Contains(institution));
                if (sources == 0) continue;
                double chance = GameRules.InstitutionSpreadChance * (p.CityId.HasValue ? GameRules.CityInstitutionSpread : 1);
                if (_random.NextDouble() < 1 - Math.Pow(1 - chance, sources)) reached.Add(p);
            }
            foreach (var p in reached) Reach(p, institution);
        }
    }

    /// <summary>The institution arrives in a province; the human hears when it first enters their nation.</summary>
    private void Reach(Province p, Institution institution)
    {
        if (!p.Institutions.Add(institution) || p.OwnerId != HumanPlayerId || Human.Institutions.Contains(institution)) return;
        if (Human.Provinces.Count(id => Map.Provinces[id].Institutions.Contains(institution)) == 1)
            Notify(HumanPlayerId, $"{Capitalized(institution.Info().The)} llega a tu nación, a {PlaceName(p)}.");
    }

    /// <summary>Share of the nation's settled people living where the institution has arrived.</summary>
    public double InstitutionShare(Player player, Institution institution)
    {
        double total = 0, with = 0;
        foreach (int id in player.Provinces)
        {
            var p = Map.Provinces[id];
            total += p.Population;
            if (p.Institutions.Contains(institution)) with += p.Population;
        }
        return total > 0 ? with / total : 0;
    }

    private void AdoptWhenWidespread(Player player)
    {
        foreach (var institution in Institutions.All.Where(i => IsBorn(i) && !player.Institutions.Contains(i)))
            if (InstitutionShare(player, institution) >= GameRules.InstitutionAdoptionShare) Adopted(player, institution);
    }

    private void Adopted(Player player, Institution institution)
    {
        player.Adopt(institution);
        var info = institution.Info();
        if (player.IsHuman)
            Notify(player.Id, $"Tu nación adopta {info.The}. {info.Description} Los avances de la era {info.Opens.Name()} ya no te cuestan más.");
    }

    /// <summary>Gold to adopt it now: so much per citizen who does not have it yet, with a minimum.</summary>
    public double AdoptionCost(Player player, Institution institution)
    {
        double without = player.Provinces.Select(id => Map.Provinces[id]).Where(p => !p.Institutions.Contains(institution)).Sum(p => p.Population);
        return Math.Max(GameRules.MinInstitutionGold, Math.Round(without * GameRules.InstitutionGoldPerCitizen));
    }

    public CommandResult CanAdopt(Player player, Institution institution)
    {
        if (player.Institutions.Contains(institution)) return CommandResult.Fail("Ya la has adoptado.");
        if (!IsBorn(institution)) return CommandResult.Fail("Todavía no ha nacido.");
        if (!player.Provinces.Any(id => Map.Provinces[id].Institutions.Contains(institution)))
            return CommandResult.Fail("Aún no ha llegado a ninguna de tus provincias.");
        double cost = AdoptionCost(player, institution);
        if (player.Stockpile[Economy.ResourceType.Gold] < cost) return CommandResult.Fail($"Cuesta {cost:N0} de oro.");
        return CommandResult.Success();
    }

    /// <summary>Adopts an institution before it has spread through the nation, paying gold.</summary>
    public CommandResult Adopt(int playerId, Institution institution)
    {
        var player = Players[playerId];
        var check = CanAdopt(player, institution);
        if (!check.Ok) return check;
        player.Stockpile[Economy.ResourceType.Gold] -= AdoptionCost(player, institution);
        Adopted(player, institution);
        return CommandResult.Success();
    }

    private static string Capitalized(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>What the advances of an age cost the player: more for each of its institutions not yet adopted.</summary>
    public static double EraCostMultiplier(Player player, Era era) =>
        1 + GameRules.InstitutionPenalty * Institutions.All.Count(i => i.Info().Opens == era && !player.Institutions.Contains(i));
}
