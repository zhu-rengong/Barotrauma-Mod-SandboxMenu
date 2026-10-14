namespace SandboxMenu;

internal static class Commands
{
    internal static void Register()
    {
        IDebugConsole console = Services.DebugConsole;

        console.RegisterCommand(
            command: "sandboxmenu",
            helpMessage: "Toggles the sandbox spawn menu.",
            flags: CommandFlags.DoNotRelayToServer,
            onCommandExecuted: static (string[] _) => MenuWindow.Instance.Toggle());

        console.RegisterCommand(
            command: "sandboxmenu.markup.selftest",
            helpMessage: "Loads every sandbox menu view once and reports markup diagnostics.",
            flags: CommandFlags.DoNotRelayToServer,
            onCommandExecuted: static (string[] _) => MarkupSelfTest.Run());

        console.RegisterCommand(
            command: "sandboxmenu_screenshot",
            helpMessage: "Saves a PNG of the sandbox spawn menu, the dialogs over it included.",
            flags: CommandFlags.DoNotRelayToServer,
            onCommandExecuted: static (string[] _) =>
            {
                if (MenuWindow.Current is { } menu) { menu.CaptureScreenshot(); }
                else { DebugConsole.NewMessage("There is no menu window to capture."); }
            });
    }
}
