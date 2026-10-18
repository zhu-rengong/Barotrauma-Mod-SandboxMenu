namespace SandboxMenu;

public sealed partial class Plugin
{
    private static readonly Identifier _resolutionChangedEvent = new("sandboxmenu.resolution");

    private static readonly Dictionary<string, UiToken> Tokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["row"] = UiToken.Percent(Theme.RowHeight),
        ["menuRow"] = UiToken.Dip(Theme.MenuRowHeight),
        ["section"] = UiToken.Percent(Theme.SectionHeight),
        ["control"] = UiToken.Percent(Theme.ControlHeight),
        ["labelWidth"] = UiToken.Percent(Theme.LabelWidth),
        ["pad"] = UiToken.Dip(Theme.Pad),
        ["gap"] = UiToken.Dip(Theme.Gap),
        ["indent"] = UiToken.Dip(Theme.TreeIndentStep),
        ["tile"] = UiToken.Dip(Theme.TileSize)
    };

    partial void InitProjSpecific()
    {
        KeyBinds.Register();
        Commands.Register();

        Services.HarmonyProvider.PatchAll();

        UiHost.RegisterViewAssembly(typeof(Plugin).Assembly);
        UiHost.RegisterElementAssembly(typeof(Plugin).Assembly);
        UiHost.RegisterShortcutHints("sandboxmenu.shortcut.hint", "sandboxmenu.shortcut.hint.dark");
        UiHost.RegisterTokens(Tokens);
        UiHost.RegisterGlobalResources(ViewLoader.LoadResources("Theme.xml"));

        SandboxMenuHost.Create();

        Services.GameScreen.RegisterResolutionChangeEvent(_resolutionChangedEvent, SandboxMenuHost.ResolutionChanged);
    }

    partial void DisposeProjSpecific() => SandboxMenuHost.Shutdown();
}
