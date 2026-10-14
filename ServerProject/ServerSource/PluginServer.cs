namespace SandboxMenu;

public sealed partial class Plugin
{
    partial void InitProjSpecific()
        => Services.DebugConsole.NewMessage("Sandbox spawning is served by this server.");
}
