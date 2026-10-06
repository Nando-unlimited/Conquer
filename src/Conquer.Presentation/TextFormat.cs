using Conquer.Game.Economy;
using Conquer.Game.Simulation;

namespace Conquer.Presentation;

/// <summary>Numbers and figures written the way the interface shows them.</summary>
public static class TextFormat
{
    /// <summary>Short form for tight spaces: 950, 12,3k, 2,9M; with <paramref name="decimals"/>, small values keep one (8,7).</summary>
    public static string Compact(double value, bool decimals = false)
    {
        double abs = Math.Abs(value);
        if (abs >= 1_000_000) return $"{value / 1_000_000:0.#}M";
        if (abs >= 10_000) return $"{value / 1000:0.#}k";
        return decimals && abs < 100 ? $"{value:0.#}" : $"{value:N0}";
    }

    public static string Plural(int n, string one, string many) => $"{n:N0} {(n == 1 ? one : many)}";

    /// <summary>"a", "a y b", "a, b y c".</summary>
    public static string List(IEnumerable<string> items)
    {
        var list = items.ToList();
        return list.Count <= 1 ? string.Concat(list) : string.Join(", ", list[..^1]) + " y " + list[^1];
    }

    /// <summary>Daily upkeep in words: "0,3 oro/día" or "1,2 oro, 0,4 hierro/día".</summary>
    public static string UpkeepText(IEnumerable<ResourceCost> costs)
    {
        var upkeep = new double[Resources.All.Length];
        foreach (var cost in costs) GameSession.AddUpkeep(upkeep, cost);
        var parts = Resources.All.Where(r => upkeep[(int)r] > 0).Select(r => $"{upkeep[(int)r]:0.##} {r.Name().ToLowerInvariant()}").ToList();
        return parts.Count == 0 ? "nada" : string.Join(", ", parts) + "/día";
    }

    /// <summary>"Tarda 16 días (20 sin tus avances militares)." or, with none that speed it up, "Tarda 20 días."</summary>
    public static string TrainingDaysText(int days, int baseDays) =>
        days < baseDays ? $"Tarda {days} días ({baseDays} sin tus avances militares)." : $"Tarda {days} días.";

    /// <summary>
    /// Key that sorts names in Spanish alphabetical order with plain ordinal comparison (the game runs
    /// without ICU, so culture-aware comparison is not available): case and accents are ignored and ñ goes after n.
    /// </summary>
    public static string SpanishSortKey(string name)
    {
        var key = new System.Text.StringBuilder(name.Length + 2);
        foreach (char ch in name)
        {
            char c = char.ToLowerInvariant(ch);
            switch (c)
            {
                case 'à' or 'á' or 'â' or 'ã' or 'ä' or 'å': key.Append('a'); break;
                case 'ç': key.Append('c'); break;
                case 'è' or 'é' or 'ê' or 'ë': key.Append('e'); break;
                case 'ì' or 'í' or 'î' or 'ï': key.Append('i'); break;
                case 'ò' or 'ó' or 'ô' or 'õ' or 'ö': key.Append('o'); break;
                case 'ù' or 'ú' or 'û' or 'ü': key.Append('u'); break;
                case 'ý' or 'ÿ': key.Append('y'); break;
                // '~' sorts after every letter, so "ñ" lands between "nz" and "o".
                case 'ñ': key.Append("n~"); break;
                default: key.Append(c); break;
            }
        }
        return key.ToString();
    }
}
