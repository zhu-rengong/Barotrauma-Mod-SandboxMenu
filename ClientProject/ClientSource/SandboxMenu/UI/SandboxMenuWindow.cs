using Microsoft.Xna.Framework;

namespace SandboxMenu.UI;

internal sealed class SandboxMenuWindow : IDialogHost
{
    private const int WindowOrder = 10;
    private const int DialogOrder = 30;
    private const int PopupOrder = 60;

    private static SandboxMenuWindow? _instance;

    static SandboxMenuWindow() => StaticState.Register(() => _instance = null);

    public static SandboxMenuWindow Instance => _instance ??= new SandboxMenuWindow();

    public static SandboxMenuWindow? Current => _instance;

    private readonly SpawnMenuViewModel _viewModel;
    private readonly List<IPopupWindow> _popups = [];

    private UiWindow? _window;

    private UiWindow? _browser;
    private ItemBrowserViewModel? _browserModel;

    private int? _builtContentVersion;

    private int _popupGrace;

    private SandboxMenuWindow() => _viewModel = new SpawnMenuViewModel(this);

    public bool IsOpen => _window?.IsOpen == true;

    public void Toggle()
    {
        if (IsOpen) { Close(); }
        else { Open(); }
    }

    public void Open()
    {
        HandleNotices();

        _window ??= new UiWindow(
            "MainWindow.xml",
            MenuTheme.DipSize(MenuTheme.WindowWidth, MenuTheme.WindowHeight),
            WindowOrder,
            _viewModel);

        _window.Open();
    }

    public void Close()
    {
        _window?.Close();
        ClosePopups();
        MenuActions.Clear();
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

        StaticState.ResetAll();
    }

    public void AddToUpdateList()
    {
        Guard.Run("Handling what the game reported failed", HandleNotices);
        Guard.Run("Handling the menu's popups failed", DrivePopups);
        Guard.Run("Updating the menu failed", UpdateViews);
        Guard.Run("Registering the menu on the GUI update list failed", RegisterViews);
        Guard.Run("Running the queued menu work failed", MenuActions.Flush);
    }

    private void HandleNotices()
    {
        DropIfContentChanged();

        if (ClientSpawnDispatcher.TryTakeResult(out SpawnStatus status, out int queued, out int problems))
        {
            _viewModel.ApplySpawnResult(status, queued, problems);
        }

        if (!MenuNotices.TakeResolution()) { return; }

        DropWindows();
    }

    private void DropIfContentChanged()
    {
        int contentVersion = ContentRevision.Current;

        if (_builtContentVersion is not { } built)
        {
            _builtContentVersion = contentVersion;
            return;
        }

        if (built == contentVersion) { return; }

        ItemPrefabCatalog.Invalidate();
        PropertyOverrideCatalog.Clear();

        DropWindows();
        _viewModel.ContentChanged();
        _builtContentVersion = contentVersion;
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
        UiWindow.InputBlocked = AnyPopupOpen();

        if (_popupGrace > 0) { _popupGrace--; }
        else { DismissPopupsOnOutsideClick(); }
    }

    private void UpdateViews()
    {
        _window?.Update();

        for (int i = 0; i < _popups.Count; i++)
        {
            if (_popups[i].IsOpen) { _popups[i].Update(); }
        }
    }

    private void RegisterViews()
    {
        _window?.Register();

        for (int i = 0; i < _popups.Count; i++)
        {
            if (_popups[i].IsOpen) { _popups[i].Register(); }
        }
    }

    public void ShowItemBrowser(Action<string> onPicked)
    {
        if (_browser is null || _browserModel is null)
        {
            _browserModel = new ItemBrowserViewModel(this);
            _browser = new UiWindow("Browser.xml", MenuTheme.DipSize(MenuTheme.BrowserWidth, MenuTheme.BrowserHeight), DialogOrder, _browserModel);

            _browserModel.Attach(_browser.Find<GUIListBox>("Results"));
        }

        _browserModel.PickInto(identifier =>
        {
            ClosePopups();
            onPicked(identifier);
        });

        ShowPopup(_browser);
    }

    public void ShowMultiPicker(LocalizedString title, IEnumerable<PickerToggle> options)
    {
        UiWindow popup = new(
            "MultiPicker.xml",
            MenuTheme.DipSize(MenuTheme.MultiPickerWidth, MenuTheme.MultiPickerHeight),
            DialogOrder + 1,
            new MultiPickerViewModel(title, options));

        ShowPopup(popup, keepOpen: true);
    }

    public void ShowOptions(LocalizedString title, IEnumerable<PickerOption> options)
    {
        OptionsPickerViewModel viewModel = new(title, options, option =>
        {
            ClosePopups();
            option.Picked();
        });

        ShowPopup(new UiWindow("Options.xml", MenuTheme.DipSize(MenuTheme.OptionsWidth, MenuTheme.OptionsHeight), DialogOrder, viewModel));
    }

    public void ShowContextMenu(IEnumerable<MenuAction> actions, Vector2 position)
    {
        ContextMenuViewModel viewModel = new(actions, ClosePopups);
        UiWindow popup = new("ContextMenu.xml", MenuTheme.DipSize(MenuTheme.ContextMenuWidth, MenuTheme.ContextMenuHeight), PopupOrder, viewModel);

        ShowPopup(popup);
        popup.PositionAt(position);
    }

    public void PickWorldPosition(Action<Vector2> onPicked)
    {
        Close();
        SpawnLocationPicker.Begin(onPicked, Open);
    }

    private void ShowPopup(IPopupWindow popup, bool keepOpen = false)
    {
        if (!keepOpen) { ClosePopups(); }

        popup.Open();
        _popups.Add(popup);
        _popupGrace = 2;
    }

    private void ClosePopups()
    {
        foreach (IPopupWindow popup in _popups)
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

        _popups.Clear();
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

        foreach (IPopupWindow popup in _popups)
        {
            if (popup.IsOpen && popup.Rect.Contains(mouse)) { return; }
        }

        ClosePopups();
    }
}
