using Conquer.Client;
using Conquer.Game.Rules;
using Conquer.Game.World;

// Spanish number formatting (1.234,5) everywhere in the interface. Built from the invariant culture
// instead of "es-ES" so the game needs no ICU library (InvariantGlobalization in the csproj).
var spanish = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
spanish.NumberFormat.NumberDecimalSeparator = spanish.NumberFormat.PercentDecimalSeparator = ",";
spanish.NumberFormat.NumberGroupSeparator = spanish.NumberFormat.PercentGroupSeparator = ".";
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = spanish;
System.Globalization.CultureInfo.CurrentCulture = spanish;

if (!Conquer.Client.Graphics.GlSupport.Ensure())
{
    Console.Error.WriteLine("Conquer necesita una tarjeta gráfica con OpenGL 3.3 o superior.");
    return 1;
}

var options = StartOptions.Parse(args);
new ConquerApp(options).Run();
return 0;

namespace Conquer.Client
{
    public sealed record QuickStart(WorldSettings World, int Players);

    /// <summary>
    /// Command line, mainly for testing:
    /// <c>--new random|earth [--seed N] [--players N] [--difficulty veryeasy|easy|normal|hard|veryhard]</c> skips the menus and starts a game;
    /// <c>--days N</c> founds the player's capital and fast-forwards N days;
    /// <c>--zoom Z</c>, <c>--at longitude,latitude</c> and <c>--mode terrain|political|population|mood|fertility</c> set the view;
    /// <c>--nation summary|cities|provinces|science</c> opens the nation screen on that tab;
    /// <c>--panel buildings|army</c> shows that tab of the selected province, and <c>--panel regiment</c> a sample regiment (<c>--panel march</c> sends it a few provinces away, <c>--panel edit</c> opens its editor with officers in the reserve), <c>--panel found</c> the dialog to name the first city and <c>--panel battle</c> the window of the first battle under way;
    /// <c>--load file.conquer</c> carries on a saved game; <c>--menu new|load</c> opens that menu screen;
    /// <c>--screenshot file.png</c> saves the first frames to a PNG and exits.
    /// </summary>
    public sealed record StartOptions(QuickStart? QuickStart, int Days = 0, float? Zoom = null, string? Mode = null, string? Screenshot = null, string? Nation = null, string? Panel = null, string? Load = null, string? Menu = null, (float Longitude, float Latitude)? At = null)
    {
        public static StartOptions Parse(string[] args)
        {
            string? kind = null, mode = null, screenshot = null, nation = null, panel = null, load = null, menu = null;
            int seed = Environment.TickCount & 0xFFFF, players = 4, days = 0;
            var difficulty = Difficulty.Normal;
            float? zoom = null;
            (float, float)? at = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                string value = args[i + 1];
                switch (args[i])
                {
                    case "--new": kind = value; break;
                    case "--seed": seed = int.Parse(value); break;
                    case "--players": players = int.Parse(value); break;
                    case "--difficulty": difficulty = Enum.Parse<Difficulty>(value, ignoreCase: true); break;
                    case "--days": days = int.Parse(value); break;
                    case "--zoom": zoom = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--mode": mode = value; break;
                    case "--screenshot": screenshot = value; break;
                    case "--nation": nation = value; break;
                    case "--panel": panel = value; break;
                    case "--load": load = value; break;
                    case "--menu": menu = value; break;
                    case "--at":
                        var parts = value.Split(',');
                        at = (float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                        break;
                }
            }
            QuickStart? quick = null;
            if (kind != null)
            {
                var map = kind.Equals("earth", StringComparison.OrdinalIgnoreCase) ? MapKind.Earth : MapKind.Random;
                quick = new QuickStart(new WorldSettings(map, seed, Difficulty: difficulty, Generator: WorldGenerator.LatestGenerator), players);
            }
            return new StartOptions(quick, days, zoom, mode, screenshot, nation, panel, load, menu, at);
        }
    }
}
