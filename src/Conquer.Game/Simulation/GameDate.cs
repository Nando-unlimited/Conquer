namespace Conquer.Game.Simulation;

/// <summary>
/// An in-game moment counted in whole hours since the start of the game (1 January 4000 BC, 00:00).
/// The calendar uses 365-day years without leap days; years before 1 AD are shown as BC.
/// </summary>
public readonly record struct GameDate(long Hours)
{
    public const int StartYear = -4000;
    private static readonly int[] MonthDays = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    private static readonly string[] MonthNames = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    public long Days => Hours / 24;
    public int Hour => (int)(Hours % 24);

    /// <summary>Calendar year: negative for BC (-4000 = 4000 BC), with no year 0.</summary>
    public int Year
    {
        get
        {
            // StartYear + elapsed years, skipping the non-existent year 0.
            long y = StartYear + Days / 365;
            return (int)(y >= 0 ? y + 1 : y);
        }
    }

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

    public override string ToString()
    {
        var (month, day) = MonthAndDay;
        int year = Year;
        string era = year < 0 ? $"{-year} a.C." : $"{year} d.C.";
        return $"{day} {MonthNames[month - 1]} {era}, {Hour:00}:00";
    }
}
