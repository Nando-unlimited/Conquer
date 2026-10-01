namespace Conquer.Presentation;

/// <summary>How fast game time runs: paused or one of five speeds, turned into whole game hours to simulate each frame.</summary>
public sealed class GameClock
{
    /// <summary>Game hours per real second at each speed; index 0 is paused.</summary>
    public static readonly double[] HoursPerSecond = [0, 1, 4, 12, 48, 168];

    private int _lastSpeed = 1;
    private double _hourAccumulator;

    public int Speed { get; private set; } = 1;

    public void SetSpeed(int speed)
    {
        if (speed > 0) _lastSpeed = speed;
        Speed = speed;
    }

    /// <summary>Pauses, or resumes at the speed it had before.</summary>
    public void TogglePause() => SetSpeed(Speed == 0 ? _lastSpeed : 0);

    /// <summary>How many game hours to step after <paramref name="dt"/> real seconds; none while paused.</summary>
    public int Advance(double dt)
    {
        if (Speed == 0) return 0;
        _hourAccumulator += dt * HoursPerSecond[Speed];
        int steps = Math.Min((int)_hourAccumulator, 400);
        _hourAccumulator -= steps;
        if (_hourAccumulator > 4) _hourAccumulator = 0; // don't build up a backlog when the machine can't keep up
        return steps;
    }
}
