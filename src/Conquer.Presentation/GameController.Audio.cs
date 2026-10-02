using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>What the game sounds like: the sound of each event that concerns the player, and the music to play.</summary>
public sealed partial class GameController
{
    private readonly List<SoundCue> _sounds = [];
    /// <summary>The serious alerts shown at the last hour of game time, so that a new one rings once.</summary>
    private HashSet<string> _seriousAlerts = [];
    private long _alertsCheckedHour = -1;

    /// <summary>The sounds to play since the last call, which empties the list.</summary>
    public IReadOnlyList<SoundCue> TakeSounds()
    {
        var sounds = _sounds.ToList();
        _sounds.Clear();
        return sounds;
    }

    /// <summary>The tracks that fit the moment: the player's age, at war or at peace.</summary>
    public IReadOnlyList<string> Playlist => Soundtrack.For(Human.Era, Session.EnemiesOf(Human.Id).Any());

    /// <summary>Queues a sound; with nobody listening (in tests) the queue stays short.</summary>
    public void Play(SoundCue cue)
    {
        if (_sounds.Count < 32) _sounds.Add(cue);
    }

    /// <summary>The sound of what happened, if it concerns the player.</summary>
    private void Hear(GameEvent e)
    {
        if (!e.Involves(Human.Id)) return;
        SoundCue? cue = e.Kind switch
        {
            GameEventKind.WarDeclared => SoundCue.War,
            GameEventKind.PeaceSigned => SoundCue.Peace,
            GameEventKind.BattleStarted => SoundCue.Battle,
            GameEventKind.SiegeLaid or GameEventKind.Revolt => SoundCue.Bell,
            GameEventKind.GiftSent => SoundCue.Coins,
            // Only one's own cities, buildings and advances.
            GameEventKind.CityFounded when e.PlayerId == Human.Id => SoundCue.CityFounded,
            GameEventKind.BuildingFinished when e.PlayerId == Human.Id => SoundCue.Build,
            GameEventKind.AdvanceDiscovered when e.PlayerId == Human.Id => SoundCue.Discovery,
            _ => null,
        };
        if (cue is SoundCue c) Play(c);
    }

    /// <summary>Once per hour of game time: a serious alert that was not there before rings.</summary>
    private void ListenForAlerts()
    {
        if (Session.Date.Hours == _alertsCheckedHour) return;
        _alertsCheckedHour = Session.Date.Hours;
        var serious = Alerts().Where(a => a.Tone == Tone.Bad).Select(a => a.Text.Split(" (")[0]).ToHashSet();
        if (serious.Except(_seriousAlerts).Any()) Play(SoundCue.Alert);
        _seriousAlerts = serious;
    }
}
