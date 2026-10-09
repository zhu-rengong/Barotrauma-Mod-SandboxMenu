namespace SandboxMenu;

public sealed partial class Plugin
{
    partial void InitProjSpecific()
    {
        NetworkService.RegisterNetworkHeaders<NetworkHeaders>();
        NetworkService.RegisterHandler<NetworkHeaders, SpawnRequest>(NetworkHeaders.SpawnRequest, ServerSpawnHandler.Handle);

        DebugConsole.NewMessage("Sandbox spawning is served by this server.");
    }
}
