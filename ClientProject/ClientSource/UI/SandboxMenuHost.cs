using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI;

internal sealed class SandboxMenuHost : IDialogHost
{
    private const int WindowOrder = 10;
    private const int DialogOrder = 30;
    private const int PopupOrder = 60;

    private const double PanelTickInterval = 0.1;

    private static SandboxMenuHost? _instance;

    public static SandboxMenuHost Instance => _instance ??= new SandboxMenuHost();

    private readonly SandboxMenuViewModel _viewModel;
    private readonly List<Popup> _popups = [];

    private MarkupWindow? _window;

    private MarkupWindow? _browser;
    private ItemBrowserViewModel? _browserModel;

    private MarkupWindow? _characterBrowser;
    private CharacterBrowserViewModel? _characterBrowserModel;

    private MarkupWindow? _multiPicker;

    private int _popupGrace;
    private double _panelTickAt;
    private bool _pressedInsidePopup;
    private bool _screenshotQueued;

    private SandboxMenuHost()
    {
        _viewModel = new SandboxMenuViewModel(this);

        ClientSpawnDispatcher.Settled += _viewModel.Spawn.ApplySpawnResult;
        ClientAfflictionDispatcher.Settled += _viewModel.Afflictions.ApplyResult;
    }

    public bool IsOpen => _window?.IsOpen == true;

    internal SandboxMenuViewModel ViewModel => _viewModel;

    internal static void Create() => _ = Instance;

    public void Toggle()
    {
        if (IsOpen) { Close(); }
        else { Open(); }
    }

    public void Open()
    {
        _window ??= new MarkupWindow("MainWindow.xml", WindowOrder, _viewModel);

        _viewModel.Spawn.RefreshShortcuts();
        RefreshHints();
        _window.Open();
        UpdateInputState();
    }

    public void Close()
    {
        _window?.Close();
        ClosePopups();
        FrameActions.Clear();
        UpdateInputState();
    }

    internal bool HandleEscape()
    {
        if (CloseTopPopup()) { return true; }
        if (_window?.DismissTransient() == true) { return true; }

        if (!IsOpen) { return false; }

        Close();
        return true;
    }

    internal void HandleKeys()
    {
        if (!IsOpen || AnyPopupOpen()) { return; }
        if (_window?.HasTransient == true) { return; }

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

        foreach (Popup entry in _popups)
        {
            if (entry.Window.IsOpen) { drawn.Add(entry.Window); }
        }

        if (drawn.Count == 0)
        {
            _viewModel.Spawn.Report(StatusKind.Warning, TextManager.Get("sandboxmenu.status.screenshotclosed"));
            return;
        }

        ScreenshotWriter.Write(drawn, _viewModel.Spawn.Report);
    }

    internal void RefreshHints()
    {
        _viewModel.Spawn.RefreshHints();
        _browserModel?.RefreshHints();
    }

    public void ShowCharacterBrowser(Action<Character> onPicked)
    {
        if (_characterBrowser is null || _characterBrowserModel is null)
        {
            _characterBrowserModel = new CharacterBrowserViewModel(this);
            _characterBrowser = new MarkupWindow("CharacterBrowser.xml", DialogOrder, _characterBrowserModel);
        }

        _characterBrowserModel.Rebuild();
        _characterBrowserModel.PickInto(character =>
        {
            ClosePopups();
            onPicked(character);
        });

        ShowPopup(_characterBrowser, reusable: true, closed: _characterBrowserModel.Release);
    }

    internal static void Shutdown()
    {
        if (_instance is not { } menu) { return; }

        ClientSpawnDispatcher.Settled -= menu._viewModel.Spawn.ApplySpawnResult;
        ClientAfflictionDispatcher.Settled -= menu._viewModel.Afflictions.ApplyResult;

        menu.Reset();

        _instance = null;
    }

    internal static void Invalidated()
    {
        ContentCaches.Drop();

        if (_instance is not { } menu) { return; }

        menu.Reset();
        menu._viewModel.Spawn.ReleaseContent();
        menu._viewModel.Spawn.Rebuild();
        menu._viewModel.Afflictions.ReleaseContent();
    }

    internal static void ResolutionChanged() => _instance?.Reset();

    public void AddToUpdateList()
    {
        DrivePopups();
        UpdateViews();
        RegisterViews();
        FrameActions.Drain();
    }

    private void Reset()
    {
        DropWindows();
        FrameActions.Clear();
        NotificationBatch.Reset();
        UpdateInputState();
    }

    private void DropWindows()
    {
        ClosePopups();

        CharacterPreview.ReleaseAll();

        _window?.Dispose();
        _window = null;

        _browser?.Dispose();
        _browser = null;
        _browserModel = null;

        _characterBrowser?.Dispose();
        _characterBrowser = null;
        _characterBrowserModel = null;

        _multiPicker?.Dispose();
        _multiPicker = null;
    }

    private bool CloseTopPopup()
    {
        for (int i = _popups.Count - 1; i >= 0; i--)
        {
            if (!_popups[i].Window.IsOpen) { continue; }

            Popup entry = _popups[i];
            _popups.RemoveAt(i);

            ClosePopup(entry);
            UpdateInputState();

            return true;
        }

        return false;
    }

    private void DrivePopups()
    {
        if (!AnyPopupOpen()) { return; }

        TrackPopupPress();

        if (_popupGrace > 0) { _popupGrace--; return; }

        DismissPopupsOnOutsideClick();
    }

    private void TrackPopupPress()
    {
        if (!PlayerInput.PrimaryMouseButtonDown() && !PlayerInput.SecondaryMouseButtonDown()) { return; }

        _pressedInsidePopup = IsInsidePopup(PlayerInput.MousePosition.ToPoint());
    }

    private bool IsInsidePopup(Point mouse)
    {
        foreach (Popup entry in _popups)
        {
            if (entry.Window.IsOpen && entry.Window.Rect.Contains(mouse)) { return true; }
        }

        return false;
    }

    private void UpdateViews()
    {
        _window?.Update();

        TickPanels();

        for (int i = 0; i < _popups.Count; i++)
        {
            if (_popups[i].Window.IsOpen) { _popups[i].Window.Update(); }
        }
    }

    private void TickPanels()
    {
        double now = Timing.TotalTime;

        if (now - _panelTickAt < PanelTickInterval) { return; }

        _panelTickAt = now;

        RefreshAfflictions();
        RefreshCharacterToolTips();
    }

    private void RefreshAfflictions()
    {
        if (_window is not { IsOpen: true }) { return; }
        if (!_viewModel.Functions.ShowAfflictions) { return; }

        _viewModel.Afflictions.Tick();

        if (_window.Rect.Contains(PlayerInput.MousePosition.ToPoint())) { _viewModel.Afflictions.RefreshIconToolTip(); }
    }

    private void RefreshCharacterToolTips()
    {
        if (_characterBrowserModel is null || _characterBrowser is not { IsOpen: true }) { return; }
        if (!_characterBrowser.Rect.Contains(PlayerInput.MousePosition.ToPoint())) { return; }

        _characterBrowserModel.RefreshTiles();
    }

    private void RegisterViews()
    {
        _window?.Register();

        for (int i = 0; i < _popups.Count; i++)
        {
            if (_popups[i].Window.IsOpen) { _popups[i].Window.Register(); }
        }
    }

    private void UpdateInputState()
    {
        _window?.ListDragBlocked = AnyPopupOpen();
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
        _browserModel.RefreshHints();

        _browserModel.PickInto(identifier =>
        {
            ClosePopups();
            onPicked(identifier);
        });

        ShowPopup(_browser, reusable: true);
    }

    public void ShowMultiPicker(LocalizedString title, IEnumerable<PickerToggle> options)
    {
        if (_multiPicker is { IsOpen: true } previous)
        {
            _popups.RemoveAll(entry => ReferenceEquals(entry.Window, previous));
            ClosePopup(new Popup(previous, false, null));
        }

        _multiPicker = new MarkupWindow("MultiPicker.xml", DialogOrder + 1, new MultiPickerViewModel(title, options));

        ShowPopup(_multiPicker, keepOthers: true);
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
        MarkupWindow popup = new("ContextMenu.xml", PopupOrder, viewModel, ContextMenuSize(viewModel));

        ShowPopup(popup);
        popup.PositionAt(position ?? PlayerInput.MousePosition);
    }

    private static Point ContextMenuSize(ContextMenuViewModel viewModel)
    {
        float widest = 0f;

        foreach (ContextOptionViewModel option in viewModel.Options)
        {
            RichString text = ViewMarkup.WithShortcut(option.Label, option.Shortcut, ViewMarkup.HintKey);

            widest = MathF.Max(widest, GUIStyle.SmallFont.MeasureString(text, false).X * UiMetrics.TextScale);
        }

        int inset = UiMetrics.DipInt(Theme.MenuRowIndent) + UiMetrics.DipInt(Theme.Pad);

        return new Point(
            Math.Max(1, (int)MathF.Round(widest) + inset),
            Math.Max(Theme.MenuMinRows, viewModel.Options.Count) * UiMetrics.DipInt(Theme.MenuRowHeight));
    }

    public void PickWorldPosition(Action<Vector2> onPicked)
    {
        Close();
        SpawnPointPicker.Begin(onPicked, Open);
    }

    private void ShowPopup(IDialogWindow popup, bool keepOthers = false, bool reusable = false, Action? closed = null)
    {
        if (!keepOthers) { ClosePopups(); }

        popup.Closed = () => PopupClosed(popup);
        popup.Open();
        _popups.Add(new Popup(popup, reusable, closed));
        _popupGrace = 2;

        UpdateInputState();
    }

    private void ClosePopups()
    {
        if (_popups.Count == 0) { return; }

        Popup[] entries = [.. _popups];
        _popups.Clear();

        foreach (Popup entry in entries) { ClosePopup(entry); }

        UpdateInputState();
    }

    private void ClosePopup(Popup entry)
    {
        try
        {
            entry.Closed?.Invoke();

            if (entry.Reusable) { entry.Window.Close(); }
            else { entry.Window.Dispose(); }
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Disposing a popup failed: {e}");
        }
    }

    private void PopupClosed(IDialogWindow window)
    {
        int index = _popups.FindIndex(entry => ReferenceEquals(entry.Window, window));
        if (index < 0) { return; }

        Popup entry = _popups[index];
        _popups.RemoveAt(index);

        try
        {
            entry.Closed?.Invoke();

            if (!entry.Reusable) { entry.Window.Dispose(); }
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Closing a popup failed: {e}");
        }

        UpdateInputState();
    }

    private bool AnyPopupOpen()
    {
        for (int i = 0; i < _popups.Count; i++)
        {
            if (_popups[i].Window.IsOpen) { return true; }
        }

        return false;
    }

    private void DismissPopupsOnOutsideClick()
    {
        if (!PlayerInput.PrimaryMouseButtonClicked() && !PlayerInput.SecondaryMouseButtonClicked()) { return; }
        if (_pressedInsidePopup) { return; }
        if (IsInsidePopup(PlayerInput.MousePosition.ToPoint())) { return; }

        ClosePopups();
    }

    private readonly record struct Popup(IDialogWindow Window, bool Reusable, Action? Closed);
}
