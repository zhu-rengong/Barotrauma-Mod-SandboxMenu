namespace SandboxMenu;

public sealed partial class Plugin : IBarotraumaPlugin
{
    internal const string ModPrefix = "sandboxmenu";

    private static IDebugConsole? _debugConsole;
    private static ISettingsService? _settingsService;
    private static IGameNetwork? _network;

    internal static IDebugConsole DebugConsoleService => _debugConsole ?? throw NotReady();

    internal static ISettingsService SettingsService => _settingsService ?? throw NotReady();

    internal static IGameNetwork NetworkService => _network ?? throw NotReady();

    public void Init()
    {
        _debugConsole = PluginServiceProvider.GetService<IDebugConsole>();
        _settingsService = PluginServiceProvider.GetService<ISettingsService>();
        _network = PluginServiceProvider.GetService<IGameNetwork>();

#if CLIENT
        CreateSettings();
        RegisterCommands();
#endif
        ServerOptions.Register(SettingsService);

        InitProjSpecific();
    }

    public void OnContentLoaded()
    {
    }

    public void Dispose()
    {
        DisposeProjSpecific();

        _debugConsole = null;
        _settingsService = null;
        _network = null;
    }

    partial void InitProjSpecific();

    partial void DisposeProjSpecific();

    private static InvalidOperationException NotReady() => new($"{ModPrefix} is not initialised");
}
