#if CLIENT
namespace SandboxMenu;

public partial class Plugin
{
    // Nothing to hand back: the console keeps its own list of what this plugin registered and drops the lot when
    // the game unloads the plugin (see the note in DisposeProjectSpecific).
    private void RegisterCommands()
    {
        DebugConsoleService.RegisterCommand(
            command: "sandboxmenu",
            helpMessage: "Toggles the sandbox spawn menu.",
            flags: CommandFlags.DoNotRelayToServer,
            onCommandExecuted: (string[] args) => SandboxMenuWindow.Instance.Toggle());

        DebugConsoleService.RegisterCommand(
            command: "sandboxmenu.markup",
            helpMessage: "Loads a sample view through the markup engine and reports what it built.",
            flags: CommandFlags.DoNotRelayToServer,
            onCommandExecuted: (string[] args) => MarkupSelfTest.Run());
    }
}
#endif
