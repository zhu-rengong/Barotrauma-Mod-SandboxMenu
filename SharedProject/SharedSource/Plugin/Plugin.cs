namespace SandboxMenu;

public sealed partial class Plugin : IBarotraumaPlugin
{
    internal const string ModPrefix = "sandboxmenu";

    private static IDebugConsole? _debugConsole;
    private static ISettingsService? _settingsService;
    private static IGameNetwork? _network;

    static Plugin() => ModLifetime.Unloading += () =>
    {
        _debugConsole = null;
        _settingsService = null;
        _network = null;
    };

    // GetService recognises the plugin through Assembly.GetCallingAssembly(): keep the call in this assembly.
    internal static IDebugConsole DebugConsoleService => _debugConsole ??= PluginServiceProvider.GetService<IDebugConsole>();

    internal static ISettingsService SettingsService => _settingsService ??= PluginServiceProvider.GetService<ISettingsService>();

    internal static IGameNetwork NetworkService => _network ??= PluginServiceProvider.GetService<IGameNetwork>();

    public void Init()
    {
#if CLIENT
        CreateSettings();
        RegisterCommands();
#endif
        ServerOptions.Register(SettingsService);

        Setup();
    }

    public void OnContentLoaded()
    {
    }

    public void Dispose() => Teardown();

    // What the client and the server each add on top of the plugin; both are carried by the project that needs them.
    internal partial void Setup();

    internal partial void Teardown();
}
