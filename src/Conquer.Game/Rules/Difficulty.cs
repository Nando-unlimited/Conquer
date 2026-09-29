namespace Conquer.Game.Rules;

public enum Difficulty
{
    VeryEasy,
    Easy,
    Normal,
    Hard,
    VeryHard,
}

/// <summary>What a difficulty level changes.</summary>
/// <param name="ExtraDepositChance">A resource that misses its first roll on the map gets a second one with this many times the chance (0: none).</param>
/// <param name="DepositSize">Multiplies how much each deposit holds.</param>
/// <param name="StartingResources">Multiplies the food, wood and gold the human starts with.</param>
/// <param name="ComputerOutput">Multiplies what computer rivals produce (food, wood, gold, deposits) and their science.</param>
public sealed record DifficultyInfo(string Name, string Description, double ExtraDepositChance, double DepositSize,
    double StartingResources, double ComputerOutput);

public static class Difficulties
{
    public static readonly Difficulty[] All = Enum.GetValues<Difficulty>();

    /// <summary>The difficulty levels. <b>Balance them here.</b></summary>
    private static readonly Dictionary<Difficulty, DifficultyInfo> Table = new()
    {
        [Difficulty.VeryEasy] = new("Muy fácil", "Yacimientos por todas partes y muy grandes, el doble de recursos al empezar y rivales lentos.", 3, 1.5, 2, 0.75),
        [Difficulty.Easy] = new("Fácil", "Muchos yacimientos y grandes, más recursos al empezar y rivales algo lentos.", 2, 1.25, 1.5, 0.9),
        [Difficulty.Normal] = new("Normal", "La partida tal como está pensada.", 1, 1, 1, 1),
        [Difficulty.Hard] = new("Difícil", "Menos yacimientos y más pequeños, menos recursos al empezar y rivales que producen un 20 % más.", 0.5, 0.75, 0.75, 1.2),
        [Difficulty.VeryHard] = new("Muy difícil", "Yacimientos escasos y pequeños, la mitad de recursos al empezar y rivales que producen un 50 % más.", 0, 0.5, 0.5, 1.5),
    };

    public static DifficultyInfo Info(this Difficulty difficulty) => Table[difficulty];
}
