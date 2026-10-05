namespace SandboxMenu.UI.Startup;

// The one place the mod names itself to the framework. Everything the framework cannot know from its own assembly
// arrives here once, and is handed back when the plugin goes away.
internal static class UiBootstrap
{
    private static bool _registered;

    internal static void Register()
    {
        if (_registered) { return; }

        _registered = true;

        UiHost.SetLogSink((message, exception) =>
        {
            if (exception is null) { Log.Warn(message); }
            else { Log.Warn(message, exception); }
        });

        UiHost.RegisterViewAssembly(typeof(UiBootstrap).Assembly);
        UiHost.RegisterElementAssembly(typeof(UiBootstrap).Assembly);
        UiHost.RegisterShortcutHints("sandboxmenu.shortcut.hint", "sandboxmenu.shortcut.hint.dark");
        UiHost.RegisterTokens(Tokens);

        // The styles a view falls back on live in markup with the views themselves.
        UiHost.RegisterGlobalResources(ViewLoader.LoadResources("Theme.xml"));
    }

    internal static void Shutdown()
    {
        if (!_registered) { return; }

        _registered = false;

        UiHost.Shutdown();
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
