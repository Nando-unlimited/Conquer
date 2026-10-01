namespace Conquer.Presentation;

/// <summary>A message to the player, shown at <see cref="Time"/> (real seconds); red when it reports a failure.</summary>
public sealed record Message(string Text, double Time, bool Ok)
{
    /// <summary>1 while fresh, fading to 0 over its last second and a half.</summary>
    public float Opacity(double now) => (float)Math.Clamp((MessageLog.Lifetime - (now - Time)) / 1.5, 0, 1);
}

/// <summary>The messages on screen: results of the player's orders and the game's notifications, each for a few seconds.</summary>
public sealed class MessageLog
{
    /// <summary>Real seconds a message stays on screen.</summary>
    public const double Lifetime = 8;

    private readonly List<Message> _messages = [];

    public void Add(string text, double time, bool ok = true)
    {
        if (!string.IsNullOrEmpty(text)) _messages.Add(new Message(text, time, ok));
    }

    /// <summary>The messages still on screen at <paramref name="now"/>, newest first.</summary>
    public IReadOnlyList<Message> Current(double now)
    {
        _messages.RemoveAll(m => now - m.Time > Lifetime);
        return _messages.AsEnumerable().Reverse().ToList();
    }
}
