using Conquer.Game.Rules;
using Conquer.Game.World;

namespace Conquer.Game.Simulation;

/// <summary>
/// Real names for cities and provinces: each nation takes them from its own country (<see cref="Countries"/>), then
/// from the countries nobody plays, then from the other nations'; only when every real name is taken does it make one up.
/// </summary>
public sealed partial class GameSession
{
    /// <summary>The countries in the order the nation borrows their names: its own, those nobody plays, the rest.</summary>
    private IEnumerable<Country> NameSources(int playerId)
    {
        var own = Countries.ByName(Players[playerId].Name);
        var playing = Players.Select(p => p.Name).ToHashSet();
        // Each nation borrows in an order of its own, so they do not all empty the same country first.
        var order = new Random(playerId * 7919 + 17);
        var others = Countries.All.Where(c => c != own).OrderBy(_ => order.Next()).ToList();
        return (own is null ? [] : new[] { own }).Concat(others.Where(c => !playing.Contains(c.Name))).Concat(others.Where(c => playing.Contains(c.Name)));
    }

    private bool IsFreeName(string name) => !_usedCityNames.Contains(name) && !_usedProvinceNames.Contains(name);

    /// <summary>Real city names no city, planned city or province has taken, best first for the nation.</summary>
    private IEnumerable<string> FreeCityNames(int playerId) => NameSources(playerId).SelectMany(c => c.Cities).Where(IsFreeName);

    /// <summary>
    /// A city name for the nation to offer the player, not reserved: the first free one, or with <paramref name="after"/>
    /// the one that follows it (back to the first after the last), to try another.
    /// </summary>
    public string SuggestCityName(int playerId, string? after = null)
    {
        var free = FreeCityNames(playerId).Take(40).ToList();
        if (free.Count == 0) return CityNames.Suggest(_usedCityNames, Random.Shared);
        int index = after == null ? -1 : free.IndexOf(after);
        return free[(index + 1) % free.Count];
    }

    /// <summary>The name a computer nation gives its next city.</summary>
    internal string NextCityName(int playerId) => FreeCityNames(playerId).FirstOrDefault() ?? CityNames.Suggest(_usedCityNames, _random);

    private static readonly string[] Quarters = ["Norte de", "Sur de", "Este de", "Oeste de"];

    /// <summary>
    /// The first nation to claim a province names it, and the name stays whoever holds it later: one of its country's
    /// regions, another country's, a city name nobody has taken or, once every real name is taken, a quarter of one of
    /// the regions ("Norte de Castilla", "Sur de Aragón").
    /// </summary>
    private void NameProvince(Province p, int playerId)
    {
        if (p.Name.Length > 0 || !p.IsClaimable) return;
        var sources = NameSources(playerId).ToList();
        string? name = sources.SelectMany(c => c.Regions).FirstOrDefault(IsFreeName)
                       ?? sources.Skip(1).SelectMany(c => c.Cities).FirstOrDefault(IsFreeName)
                       ?? Quarters.SelectMany(q => sources.SelectMany(c => c.Regions).Select(r => $"{q} {r}"))
                           .FirstOrDefault(n => n.Length <= GameRules.MaxCityNameLength && IsFreeName(n));
        if (name != null) _usedProvinceNames.Add(name);
        p.Name = name ?? ProvinceNames.Next(_usedProvinceNames, _random);
    }
}
