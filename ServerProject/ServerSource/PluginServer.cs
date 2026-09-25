using Barotrauma.Networking;

namespace SandboxMenu;

// The server half of the mod. It carries no UI, no key bind and no menu: what it has is the one network handler
// that turns a client's request into items, and it is only ever loaded by a dedicated server (the game's own host
// mode runs one of those, which is why the client half can stay a pure client).
public partial class Plugin
{
    public partial void InitializeProjectSpecific()
    {
        NetworkService.RegisterNetworkHeaders<SandboxNetworkHeaders>();
        NetworkService.RegisterHandler<SandboxNetworkHeaders, SpawnRequest>(SandboxNetworkHeaders.SpawnRequest, ServerSpawnHandler.Handle);

        // No entity-event registration of the mod's own: the component values a spawn writes travel as the host's
        // property event, which the client's vanilla item code reads (see SpawnPropertySync).

        Log.Info("Sandbox spawning is served by this server.");
    }

    public partial void DisposeProjectSpecific()
    {
        // The game disposes the services it handed over — the network registrations go with them — so what is left
        // to drop is what the mod itself kept in statics.
        StaticState.ResetAll();
    }
}
