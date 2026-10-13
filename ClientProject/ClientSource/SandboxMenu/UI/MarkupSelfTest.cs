namespace SandboxMenu.UI;

internal static class MarkupSelfTest
{
    private const int WarmupFrames = 4;

    private static readonly string[] Views =
    [
        "MainWindow.xml",
        "Browser.xml",
        "CharacterBrowser.xml",
        "MultiPicker.xml",
        "Options.xml",
        "ContextMenu.xml",
        "ColorPicker.xml"
    ];

    internal static void Run()
    {
        if (Screen.Selected != GameMain.GameScreen || GameMain.Instance?.LoadingScreenOpen == true)
        {
            DebugConsole.NewMessage("sandboxmenu.markup.selftest: open a round first.");
            return;
        }

        int loaded = 0;
        int diagnostics = 0;

        foreach (string view in Views)
        {
            if (ContextFor(view) is not { } context) { continue; }

            loaded++;
            diagnostics += Load(view, context);
        }

        DebugConsole.NewMessage($"sandboxmenu.markup.selftest: {loaded} views, {diagnostics} diagnostics.");
    }

    private static object? ContextFor(string view) => view switch
    {
        "MainWindow.xml" => MenuWindow.Instance.ViewModel,
        "Browser.xml" => new ItemBrowserViewModel(MenuWindow.Instance),
        "CharacterBrowser.xml" => CharacterBrowser(),
        "MultiPicker.xml" => MultiPicker(),
        "Options.xml" => Options(),
        "ContextMenu.xml" => new ContextMenuViewModel([new MenuCommand("sandboxmenu.close", static () => { })], static () => { }),
        _ => new ColorPickerViewModel(Color.White, static _ => { })
    };

    private static CharacterBrowserViewModel CharacterBrowser()
    {
        CharacterBrowserViewModel model = new(MenuWindow.Instance);
        model.Refresh();
        return model;
    }

    private static MultiPickerViewModel MultiPicker()
    {
        List<PickerToggle> toggles = [];

        foreach (ItemPackage package in ItemPrefabCatalog.Packages())
        {
            if (toggles.Count == 4) { break; }

            toggles.Add(new PickerToggle(
                RichString.Rich(Theme.AccentMarkup(package.Label, package.Package)),
                static () => false,
                static _ => { }));
        }

        return new MultiPickerViewModel(TextManager.Get("sandboxmenu.filter.packages"), toggles);
    }

    private static OptionsPickerViewModel Options()
        => new(
            TextManager.Get("sandboxmenu.browse.property"),
            [new PickerOption(TextManager.Get("sandboxmenu.summary.unset"), static () => { })],
            static _ => { });

    private static int Load(string view, object context)
    {
        int diagnostics = 0;
        ViewContext? loaded = null;

        try
        {
            loaded = ViewLoader.Load(
                view,
                context,
                Configure,
                message =>
                {
                    diagnostics++;
                    DebugConsole.NewMessage($"sandboxmenu.markup.selftest: {message}");
                },
                GUI.Canvas);

            if (loaded.RootElement is not IViewWindow window)
            {
                diagnostics++;
                DebugConsole.NewMessage($"sandboxmenu.markup.selftest: {view} does not open with a <Window>");
            }
            else
            {
                for (int frame = 0; frame < WarmupFrames; frame++) { loaded.RunFrameActions(); }

                diagnostics += CountPrunedOverlays(window.Frame.RectTransform);
            }
        }
        catch (Exception e)
        {
            diagnostics++;
            DebugConsole.NewMessage($"sandboxmenu.markup.selftest: {view} failed to load ({e.Message})");
        }
        finally
        {
            loaded?.Dispose();
        }

        return diagnostics;
    }

    private static void Configure(ViewContext view)
    {
        view.Dispatch = FrameActions.Run;
        view.IsInputBlocked = static () => false;
        view.Close = static () => { };
        view.MakeDraggable = static (_, _) => { };
    }

    private static int CountPrunedOverlays(RectTransform transform)
    {
        int found = 0;

        if (transform.GUIComponent is GUICustomComponent overlay && !overlay.Visible && HasDrawableChild(transform))
        {
            found++;
            DebugConsole.NewMessage(
                $"sandboxmenu.markup.selftest: an invisible overlay hides {ChildName(transform)} and everything under it");
        }

        foreach (RectTransform child in transform.Children) { found += CountPrunedOverlays(child); }

        return found;
    }

    private static bool HasDrawableChild(RectTransform transform)
    {
        foreach (RectTransform child in transform.Children)
        {
            if (child.GUIComponent is not null) { return true; }
        }

        return false;
    }

    private static string ChildName(RectTransform transform)
    {
        foreach (RectTransform child in transform.Children)
        {
            if (child.GUIComponent is { } control) { return control.GetType().Name; }
        }

        return "its controls";
    }
}
