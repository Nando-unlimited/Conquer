using Conquer.Game.Entities;

namespace Conquer.Game.Simulation;

/// <summary>
/// The fog of war: what a nation can see. It sees its land and the land it occupies, its allies', overlord's and vassals',
/// one province around all of it, and around each of their units; scouts see a province further. Units elsewhere are
/// hidden from it. What it has seen once stays explored; the rest of the world is unknown to it.
/// </summary>
public sealed partial class GameSession
{
    private readonly Dictionary<int, HashSet<int>> _visible = [];
    private (long Hour, int Units) _visibleStamp = (-1, 0);

    /// <summary>Nations that share what they see with the player: itself, its allies, its overlord and its vassals.</summary>
    public bool SharesVision(int playerId, int otherId) => playerId == otherId || AreAllied(playerId, otherId) || InVassalage(playerId, otherId);

    /// <summary>The provinces a nation can see this hour.</summary>
    public IReadOnlySet<int> VisibleProvinces(int playerId)
    {
        // Worked out again each hour, or when units appear or vanish.
        if (_visibleStamp != (Date.Hours, Units.Count))
        {
            _visible.Clear();
            _visibleStamp = (Date.Hours, Units.Count);
        }
        if (_visible.TryGetValue(playerId, out var seen)) return seen;
        seen = [];
        var friends = Players.Where(p => !p.Eliminated && SharesVision(playerId, p.Id)).Select(p => p.Id).ToHashSet();
        foreach (var p in Map.Provinces.Where(p => p.IsOwned && (friends.Contains(p.ControllerId) || friends.Contains(p.OwnerId))))
            See(p.Id, 1);
        foreach (var unit in Units.Where(u => friends.Contains(u.OwnerId) && !u.IsAboard))
        {
            See(unit.ProvinceId, unit.HasScouts ? 2 : 1);
            if (unit.IsMoving) See(unit.Path[0], 0);
        }
        _visible[playerId] = seen;
        Players[playerId].Explored.UnionWith(seen);
        return seen;

        void See(int provinceId, int rings)
        {
            seen.Add(provinceId);
            if (rings <= 0) return;
            foreach (int n in Map.Provinces[provinceId].Neighbors) See(n, rings - 1);
        }
    }

    /// <summary>Whether two nations have met (a nation always knows itself).</summary>
    public bool HasContact(int playerId, int otherId) => playerId == otherId || Players[playerId].Contacts.Contains(otherId);

    /// <summary>
    /// Nations meet when one has explored land the other holds or sees its troops, or when they are bound by war, an
    /// alliance, vassalage or trade. Once met, they stay known to each other.
    /// </summary>
    public void UpdateContacts()
    {
        foreach (var player in Players.Where(p => !p.Eliminated))
        {
            var seen = VisibleProvinces(player.Id);
            foreach (int id in player.Explored)
            {
                var p = Map.Provinces[id];
                if (p.IsOwned) Meet(player.Id, p.OwnerId);
                if (p.ControllerId >= 0) Meet(player.Id, p.ControllerId);
            }
            foreach (var unit in Units.Where(u => !u.IsAboard && seen.Contains(u.ProvinceId))) Meet(player.Id, unit.OwnerId);
            foreach (var other in Players.Where(o => AtWar(player.Id, o.Id) || AreAllied(player.Id, o.Id) || InVassalage(player.Id, o.Id)))
                Meet(player.Id, other.Id);
            foreach (var deal in TradesOf(player.Id)) Meet(deal.SellerId, deal.BuyerId);
        }

        void Meet(int a, int b)
        {
            if (a == b || a < 0 || b < 0) return;
            Players[a].Contacts.Add(b);
            Players[b].Contacts.Add(a);
        }
    }

    /// <summary>Whether a nation has ever seen a province.</summary>
    public bool HasExplored(int playerId, int provinceId) => Players[playerId].Explored.Contains(provinceId);

    /// <summary>Whether a nation can see a unit: its own and its friends' always, any other only in a province it sees.</summary>
    public bool CanSee(int playerId, Unit unit) =>
        SharesVision(playerId, unit.OwnerId) || (!unit.IsAboard && VisibleProvinces(playerId).Contains(unit.ProvinceId))
        || (unit.CarrierId is int fleet && UnitById(fleet) is { } carrier && CanSee(playerId, carrier));
}
