using Conquer.Game.Economy;
using Conquer.Game.Rules;
using Conquer.Game.Simulation;

namespace Conquer.Game.AI;

/// <summary>
/// The rival's friends: it allies with nations it thinks well of, courts with gifts those it nearly trusts, and
/// counts its victims' allies before attacking.
/// </summary>
internal sealed partial class AiPlayer
{
    /// <summary>On average, a rival looks for allies this often (in days).</summary>
    private const int AllianceChanceDays = 60;
    /// <summary>It seeks as allies nations it thinks at least this well of (a common rival or enemy is enough).</summary>
    private const double CourtingOpinion = 15;

    /// <summary>Whether it signs an alliance: it thinks well enough of the other and has room for another ally.</summary>
    public bool WouldAlly(int otherId) =>
        _session.Opinion(_player.Id, otherId) >= GameRules.AllianceAcceptOpinion
        && _session.AlliesOf(_player.Id).Count() < GameRules.MaxAllies
        && !_session.EnemiesOf(_player.Id).Any(e => _session.AreAllied(e.Id, otherId));

    /// <summary>
    /// Now and then, offers an alliance to the rival it thinks best of; if that one does not think well enough of it yet
    /// but is not hostile, and there is gold to spare, it sends a gift instead.
    /// </summary>
    private void SeekAlliances()
    {
        if (_random.Next(AllianceChanceDays) != 0 || _session.AlliesOf(_player.Id).Count() >= GameRules.MaxAllies) return;
        var friend = _session.Players.Where(p => !p.IsHuman && p.Id != _player.Id && p.Provinces.Count > 0
                                                 && _session.CanProposeAlliance(_player.Id, p.Id).Ok
                                                 && _session.Opinion(_player.Id, p.Id) >= CourtingOpinion)
            .MaxBy(p => _session.Opinion(_player.Id, p.Id));
        if (friend == null) return;
        if (_session.ProposeAlliance(_player.Id, friend.Id).Ok) return;
        double theirs = _session.Opinion(friend.Id, _player.Id);
        if (theirs >= 0 && _player.Stockpile[ResourceType.Gold] - GameSession.GiftCost(_player) >= GoldKeptForRecruiting * 3)
            _session.SendGift(_player.Id, friend.Id);
    }

    /// <summary>
    /// The army it would face attacking a nation: that nation's, plus its allies' who would come to its aid, and less
    /// how much it dislikes it (a hated neighbour looks weaker than it is).
    /// </summary>
    private double DefendingPower(int targetId)
    {
        double power = _session.MilitaryPower(targetId) + _session.AlliesOf(targetId)
            .Where(a => a.Id != _player.Id && !_session.AreAllied(a.Id, _player.Id)).Sum(a => _session.MilitaryPower(a.Id));
        return power * (1 + _session.Opinion(_player.Id, targetId) / 200);
    }
}
