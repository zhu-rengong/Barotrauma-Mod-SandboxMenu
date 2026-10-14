using SandboxMenu.Domain.Afflictions;

namespace SandboxMenu;

[RunConstructorOnInit]
internal static class Network
{
    static Network()
    {
        Services.GameNetwork.RegisterNetworkHeaders<NetworkHeaders>();

#if CLIENT
        Services.GameNetwork.RegisterHandler<NetworkHeaders, SpawnResponse>(
            NetworkHeaders.SpawnResponse, ClientSpawnDispatcher.OnResponse);

        Services.GameNetwork.RegisterHandler<NetworkHeaders, AfflictionResponse>(
            NetworkHeaders.AfflictionResponse, ClientAfflictionDispatcher.OnResponse);
#elif SERVER
        Services.GameNetwork.RegisterHandler<NetworkHeaders, SpawnRequest>(
            NetworkHeaders.SpawnRequest, ServerSpawnHandler.Handle);

        Services.GameNetwork.RegisterHandler<NetworkHeaders, AfflictionRequest>(
            NetworkHeaders.AfflictionRequest, ServerAfflictionHandler.Handle);
#endif
    }
}
