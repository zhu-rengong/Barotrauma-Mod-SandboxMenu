namespace SandboxMenu;

public sealed partial class Plugin
{
    private static readonly Identifier _resolutionChangedEvent = new("sandboxmenu.resolution");

    private static IGameScreen? _gameScreen;
    private static ISimpleHookService? _hookService;

    private static IGameScreen GameScreenService => _gameScreen ??= PluginServiceProvider.GetService<IGameScreen>();

    private static ISimpleHookService HookService => _hookService ??= PluginServiceProvider.GetService<ISimpleHookService>();

    partial void InitProjSpecific()
    {
        UiHost.RegisterViewAssembly(typeof(Plugin).Assembly);
        UiHost.RegisterElementAssembly(typeof(Plugin).Assembly);
        UiHost.RegisterShortcutHints("sandboxmenu.shortcut.hint", "sandboxmenu.shortcut.hint.dark");
        UiHost.RegisterTokens(Tokens);
        UiHost.RegisterGlobalResources(ViewLoader.LoadResources("Theme.xml"));

        NetworkService.RegisterNetworkHeaders<NetworkHeaders>();
        NetworkService.RegisterHandler<NetworkHeaders, SpawnResponse>(NetworkHeaders.SpawnResponse, ClientSpawnDispatcher.OnResponse);

        GameScreenService.RegisterResolutionChangeEvent(_resolutionChangedEvent, MenuWindow.ResolutionChanged.Signal);

        HookService.RegisterHook(PluginHooks.AddToGUIUpdateListHook);
        HookService.RegisterHook(PluginHooks.GameModeDrawHook);
        HookService.RegisterHook(PluginHooks.PostUpdateHook);
        HookService.RegisterHook(PluginHooks.EscapeKeyHook);
    }

    partial void DisposeProjSpecific()
    {
        MenuWindow.Shutdown();
    }

    private static readonly Dictionary<string, UiToken> Tokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["row"] = UiToken.Percent(Theme.RowHeight),
        ["section"] = UiToken.Percent(Theme.SectionHeight),
        ["control"] = UiToken.Percent(Theme.ControlHeight),
        ["labelWidth"] = UiToken.Percent(Theme.LabelWidth),
        ["pad"] = UiToken.Dip(Theme.Pad),
        ["gap"] = UiToken.Dip(Theme.Gap),
        ["indent"] = UiToken.Dip(Theme.TreeIndentStep),
        ["tile"] = UiToken.Dip(Theme.TileSize)
    };
}
