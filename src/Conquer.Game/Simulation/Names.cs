namespace Conquer.Game.Simulation;

internal static class PlayerNames
{
    private static readonly string[] Names =
    [
        "Ardania", "Borvia", "Caldoria", "Drakmar", "Estovia", "Farnia", "Galmoria", "Hesperia",
        "Ilyria", "Kartesia", "Lumeria", "Morvania",
    ];

    /// <summary>Distinct, saturated colours that read well over every biome.</summary>
    public static readonly uint[] Colors =
    [
        0xFFD03A3A, 0xFF2F6FDB, 0xFFE8C21E, 0xFF8E3FD1, 0xFFEE7A1E, 0xFF1FB5A8, 0xFFE0569B, 0xFF7A5230,
        0xFF6CC83B, 0xFF3AC7E8, 0xFFB0B0B0, 0xFF2E2E8F,
    ];

    public const int MaxPlayers = 12;

    public static List<string> Pick(int count, Random random) =>
        Names.OrderBy(_ => random.Next()).Take(count).ToList();
}

internal static class CityNames
{
    private static readonly string[] Starts = ["Al", "Bar", "Cor", "Dun", "El", "Fal", "Gar", "Hal", "Is", "Kar", "Lor", "Mar", "Nor", "Os", "Per", "Qui", "Ros", "Sal", "Tar", "Ur", "Val", "Zan"];
    private static readonly string[] Middles = ["", "", "a", "e", "i", "o", "an", "en", "ar", "or"];
    private static readonly string[] Ends = ["dor", "gar", "heim", "ia", "is", "mar", "polis", "ra", "tum", "via", "burg", "on", "ea", "ante"];

    /// <summary>A new city name, avoiding those already in <paramref name="used"/>.</summary>
    public static string Next(ISet<string> used, Random random)
    {
        for (int attempt = 0; ; attempt++)
        {
            string name = Starts[random.Next(Starts.Length)] + Middles[random.Next(Middles.Length)] + Ends[random.Next(Ends.Length)];
            if (used.Add(name) || attempt > 50) return name;
        }
    }
}
