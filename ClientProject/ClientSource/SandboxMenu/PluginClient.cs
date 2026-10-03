namespace SandboxMenu;

public sealed partial class Plugin
{
    private static readonly Identifier _resolutionChangedEvent = new("sandboxmenu.resolution");

    private static IGameScreen? _gameScreen;
    private static ISimpleHookService? _hookService;

    private static IGameScreen GameScreenService => _gameScreen ??= PluginServiceProvider.GetService<IGameScreen>();

    private static ISimpleHookService HookService => _hookService ??= PluginServiceProvider.GetService<ISimpleHookService>();

    internal partial void Setup()
    {
        NetworkService.RegisterNetworkHeaders<NetworkHeaders>();
        NetworkService.RegisterHandler<NetworkHeaders, SpawnResponse>(NetworkHeaders.SpawnResponse, ClientSpawnDispatcher.OnResponse);

        GameScreenService.RegisterResolutionChangeEvent(_resolutionChangedEvent, ScreenReload.Signal);

        HookService.RegisterHook(PluginHooks.AddToGUIUpdateListHook);
        HookService.RegisterHook(PluginHooks.GameModeDrawHook);
        HookService.RegisterHook(PluginHooks.PostUpdateHook);
        HookService.RegisterHook(PluginHooks.EscapeKeyHook);
    }

    internal partial void Teardown()
    {
        MenuHost.Shutdown();

        _gameScreen = null;
        _hookService = null;
        _toggleKeySetting?.Detach();
        _toggleKeySetting = null;
        _giveKeySetting?.Detach();
        _giveKeySetting = null;
    }
}
