namespace SandboxMenu;

public sealed partial class Plugin
{
    private void RegisterCommands()
    {
        DebugConsoleService.RegisterCommand(
            command: "sandboxmenu",
            helpMessage: "Toggles the sandbox spawn menu.",
            flags: CommandFlags.DoNotRelayToServer,
            onCommandExecuted: (string[] _) => MenuHost.Instance.Toggle());

        DebugConsoleService.RegisterCommand(
            command: "sandboxmenu_screenshot",
            helpMessage: "Saves a PNG of the sandbox spawn menu, the dialogs over it included.",
            flags: CommandFlags.DoNotRelayToServer,
            onCommandExecuted: (string[] _) =>
            {
                if (MenuHost.Current is { } menu) { menu.CaptureScreenshot(); }
                else { Log.Info("There is no menu window to capture."); }
            });
    }
}
