using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

    private readonly SandboxMenuViewModel _viewModel;
    private readonly List<IDialogWindow> _popups = [];

    private MarkupWindow? _window;

    private MarkupWindow? _browser;
    private ItemBrowserViewModel? _browserModel;

    private MarkupWindow? _multiPicker;

    private int _popupGrace;
    private bool _pressedInsidePopup;
    private bool _screenshotQueued;
    private Character? _hintedFor;

    private MenuHost() => _viewModel = new SandboxMenuViewModel(this);

    public bool IsOpen => _window?.IsOpen == true;

    public void Toggle()
    {
        if (IsOpen) { Close(); }
        else { Open(); }
    }

    public void Open()
    {
        HandleNotices();

        // The window's size is written in its own markup, where the view it holds is written.
        _window ??= new MarkupWindow("MainWindow.xml", WindowOrder, _viewModel);

        // The window is kept between openings, so the key hints it shows are refreshed against the settings.
        _viewModel.Spawn.RefreshShortcuts();

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

    // The menu's own keys are declared by the view that answers to them, so all that is left is the shell's part of
    // it: while no dialog is up, the window runs whatever keys are live in what it is showing.
    internal void HandleKeys()
    {
        if (!IsOpen || AnyPopupOpen()) { return; }

        _window?.RunInputBindings();
    }

    internal void SpawnIntoInventory() => _viewModel.Spawn.SpawnIntoInventoryCommand.Execute(null);

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
            _viewModel.Spawn.Report(TextManager.Get("sandboxmenu.status.screenshotclosed"));
            return;
        }

        ScreenshotWriter.Write(drawn, _viewModel.Spawn.Report);
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
        if (ContentReload.TakeRebuild()) { _viewModel.Spawn.ContentChanged(); }

        if (ClientSpawnDispatcher.TryTakeResult(out SpawnStatus status, out int queued, out int problems))
        {
            _viewModel.Spawn.ApplySpawnResult(status, queued, problems);
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
        _viewModel.Spawn.ReleaseContent();
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

        TrackPopupPress();

        if (_popupGrace > 0) { _popupGrace--; }
        else { DismissPopupsOnOutsideClick(); }
    }

    // A click that began inside a dialog is not a click outside it, wherever the button ends up coming loose: dragging
    // from the colour picker onto the window behind it used to close the picker.
    private void TrackPopupPress()
    {
        if (!PlayerInput.PrimaryMouseButtonDown() && !PlayerInput.SecondaryMouseButtonDown()) { return; }

        _pressedInsidePopup = IsInsidePopup(PlayerInput.MousePosition.ToPoint());
    }

    private bool IsInsidePopup(Point mouse)
    {
        foreach (IDialogWindow popup in _popups)
        {
            if (popup.IsOpen && popup.Rect.Contains(mouse)) { return true; }
        }

        return false;
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

        _viewModel.Spawn.RefreshHints();
        _browserModel?.RefreshHints();
    }

    private void RegisterViews()
    {
        // The dialog on top is what takes input; the menu stays drawn underneath it but stops being updated.
        _window?.SetInteractive(!AnyPopupOpen());
        _window?.Register();

        for (int i = 0; i < _popups.Count; i++)
        {
            if (_popups[i].IsOpen) { _popups[i].Register(); }
        }
    }

    public void ShowItemBrowser(Action<string> onPicked, ItemEntry? container = null, Action<string>? onSelf = null)
    {
        if (_browser is null || _browserModel is null)
        {
            _browserModel = new ItemBrowserViewModel(this);
            _browser = new MarkupWindow("Browser.xml", DialogOrder, _browserModel);
        }

        _browserModel.UseParent(container);
        _browserModel.UseOnSelf(onSelf);

        _browserModel.PickInto(identifier =>
        {
            ClosePopups();
            onPicked(identifier);
        });

        ShowPopup(_browser);
    }

    public void ShowMultiPicker(LocalizedString title, IEnumerable<PickerToggle> options)
    {
        // A picker that is already open is closed before the new one takes its place, the way every other dialog here
        // reopens: the button that opened it stays live under it, so a second click must not stack a second list.
        if (_multiPicker is { IsOpen: true } previous)
        {
            _popups.Remove(previous);
            ClosePopup(previous);
        }

        _multiPicker = new MarkupWindow("MultiPicker.xml", DialogOrder + 1, new MultiPickerViewModel(title, options));

        ShowPopup(_multiPicker, keepOpen: true);
    }

    public void ShowOptions(LocalizedString title, IEnumerable<PickerOption> options, bool filterable = false)
    {
        OptionsPickerViewModel viewModel = new(title, options, option =>
        {
            ClosePopups();
            option.Picked();
        }, filterable);

        ShowPopup(new MarkupWindow("Options.xml", DialogOrder, viewModel));
    }

    public void ShowColorPicker(Color current, Action<Color> onPicked)
        => ShowPopup(new MarkupWindow("ColorPicker.xml", DialogOrder, new ColorPickerViewModel(current, onPicked)));

    public void ShowContextMenu(IEnumerable<MenuAction> actions, Vector2? position = null)
    {
        ContextMenuViewModel viewModel = new(actions, ClosePopups);
        MarkupWindow popup = new("ContextMenu.xml", PopupOrder, viewModel);

        ShowPopup(popup);
        popup.PositionAt(position ?? PlayerInput.MousePosition);
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
        if (_pressedInsidePopup) { return; }
        if (IsInsidePopup(PlayerInput.MousePosition.ToPoint())) { return; }

        ClosePopups();
    }
}
