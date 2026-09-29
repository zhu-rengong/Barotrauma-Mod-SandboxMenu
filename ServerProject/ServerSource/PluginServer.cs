namespace SandboxMenu;

public sealed partial class Plugin
{
    internal partial void InitializeProjectSpecific()
    {
        NetworkService.RegisterNetworkHeaders<SandboxNetworkHeaders>();
        NetworkService.RegisterHandler<SandboxNetworkHeaders, SpawnRequest>(SandboxNetworkHeaders.SpawnRequest, ServerSpawnHandler.Handle);

        Log.Info("Sandbox spawning is served by this server.");
    }

    internal partial void DisposeProjectSpecific() => StaticState.ResetAll();
}
