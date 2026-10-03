using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace SandboxMenu.UI;

internal sealed class MenuHost : IDialogHost
{
    private const int WindowOrder = 10;
    private const int DialogOrder = 30;
    private const int PopupOrder = 60;

    private static MenuHost? _instance;

    static MenuHost()
    {
        ModLifetime.Unloading += () => _instance = null;
        ContentReload.Invalidated += ReleaseContent;
    }

    public static MenuHost Instance => _instance ??= new MenuHost();

    public static MenuHost? Current => _instance;

    private readonly SpawnMenuViewModel _viewModel;
    private readonly List<IDialogWindow> _popups = [];

    private MarkupWindow? _window;

    private MarkupWindow? _browser;
    private ItemBrowserViewModel? _browserModel;

    private int _popupGrace;
    private bool _screenshotQueued;
    private Character? _hintedFor;

    private MenuHost() => _viewModel = new SpawnMenuViewModel(this);

    public bool IsOpen => _window?.IsOpen == true;

    public void Toggle()
    {
        if (IsOpen) { Close(); }
        else { Open(); }
    }

    public void Open()
    {
        HandleNotices();

        _window ??= new MarkupWindow(
            "MainWindow.xml",
            UiMetrics.DipSize(Theme.WindowWidth, Theme.WindowHeight),
            WindowOrder,
            _viewModel);

        // The window is kept between openings, so the key hints it shows are refreshed against the settings.
        _viewModel.RefreshShortcuts();

        _window.Open();
    }

    // Escape steps back one layer at a time: the dialog on top first (a picker opened from the browser, then the
    // browser itself), and only then the menu.
    internal bool HandleEscape()
    {
        if (CloseTopPopup()) { return true; }

        if (!IsOpen) { return false; }

        Close();
        return true;
    }

    private bool CloseTopPopup()
    {
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            if (!_popups[i].IsOpen) { continue; }

            ClosePopup(_popups[i]);
            _popups.RemoveAt(i);

            return true;
        }

        return false;
    }

    // The menu's own keys, working on the entry the left list has selected and only while no dialog is up. Arrows and
    // Enter command rather than type, so they are taken even while a box has the keyboard; Del and "+" are text keys.
    internal void HandleKeys()
    {
        if (!IsOpen || AnyPopupOpen()) { return; }

        if (PlayerInput.KeyHit(Keys.Up) || PlayerInput.KeyHit(Keys.Down))
        {
            int direction = PlayerInput.KeyHit(Keys.Up) ? -1 : 1;
            bool alt = PlayerInput.KeyDown(Keys.LeftAlt) || PlayerInput.KeyDown(Keys.RightAlt);

            // Alt reorders inside the list the entry lives in; the arrows on their own walk the list as it is shown.
            if (alt) { _viewModel.MoveSelection(direction); }
            else { _viewModel.StepSelection(direction); }
        }

        if (PlayerInput.KeyHit(Keys.Enter)) { _viewModel.FocusIdentifier(); }

        if (GUI.KeyboardDispatcher.Subscriber is not null) { return; }

        if (PlayerInput.KeyHit(Keys.Delete)) { _viewModel.DeleteSelected(); }

        if (PlayerInput.KeyHit(Keys.OemPlus) || PlayerInput.KeyHit(Keys.Add))
        {
            // "+" sits on Shifted "=" on most layouts: without Shift the new entry lands next to the selection, with
            // Shift it is nested inside it.
            bool child = PlayerInput.KeyDown(Keys.LeftShift) || PlayerInput.KeyDown(Keys.RightShift);

            _viewModel.AddItem(child);
        }
    }

    internal void SpawnIntoInventory() => _viewModel.SpawnIntoInventoryCommand.Execute(null);

    // The capture is queued, not taken where the console runs it: the host lays its layout groups and lists out in
    // their own update, so a capture taken before that pass is served at the end of the frame instead.
    internal void CaptureScreenshot() => _screenshotQueued = true;

    internal void ServePendingScreenshot()
    {
        if (!_screenshotQueued) { return; }

        _screenshotQueued = false;

        List<IDialogWindow> drawn = [];

        if (_window is { IsOpen: true } window) { drawn.Add(window); }

        foreach (IDialogWindow popup in _popups)
        {
            if (popup.IsOpen) { drawn.Add(popup); }
        }

        if (drawn.Count == 0)
        {
            _viewModel.Report(TextManager.Get("sandboxmenu.status.screenshotclosed"));
            return;
        }

        ScreenshotWriter.Write(drawn, _viewModel.Report);
    }

    public void Close()
    {
        _window?.Close();
        ClosePopups();
        FrameActions.Clear();
    }

    internal static void Shutdown()
    {
        if (_instance is { } instance)
        {
            instance.Close();
            instance._window?.Dispose();
            instance._window = null;
            instance._browser?.Dispose();
            instance._browser = null;
            instance._browserModel = null;
        }

        _instance = null;

        ModLifetime.Unload();
    }

    public void AddToUpdateList()
    {
        HandleNotices();
        DrivePopups();
        UpdateViews();
        RegisterViews();
        FrameActions.Drain();
    }

    private void HandleNotices()
    {
        if (ContentReload.TakeRebuild()) { _viewModel.ContentChanged(); }

        if (ClientSpawnDispatcher.TryTakeResult(out SpawnStatus status, out int queued, out int problems))
        {
            _viewModel.ApplySpawnResult(status, queued, problems);
        }

        if (!ScreenReload.TakeRebuild()) { return; }

        DropWindows();
    }

    // Subscribed to ContentReload: what the menu holds from the old packages has to go even while the menu is
    // closed, or the plugin of a package that is being unloaded stays referenced.
    internal static void ReleaseContent() => _instance?.ReleaseCachedContent();

    private void ReleaseCachedContent()
    {
        DropWindows();
        _viewModel.ReleaseContent();
    }

    private void DropWindows()
    {
        ClosePopups();

        _window?.Dispose();
        _window = null;

        _browser?.Dispose();
        _browser = null;
        _browserModel = null;
    }

    private void DrivePopups()
    {
        MarkupWindow.InputBlocked = AnyPopupOpen();

        if (_popupGrace > 0) { _popupGrace--; }
        else { DismissPopupsOnOutsideClick(); }
    }

    private void UpdateViews()
    {
        _window?.Update();
        RefreshHints();

        for (int i = 0; i < _popups.Count; i++)
        {
            if (_popups[i].IsOpen) { _popups[i].Update(); }
        }
    }

    // The game reports nothing when whoever is being played changes, and the hints are weighed against that character,
    // so the rows are told to take their hints again once a frame.
    private void RefreshHints()
    {
        if (ReferenceEquals(_hintedFor, Character.Controlled)) { return; }

        _hintedFor = Character.Controlled;

        _viewModel.RefreshHints();
        _browserModel?.RefreshHints();
    }

    private void RegisterViews()
    {
        _window?.Register();

        for (int i = 0; i < _popups.Count; i++)
        {
            if (_popups[i].IsOpen) { _popups[i].Register(); }
        }
    }

    public void ShowItemBrowser(Action<string> onPicked, ItemEntry? container = null)
    {
        if (_browser is null || _browserModel is null)
        {
            _browserModel = new ItemBrowserViewModel(this);
            _browser = new MarkupWindow("Browser.xml", UiMetrics.DipSize(Theme.BrowserWidth, Theme.BrowserHeight), DialogOrder, _browserModel);

            _browserModel.Attach(_browser.Find<GUIListBox>("Results"));
        }

        _browserModel.UseParent(container);

        _browserModel.PickInto(identifier =>
        {
            ClosePopups();
            onPicked(identifier);
        });

        ShowPopup(_browser);
    }

    public void ShowMultiPicker(LocalizedString title, IEnumerable<PickerToggle> options)
    {
        MarkupWindow popup = new(
            "MultiPicker.xml",
            UiMetrics.DipSize(Theme.MultiPickerWidth, Theme.MultiPickerHeight),
            DialogOrder + 1,
            new MultiPickerViewModel(title, options));

        ShowPopup(popup, keepOpen: true);
    }

    public void ShowOptions(LocalizedString title, IEnumerable<PickerOption> options, bool filterable = false)
    {
        OptionsPickerViewModel viewModel = new(title, options, option =>
        {
            ClosePopups();
            option.Picked();
        }, filterable);

        ShowPopup(new MarkupWindow("Options.xml", UiMetrics.DipSize(Theme.OptionsWidth, Theme.OptionsHeight), DialogOrder, viewModel));
    }

    public void ShowContextMenu(IEnumerable<MenuAction> actions, Vector2 position)
    {
        ContextMenuViewModel viewModel = new(actions, ClosePopups);
        MarkupWindow popup = new("ContextMenu.xml", UiMetrics.DipSize(Theme.ContextMenuWidth, Theme.ContextMenuHeight), PopupOrder, viewModel);

        ShowPopup(popup);
        popup.PositionAt(position);
    }

    public void PickWorldPosition(Action<Vector2> onPicked)
    {
        Close();
        SpawnPointPicker.Begin(onPicked, Open);
    }

    private void ShowPopup(IDialogWindow popup, bool keepOpen = false)
    {
        if (!keepOpen) { ClosePopups(); }

        popup.Open();
        _popups.Add(popup);
        _popupGrace = 2;
    }

    private void ClosePopups()
    {
        foreach (IDialogWindow popup in _popups) { ClosePopup(popup); }

        _popups.Clear();
    }

    private void ClosePopup(IDialogWindow popup)
    {
        try
        {
            popup.Closing?.Invoke();

            if (ReferenceEquals(popup, _browser)) { _browser.Close(); }
            else { popup.Dispose(); }
        }
        catch (Exception e)
        {
            Log.Warn("Disposing a popup failed", e);
        }
    }

    private bool AnyPopupOpen()
    {
        for (int i = 0; i < _popups.Count; i++)
        {
            if (_popups[i].IsOpen) { return true; }
        }

        return false;
    }

    private void DismissPopupsOnOutsideClick()
    {
        if (_popups.Count == 0) { return; }
        if (!PlayerInput.PrimaryMouseButtonClicked() && !PlayerInput.SecondaryMouseButtonClicked()) { return; }

        Point mouse = PlayerInput.MousePosition.ToPoint();

        foreach (IDialogWindow popup in _popups)
        {
            if (popup.IsOpen && popup.Rect.Contains(mouse)) { return; }
        }

        ClosePopups();
    }
}
