namespace SandboxMenu.UI;

internal static class MarkupSelfTest
{
    private const int WarmupFrames = 4;

    private const int SwitchPasses = 3;

    private const int Tolerance = 1;

    private const string MainView = "MainWindow.xml";

    private static readonly string[] Views =
    [
        MainView,
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

    private static int AuditReleases(ViewElement? root, string view)
    {
        int found = ViewAudit.ReportReleases(
            root,
            message => DebugConsole.NewMessage($"sandboxmenu.markup.selftest: {view} {message}"));

        DebugConsole.NewMessage($"sandboxmenu.markup.selftest: {view} closed {ViewAudit.Count(root)} elements, {found} still held");

        return found;
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

        foreach (PackageLabel package in ItemPrefabCatalog.Packages())
        {
            if (toggles.Count == 4) { break; }

            toggles.Add(new PickerToggle(
                RichString.Rich(Theme.AccentMarkup(package.Label, package.Accent)),
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

                if (string.Equals(view, MainView, StringComparison.OrdinalIgnoreCase)) { diagnostics += VerifyFunctionSwitch(loaded, view); }
            }
        }
        catch (Exception e)
        {
            diagnostics++;
            DebugConsole.NewMessage($"sandboxmenu.markup.selftest: {view} failed to load ({e.Message})");
        }
        finally
        {
            ViewElement? root = loaded?.RootElement;

            loaded?.Dispose();

            diagnostics += AuditReleases(root, view);
        }

        return diagnostics;
    }

    private static int VerifyFunctionSwitch(ViewContext view, string viewName)
    {
        if (view.RootElement?.DataContext is not SandboxMenuViewModel model) { return 0; }

        if (view.FindElement("AfflictionTiles") is not { } tiles)
        {
            DebugConsole.NewMessage($"sandboxmenu.markup.selftest: {viewName} has no named affliction grid");
            return 1;
        }

        RectTransform column = tiles.Control.RectTransform.Parent ?? tiles.Control.RectTransform;

        model.Functions.SelectedIndex = (int)FunctionTab.Afflictions;
        Pump(view);
        Pump(view);

        Rectangle baselineColumn = column.Rect;
        Rectangle[] baseline = ChildRects(column);

        for (int pass = 0; pass < SwitchPasses; pass++)
        {
            model.Functions.SelectedIndex = (int)FunctionTab.Spawn;
            Pump(view);

            model.Functions.SelectedIndex = (int)FunctionTab.Afflictions;
            Pump(view);
        }

        int problems = 0;

        problems += ReportDrift(viewName, "affliction column", baselineColumn, column.Rect);

        Rectangle[] current = ChildRects(column);

        for (int i = 0; i < current.Length && i < baseline.Length; i++)
        {
            problems += ReportDrift(viewName, $"affliction column child {i}", baseline[i], current[i]);
        }

        model.Functions.SelectedIndex = (int)FunctionTab.Spawn;
        Pump(view);

        return problems;
    }

    private static int ReportDrift(string viewName, string what, Rectangle baseline, Rectangle current)
    {
        if (Math.Abs(current.X - baseline.X) <= Tolerance
            && Math.Abs(current.Y - baseline.Y) <= Tolerance
            && Math.Abs(current.Width - baseline.Width) <= Tolerance
            && Math.Abs(current.Height - baseline.Height) <= Tolerance)
        {
            return 0;
        }

        DebugConsole.NewMessage(
            $"sandboxmenu.markup.selftest: {viewName} {what} went from {baseline} to {current} after switching");

        return 1;
    }

    private static Rectangle[] ChildRects(RectTransform transform)
    {
        Rectangle[] rects = new Rectangle[transform.CountChildren];

        for (int i = 0; i < rects.Length; i++) { rects[i] = transform.GetChild(i).Rect; }

        return rects;
    }

    private static void Pump(ViewContext view)
    {
        for (int frame = 0; frame < WarmupFrames; frame++) { view.RunFrameActions(); }
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
