namespace SandboxMenu;

public sealed partial class Plugin : IBarotraumaPlugin
{
    internal const string ModPrefix = "sandboxmenu";

    private static IDebugConsole? _debugConsole;
    private static ISettingsService? _settingsService;
    private static IGameNetwork? _network;

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

        InitProjSpecific();
    }

    public void OnContentLoaded()
    {
    }

    public void Dispose()
    {
        DisposeProjSpecific();
    }

    partial void InitProjSpecific();

    partial void DisposeProjSpecific();
}
