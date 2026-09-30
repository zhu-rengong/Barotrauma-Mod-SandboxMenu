namespace SandboxMenu;

public sealed partial class Plugin
{
    private static readonly Identifier _resolutionChangedEvent = new("sandboxmenu.resolution");

    internal partial void InitializeProjectSpecific()
    {
        NetworkService.RegisterNetworkHeaders<SandboxNetworkHeaders>();
        NetworkService.RegisterHandler<SandboxNetworkHeaders, SpawnResponse>(SandboxNetworkHeaders.SpawnResponse, ClientSpawnDispatcher.OnResponse);

        GameScreenService.RegisterResolutionChangeEvent(_resolutionChangedEvent, MenuNotices.ResolutionChanged);

        HookService.RegisterHook(PluginHooks.AddToGUIUpdateListHook);
        HookService.RegisterHook(PluginHooks.GameModeDrawHook);
        HookService.RegisterHook(PluginHooks.PostUpdateHook);
        HookService.RegisterHook(PluginHooks.EscapeKeyHook);
    }

    internal partial void DisposeProjectSpecific() => SandboxMenuWindow.Shutdown();
}
