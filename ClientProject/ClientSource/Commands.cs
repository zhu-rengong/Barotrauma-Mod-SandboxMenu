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
            onCommandExecuted: static (string[] _) => SandboxMenuHost.Instance.Toggle());

        console.RegisterCommand(
            command: "sandboxmenu_screenshot",
            helpMessage: "Saves a PNG of the sandbox spawn menu, the dialogs over it included.",
            flags: CommandFlags.DoNotRelayToServer,
            onCommandExecuted: static (string[] _) => SandboxMenuHost.Instance.CaptureScreenshot());
    }
}
