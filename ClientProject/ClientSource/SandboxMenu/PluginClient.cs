namespace SandboxMenu;

public partial class Plugin
{
    private static readonly Identifier ResolutionChangedEvent = new("sandboxmenu.resolution");

    public partial void InitializeProjectSpecific()
    {
        // The client's end of the spawn channel: the request goes out from the menu, the answer comes back here.
        // The server's end lives in the other assembly, which is the only place it can be — the game loads
        // server-targeted plugins on a dedicated server and nowhere else. Nothing has to be registered for the
        // property values a spawn writes: those arrive as the host's own item event and the game applies them.
        NetworkService.RegisterNetworkHeaders<SandboxNetworkHeaders>();
        NetworkService.RegisterHandler<SandboxNetworkHeaders, SpawnResponse>(SandboxNetworkHeaders.SpawnResponse, ClientSpawnDispatcher.OnResponse);

        GameScreenService.RegisterResolutionChangeEvent(ResolutionChangedEvent, MenuNotices.ResolutionChanged);

        HookService.RegisterHook(PluginHooks.AddToGUIUpdateListHook);
        HookService.RegisterHook(PluginHooks.GameModeDrawHook);
        HookService.RegisterHook(PluginHooks.PostUpdateHook);
    }

    public partial void DisposeProjectSpecific()
    {
        // Nothing is handed back here on purpose: everything this plugin registers goes through a plugin service,
        // and the service releases it all as the game disposes the service while unloading the plugin — that
        // release is the deregistration. Doing it by hand would be an extra step, and one that can land after the
        // service has been disposed.

        // What is left to end is everything the menu itself kept.
        SandboxMenuWindow.Shutdown();
    }
}
