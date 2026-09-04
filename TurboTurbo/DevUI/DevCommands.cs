using CommandTerminal;

namespace TurboTurbo.DevUI;

/// <summary>
/// Registers the mod's console commands.
/// </summary>
internal static class DevCommands
{
    private static readonly Logger Log = TurboTurbo.Log.ForContext("commands");
    private static bool _registered;

    internal static void TryRegister(DevPanelPresenter presenter)
    {
        if (_registered || Terminal.Shell == null) return;
        _registered = true;

        var command = Terminal.Shell.AddCommand("turbodev",
            _ => Terminal.Log($"dev panel {(presenter.Toggle() ? "opened" : "closed")}"),
            0, 0, "Toggle the TurboTurbo dev panel");
        Terminal.Autocomplete.Register(command);
        Log.Info("console command registered: turbodev");
    }
}