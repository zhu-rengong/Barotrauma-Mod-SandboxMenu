namespace SandboxMenu;

internal static class Services
{
    internal static readonly IDebugConsole DebugConsole = PluginServiceProvider.GetService<IDebugConsole>();

    internal static readonly ISimpleHookService HookService = PluginServiceProvider.GetService<ISimpleHookService>();

    internal static readonly IHarmonyProvider HarmonyProvider = PluginServiceProvider.GetService<IHarmonyProvider>();

    internal static readonly ISettingsService Settings = PluginServiceProvider.GetService<ISettingsService>();

    internal static readonly IGameScreen GameScreen = PluginServiceProvider.GetService<IGameScreen>();

    internal static readonly IGameNetwork GameNetwork = PluginServiceProvider.GetService<IGameNetwork>();
}
