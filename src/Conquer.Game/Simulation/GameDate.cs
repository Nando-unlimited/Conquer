namespace Conquer.Game.Simulation;

/// <summary>
/// An in-game moment counted in whole hours since the start of the game (1 January of year 1, 00:00). Years count
/// the life of the player's civilization, not real history: the ages are stages of development, not dates. The
/// calendar uses 365-day years without leap days.
/// </summary>
public readonly record struct GameDate(long Hours)
{
    private static readonly int[] MonthDays = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    private static readonly string[] MonthNames = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    public long Days => Hours / 24;
    public int Hour => (int)(Hours % 24);

    /// <summary>Year of the game, from 1.</summary>
    public int Year => (int)(Days / 365) + 1;

    public int DayOfYear => (int)(Days % 365);

    public (int Month, int Day) MonthAndDay
    {
        get
        {
            int d = DayOfYear;
            for (int m = 0; m < 12; m++)
            {
                if (d < MonthDays[m]) return (m + 1, d + 1);
                d -= MonthDays[m];
            }
            throw new InvalidOperationException();
        }
    }

    public GameDate AddHours(double hours) => new(Hours + (long)Math.Ceiling(hours));

    /// <summary>«12 mar, año 37, 08:00».</summary>
    public override string ToString()
    {
        var (month, day) = MonthAndDay;
        return $"{day} {MonthNames[month - 1]}, año {Year}, {Hour:00}:00";
    }
}
