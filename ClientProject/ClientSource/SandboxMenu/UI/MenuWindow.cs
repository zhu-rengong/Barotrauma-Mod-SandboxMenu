using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI;

internal sealed class MenuWindow : IDialogHost
{
    private const int WindowOrder = 10;
    private const int DialogOrder = 30;
    private const int PopupOrder = 60;

    private static MenuWindow? _instance;

    public static MenuWindow Instance => _instance ??= new MenuWindow();

    public static MenuWindow? Current => _instance;

    internal static readonly RebuildSignal ResolutionChanged = new();

    private readonly SandboxMenuViewModel _viewModel;
    private readonly List<IDialogWindow> _popups = [];

    private MarkupWindow? _window;

    private MarkupWindow? _browser;
    private ItemBrowserViewModel? _browserModel;

    private MarkupWindow? _multiPicker;

    private int _popupGrace;
    private bool _pressedInsidePopup;
    private bool _screenshotQueued;
    private WeakReference<Character>? _hintedFor;

    private MenuWindow() => _viewModel = new SandboxMenuViewModel(this);

    public bool IsOpen => _window?.IsOpen == true;

    public void Toggle()
    {
        if (IsOpen) { Close(); }
        else { Open(); }
    }

    public void Open()
    {
        HandleNotices();

        _window ??= new MarkupWindow("MainWindow.xml", WindowOrder, _viewModel);

        _viewModel.Spawn.RefreshShortcuts();

        _window.Open();
    }

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

    internal void HandleKeys()
    {
        if (!IsOpen || AnyPopupOpen()) { return; }

        _window?.RunInputBindings();
    }

    internal void SpawnIntoInventory() => _viewModel.Spawn.SpawnIntoInventoryCommand.Execute(null);

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
        if (ContentReload.TakeRebuild())
        {
            ReleaseCachedContent();
            _viewModel.Spawn.ContentChanged();
        }

        if (ClientSpawnDispatcher.TryTakeResult(out SpawnStatus status, out int queued, out int problems))
        {
            _viewModel.Spawn.ApplySpawnResult(status, queued, problems);
        }

        if (ResolutionChanged.Take()) { DropWindows(); }
    }

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

    private void RefreshHints()
    {
        Character? controlled = Character.Controlled;

        if (_hintedFor is { } hinted && hinted.TryGetTarget(out Character? last) && ReferenceEquals(last, controlled)) { return; }
        if (_hintedFor is null && controlled is null) { return; }

        _hintedFor = controlled is null ? null : new WeakReference<Character>(controlled);

        _viewModel.Spawn.RefreshHints();
        _browserModel?.RefreshHints();
    }

    private void RegisterViews()
    {
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

    public void ShowContextMenu(IEnumerable<MenuCommand> actions, Vector2? position = null)
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
            DebugConsole.AddWarning($"Disposing a popup failed: {e}");
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
