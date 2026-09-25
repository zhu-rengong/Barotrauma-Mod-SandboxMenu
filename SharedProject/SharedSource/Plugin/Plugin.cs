namespace SandboxMenu;

public partial class Plugin : IBarotraumaPlugin
{
    public const string ModPrefix = "sandboxmenu";

    private static IDebugConsole? _debugConsole;
    private static ISettingsService? _settingsService;
    private static IGameNetwork? _network;
#if CLIENT
    private static IGameScreen? _gameScreen;
    private static ISimpleHookService? _hookService;
#endif

    // A service of the game's is reached through a delegate of this assembly; dropping them together with the
    // rest of the mod's state is what leaves nothing pointing either way once it is unloaded. The setting row
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

    // GetService recognises the plugin through Assembly.GetCallingAssembly(), so the call sits directly in a
    // member of this assembly: handing it to a factory (a Lazy lambda, for instance) would lose that frame.
    public static IDebugConsole DebugConsoleService => _debugConsole ??= PluginServiceProvider.GetService<IDebugConsole>();

    public static ISettingsService SettingsService => _settingsService ??= PluginServiceProvider.GetService<ISettingsService>();

    // The plugin's network channel. Both halves register the same header enum through it, which is what puts the
    // client and the server on one opcode range.
    internal static IGameNetwork NetworkService => _network ??= PluginServiceProvider.GetService<IGameNetwork>();

#if CLIENT
    private static IGameScreen GameScreenService => _gameScreen ??= PluginServiceProvider.GetService<IGameScreen>();

    private static ISimpleHookService HookService => _hookService ??= PluginServiceProvider.GetService<ISimpleHookService>();
#endif

    [MethodImpl(MethodImplOptions.NoOptimization)]
    public void Init()
    {
#if CLIENT
        CreateSettings();
        RegisterCommands();
#endif
        ServerOptions.Register(SettingsService);

        InitializeProjectSpecific();
    }

    public partial void InitializeProjectSpecific();

    [MethodImpl(MethodImplOptions.NoOptimization)]
    public void OnContentLoaded()
    {
    }

    [MethodImpl(MethodImplOptions.NoOptimization)]
    public void Dispose()
    {
        DisposeProjectSpecific();

#if CLIENT
        // The settings row sits in the game's settings menu, which has no idea that the plugin is going away:
        // its controls hold delegates back into this assembly and would keep it from being unloaded.
        _toggleKeySetting?.Detach();
        _toggleKeySetting = null;
#endif
    }

    public partial void DisposeProjectSpecific();
}
