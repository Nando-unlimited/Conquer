using Conquer.Client;
using Conquer.Game.World;

// Spanish number formatting (1.234,5) everywhere in the interface. Built from the invariant culture
// instead of "es-ES" so the game needs no ICU library (InvariantGlobalization in the csproj).
var spanish = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
spanish.NumberFormat.NumberDecimalSeparator = spanish.NumberFormat.PercentDecimalSeparator = ",";
spanish.NumberFormat.NumberGroupSeparator = spanish.NumberFormat.PercentGroupSeparator = ".";
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = spanish;
System.Globalization.CultureInfo.CurrentCulture = spanish;

var options = StartOptions.Parse(args);
new ConquerApp(options).Run();

namespace Conquer.Client
{
    public sealed record QuickStart(WorldSettings World, int Players);

    /// <summary>
    /// Command line, mainly for testing:
    /// <c>--new random|earth [--seed N] [--players N]</c> skips the menus and starts a game;
    /// <c>--days N</c> founds the player's capital and fast-forwards N days;
    /// <c>--zoom Z</c> and <c>--mode terrain|political|population|mood|fertility</c> set the view;
    /// <c>--nation summary|cities|provinces|science</c> opens the nation screen on that tab;
    /// <c>--panel buildings|army</c> shows that tab of the selected province, and <c>--panel regiment</c> a sample regiment;
    /// <c>--screenshot file.png</c> saves the first frames to a PNG and exits.
    /// </summary>
    public sealed record StartOptions(QuickStart? QuickStart, int Days = 0, float? Zoom = null, string? Mode = null, string? Screenshot = null, string? Nation = null, string? Panel = null)
    {
        public static StartOptions Parse(string[] args)
        {
            string? kind = null, mode = null, screenshot = null, nation = null, panel = null;
            int seed = Environment.TickCount & 0xFFFF, players = 4, days = 0;
            float? zoom = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                string value = args[i + 1];
                switch (args[i])
                {
                    case "--new": kind = value; break;
                    case "--seed": seed = int.Parse(value); break;
                    case "--players": players = int.Parse(value); break;
                    case "--days": days = int.Parse(value); break;
                    case "--zoom": zoom = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--mode": mode = value; break;
                    case "--screenshot": screenshot = value; break;
                    case "--nation": nation = value; break;
                    case "--panel": panel = value; break;
                }
            }
            QuickStart? quick = null;
            if (kind != null)
            {
                var map = kind.Equals("earth", StringComparison.OrdinalIgnoreCase) ? MapKind.Earth : MapKind.Random;
                quick = new QuickStart(new WorldSettings(map, seed), players);
            }
            return new StartOptions(quick, days, zoom, mode, screenshot, nation, panel);
        }
    }
}
