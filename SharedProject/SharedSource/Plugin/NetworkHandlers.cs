using SandboxMenu.Domain.Afflictions;
#if CLIENT
using SandboxMenu.Domain.Talents;
#endif

namespace SandboxMenu;

internal static class NetworkHandlers
{
    internal static void Register()
    {
        Services.GameNetwork.RegisterNetworkHeaders<NetworkHeaders>();

#if CLIENT
        Services.GameNetwork.RegisterHandler<NetworkHeaders, SpawnResponse>(
            NetworkHeaders.SpawnResponse, ClientSpawnDispatcher.OnResponse);

        Services.GameNetwork.RegisterHandler<NetworkHeaders, AfflictionResponse>(
            NetworkHeaders.AfflictionResponse, ClientAfflictionDispatcher.OnResponse);

        Services.GameNetwork.RegisterHandler<NetworkHeaders, TalentResponse>(
            NetworkHeaders.TalentResponse, ClientTalentDispatcher.OnResponse);
#elif SERVER
        Services.GameNetwork.RegisterHandler<NetworkHeaders, SpawnRequest>(
            NetworkHeaders.SpawnRequest, ServerSpawnHandler.Handle);

        Services.GameNetwork.RegisterHandler<NetworkHeaders, AfflictionRequest>(
            NetworkHeaders.AfflictionRequest, ServerAfflictionHandler.Handle);

        Services.GameNetwork.RegisterHandler<NetworkHeaders, TalentRequest>(
            NetworkHeaders.TalentRequest, ServerTalentHandler.Handle);
#endif
    }
}
