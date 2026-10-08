using Conquer.Game.World;
using Conquer.Presentation;

namespace Conquer.Tests;

/// <summary>A menu navigator that goes nowhere, for building menus in tests.</summary>
internal sealed class NoNavigator : IMenuNavigator
{
    public void ShowMainMenu() { }
    public void ShowNewGame() { }
    public void ShowLoadGame() { }
    public void StartNewGame(WorldSettings settings, int players, string? country) { }
    public void LoadSavedGame(SaveFile save) { }
    public void Quit() { }
}
