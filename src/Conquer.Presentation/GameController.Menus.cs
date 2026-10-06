namespace Conquer.Presentation;

/// <summary>The pause menu, the help and the changelog over the game, and the Esc key.</summary>
public sealed partial class GameController
{
    public bool MenuOpen { get; set; }
    public bool HelpOpen { get; set; }
    public bool ChangelogOpen { get; set; }
    public SettingsMenu Settings { get; } = new();

    /// <summary>Whether a window that stops time is open: the menu, the options, the help, the changelog or a dialog (battles do not stop it).</summary>
    public bool TimeStopped => MenuOpen || ChangelogOpen || HelpOpen || Naming.HasValue || EditingUnitId.HasValue || RoadWindowOpen || DecisionOpen || GameOverOpen || Settings.Open;

    /// <summary>Esc closes the topmost window, then cancels the migration target, then the selection, and otherwise opens or closes the menu.</summary>
    public void Escape()
    {
        if (Settings.Open) Settings.Open = false;
        else if (HelpOpen) HelpOpen = false;
        else if (BattleWindowOpen) CloseBattle();
        else if (RoadWindowOpen) CloseRoadWindow();
        else if (ChangelogOpen) ChangelogOpen = false;
        else if (Nation.Visible) Nation.Visible = false;
        else if (TargetPrompt != null) CancelTarget();
        else if (ChoosingMigrationTarget) ChoosingMigrationTarget = false;
        else if (HasSelection) ClearSelection();
        else MenuOpen = !MenuOpen;
    }

    /// <summary>The pause menu's buttons: carry on, save, help, changelog, options, back to the title screen or quit.</summary>
    public IReadOnlyList<Button> PauseMenu(IMenuNavigator navigator, string version) =>
    [
        new("Continuar", () => MenuOpen = false),
        new("Guardar partida", () => { if (Save(version)) MenuOpen = false; }, Tooltip: $"Se guarda en {SaveFiles.Folder}"),
        new("Ayuda", () => { HelpOpen = true; MenuOpen = false; }),
        new("Historial de versiones", () => { ChangelogOpen = true; MenuOpen = false; }),
        new("Opciones", () => { Settings.Open = true; MenuOpen = false; }, Tooltip: "Música, sonido, animaciones y aspecto del mapa."),
        new("Menú principal", navigator.ShowMainMenu),
        new("Salir del juego", navigator.Quit),
    ];
}
