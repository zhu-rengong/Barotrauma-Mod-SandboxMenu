#if CLIENT
namespace SandboxMenu;

public sealed partial class Plugin
{
    private void RegisterCommands()
    {
        DebugConsoleService.RegisterCommand(
            command: "sandboxmenu",
            helpMessage: "Toggles the sandbox spawn menu.",
            flags: CommandFlags.DoNotRelayToServer,
            onCommandExecuted: (string[] _) => SandboxMenuWindow.Instance.Toggle());
    }
}
#endif
