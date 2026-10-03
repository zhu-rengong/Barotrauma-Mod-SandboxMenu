namespace SandboxMenu;

public sealed partial class Plugin
{
    internal partial void Setup()
    {
        NetworkService.RegisterNetworkHeaders<NetworkHeaders>();
        NetworkService.RegisterHandler<NetworkHeaders, SpawnRequest>(NetworkHeaders.SpawnRequest, ServerSpawnHandler.Handle);

        Log.Info("Sandbox spawning is served by this server.");
    }

    internal partial void Teardown() => ModLifetime.Unload();
}
