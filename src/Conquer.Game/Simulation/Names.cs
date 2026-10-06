namespace Conquer.Game.Simulation;

internal static class CityNames
{
    private static readonly string[] Starts = ["Al", "Bar", "Cor", "Dun", "El", "Fal", "Gar", "Hal", "Is", "Kar", "Lor", "Mar", "Nor", "Os", "Per", "Qui", "Ros", "Sal", "Tar", "Ur", "Val", "Zan"];
    private static readonly string[] Middles = ["", "", "a", "e", "i", "o", "an", "en", "ar", "or"];
    private static readonly string[] Ends = ["dor", "gar", "heim", "ia", "is", "mar", "polis", "ra", "tum", "via", "burg", "on", "ea", "ante"];

    /// <summary>A new city name, avoiding those already in <paramref name="used"/>, and adds it there.</summary>
    public static string Next(ISet<string> used, Random random)
    {
        string name = Suggest(used, random);
        used.Add(name);
        return name;
    }

    /// <summary>A name not in <paramref name="used"/> (after 50 tries, any name), without taking it.</summary>
    public static string Suggest(ISet<string> used, Random random)
    {
        for (int attempt = 0; ; attempt++)
        {
            string name = Starts[random.Next(Starts.Length)] + Middles[random.Next(Middles.Length)] + Ends[random.Next(Ends.Length)];
            if (!used.Contains(name) || attempt > 50) return name;
        }
    }
}

/// <summary>Names for every habitable province, from syllables of their own so they read differently from city names.</summary>
internal static class ProvinceNames
{
    private static readonly string[] Starts =
    [
        "Ab", "Ber", "Cal", "Del", "Es", "Fen", "Gual", "Her", "Ib", "Jar", "Lan", "Mor", "Nal", "Ol", "Pen", "Quer",
        "Ren", "Sor", "Tel", "Ul", "Ver", "Yal", "Zor", "Arn", "Bel", "Cas", "Dor", "Fal", "Gor", "Lis", "Mon", "Tor",
        "Al", "Bur", "Cer", "Dal", "Em", "Fir", "Gal", "Hel", "Mir", "Nor", "Par", "Rim", "Sil", "Tam", "Val", "Zar",
    ];
    private static readonly string[] Middles = ["", "a", "e", "i", "o", "u", "ar", "en", "il", "or", "an", "es", "ur", "al", "ov", "im"];
    private static readonly string[] Ends =
    [
        "ia", "ena", "ona", "ara", "ada", "illa", "enia", "ora", "ana", "este", "uria", "anda", "eda", "ina", "osa", "unia",
        "alia", "ero", "ano", "al", "ar", "on", "ez", "ante", "ado", "iles", "ueña", "ios", "iza", "ela", "era", "ota",
        "abia", "edo", "ica", "ueva", "orca", "ines", "ava", "igo", "uca", "oria", "osia", "anto", "ulia", "ejo", "ata", "ira",
    ];

    /// <summary>
    /// A new province name that reads well and is not in <paramref name="used"/> (which it joins). The
    /// syllables give some 37,000; should they ever run out, a Roman numeral tells the repeats apart.
    /// </summary>
    public static string Next(HashSet<string> used, Random random)
    {
        string name = "";
        for (int attempt = 0; attempt < 60; attempt++)
        {
            name = Starts[random.Next(Starts.Length)] + Middles[random.Next(Middles.Length)] + Ends[random.Next(Ends.Length)];
            if (ReadsWell(name) && used.Add(name)) return name;
        }
        for (int n = 2; ; n++)
            if (used.Add($"{name} {Military.Formations.Roman(n)}")) return $"{name} {Military.Formations.Roman(n)}";
    }

    /// <summary>No doubled vowel ("Penuulia") and no three vowels in a row ("Rimiueva").</summary>
    private static bool ReadsWell(string name)
    {
        static bool Vowel(char c) => "aeiouáéíóú".Contains(char.ToLowerInvariant(c));
        for (int i = 1; i < name.Length; i++)
        {
            if (Vowel(name[i]) && char.ToLowerInvariant(name[i]) == char.ToLowerInvariant(name[i - 1])) return false;
            if (i >= 2 && Vowel(name[i]) && Vowel(name[i - 1]) && Vowel(name[i - 2])) return false;
        }
        return true;
    }
}
