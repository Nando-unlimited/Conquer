using Conquer.Presentation;

namespace Conquer.Client.Graphics;

/// <summary>
/// Small looping motions for the icons on the map: units hop as they march, bob when selected and shake in battle,
/// ships ride the swell, smoke rises from towns. Each thing moves out of step with the others (its phase comes from
/// its id). Display only; off with "Animaciones" in the pause menu (<see cref="DisplaySettings"/>).
/// </summary>
public static class Motion
{
    public static bool Enabled => DisplaySettings.Current.Animations;

    /// <summary>Seconds, set once a frame and kept small so float maths stays smooth.</summary>
    public static float Time { get; set; }

    /// <summary>The golden angle, so neighbouring ids get well spread phases.</summary>
    private static float Phase(int id) => id * 2.39996f;

    /// <summary>A sine of <paramref name="amplitude"/> at <paramref name="speed"/> radians a second, or 0 when off.</summary>
    public static float Wave(float speed, int id, float amplitude) => Enabled ? MathF.Sin(Time * speed + Phase(id)) * amplitude : 0f;

    /// <summary>A bounce up to <paramref name="height"/> and back down, twice per sine period, or 0 when off.</summary>
    public static float Hop(float speed, int id, float height) => Enabled ? MathF.Abs(MathF.Sin(Time * speed + Phase(id))) * height : 0f;

    /// <summary>Goes from 0 to 1 and starts again, <paramref name="perSecond"/> times a second (0 when off).</summary>
    public static float Cycle(float perSecond, int id, float offset = 0f)
    {
        if (!Enabled) return 0f;
        float t = Time * perSecond + offset + Phase(id) / MathF.Tau;
        return t - MathF.Floor(t);
    }
}
