namespace SandboxMenu;

public sealed partial class Plugin : IBarotraumaPlugin
{
    internal const string ModPrefix = "sandboxmenu";

    private static IDebugConsole? _debugConsole;
    private static ISettingsService? _settingsService;
    private static IGameNetwork? _network;
#if CLIENT
    private static IGameScreen? _gameScreen;
    private static ISimpleHookService? _hookService;
#endif

    static Plugin() => StaticState.Register(() =>
    {
        _debugConsole = null;
        _settingsService = null;
        _network = null;
#if CLIENT
        _gameScreen = null;
        _hookService = null;
        _toggleKeySetting?.Detach();
        _toggleKeySetting = null;
#endif
    });

    // GetService recognises the plugin through Assembly.GetCallingAssembly(): the call has to stay in this assembly and must not be inlined.
    internal static IDebugConsole DebugConsoleService
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get => _debugConsole ??= PluginServiceProvider.GetService<IDebugConsole>();
    }

    internal static ISettingsService SettingsService
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get => _settingsService ??= PluginServiceProvider.GetService<ISettingsService>();
    }

    internal static IGameNetwork NetworkService
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get => _network ??= PluginServiceProvider.GetService<IGameNetwork>();
    }

#if CLIENT
    private static IGameScreen GameScreenService
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get => _gameScreen ??= PluginServiceProvider.GetService<IGameScreen>();
    }

    private static ISimpleHookService HookService
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get => _hookService ??= PluginServiceProvider.GetService<ISimpleHookService>();
    }
#endif

    public void Init()
    {
#if CLIENT
        CreateSettings();
        RegisterCommands();
#endif
        ServerOptions.Register(SettingsService);

        InitializeProjectSpecific();
    }

    internal partial void InitializeProjectSpecific();

    public void OnContentLoaded()
    {
    }

    public void Dispose() => DisposeProjectSpecific();

    internal partial void DisposeProjectSpecific();
}
