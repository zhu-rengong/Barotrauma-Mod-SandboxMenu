using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

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

    private int _popupGrace;
    private bool _screenshotQueued;
    private Character? _hintedFor;

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
    // their own update, so a draw taken before that pass is done shows rows and columns at their earlier places. It is
    // served at the end of the frame instead, once the GUI update has run through.
    internal void CaptureScreenshot() => _screenshotQueued = true;

    internal void ServePendingScreenshot()
    {
        if (!_screenshotQueued) { return; }

        _screenshotQueued = false;
        WriteScreenshot();
    }

    // Renders what the menu is showing right now — the window and any dialog over it — onto an offscreen target and
    // writes it out as a PNG. The host draws its GUI from screen coordinates, so the batch is shifted by the captured
    // area to put it at the origin; the host's own offscreen image code is the model for the target and the state it
    // has to put back.
    private void WriteScreenshot()
    {
        List<IPopupWindow> drawn = [];

        if (_window is { IsOpen: true } window) { drawn.Add(window); }

        foreach (IPopupWindow popup in _popups)
        {
            if (popup.IsOpen) { drawn.Add(popup); }
        }

        if (drawn.Count == 0)
        {
            _viewModel.Report(TextManager.Get("sandboxmenu.status.screenshotclosed"));
            return;
        }

        Rectangle area = drawn[0].Rect;
        foreach (IPopupWindow popup in drawn) { area = Rectangle.Union(area, popup.Rect); }

        if (area.Width <= 0 || area.Height <= 0) { return; }

        string folder = Path.Combine(Plugin.SettingsService.SaveFolder, "Screenshots");
        string name = $"sandboxmenu {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png";
        string path = Path.Combine(folder, name);

        GraphicsDevice device = GameMain.Instance.GraphicsDevice;

        // The host renders at its own virtual resolution and keeps the device's viewport on that, so a render target of
        // the captured area has to be given a viewport of its own: without it the window — which sits in the middle of
        // the canvas — is squeezed into a corner of the image.
        Viewport previousViewport = device.Viewport;
        Rectangle previousScissor = device.ScissorRectangle;

        try
        {
            Directory.CreateDirectory(folder);

            using RenderTarget2D target = new(device, area.Width, area.Height, false, SurfaceFormat.Color, DepthFormat.None);
            using SpriteBatch batch = new(device);

            try
            {
                device.SetRenderTarget(target);
                device.Viewport = new Viewport(0, 0, target.Width, target.Height);
                device.ScissorRectangle = new Rectangle(0, 0, target.Width, target.Height);
                device.Clear(Color.Transparent);

                // Deferred with the sampler the host draws its GUI with: BackToFront would sort the menu's components by
                // depth and texture instead of leaving them in the order they are drawn in. The windows are shifted into
                // place themselves (see DrawInto) rather than by the batch, because parts of the host's GUI restart the
                // batch for their own draw and would ignore a transform.
                batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, GUI.SamplerState, null, null, null);

                try
                {
                    Point shift = new(-area.X, -area.Y);

                    foreach (IPopupWindow popup in drawn) { popup.DrawInto(batch, shift); }

                    DrawCursor(batch, shift);
                }
                finally
                {
                    batch.End();
                }

                device.SetRenderTarget(null);

                using FileStream stream = File.Create(path);
                target.SaveAsPng(stream, target.Width, target.Height);
            }
            finally
            {
                device.SetRenderTarget(null);
                device.Viewport = previousViewport;
                device.ScissorRectangle = previousScissor;
                GameMain.Instance.ResetViewPort();
            }

            _viewModel.Report(TextManager.GetWithVariable("sandboxmenu.status.screenshot", "[name]", name));
            Log.Info($"Saved a screenshot of the menu to '{path}'");
        }
        catch (Exception e)
        {
            Log.Warn("Saving a screenshot of the menu failed", e);
            _viewModel.Report(TextManager.Get("sandboxmenu.status.screenshotfailed"));
        }
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
        if (ContentWatch.TakeChanged()) { _viewModel.ContentChanged(); }

        if (ClientSpawnDispatcher.TryTakeResult(out SpawnStatus status, out int queued, out int problems))
        {
            _viewModel.ApplySpawnResult(status, queued, problems);
        }

        if (!MenuNotices.TakeResolution()) { return; }

        DropWindows();
    }

    // Called the moment the content packages change: what the menu holds from the old packages has to go even while
    // the menu is closed, or the plugin of a package that is being unloaded stays referenced.
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
        UiWindow.InputBlocked = AnyPopupOpen();

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

    // The item hints weigh what an item asks for against the skills of whoever is being played, and the game reports
    // nothing when that changes; compared once a frame here, the rows and tiles on screen take their hints again the
    // moment the character is switched or lost, instead of holding on to what they said when they were built.
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
            _browser = new UiWindow("Browser.xml", MenuTheme.DipSize(MenuTheme.BrowserWidth, MenuTheme.BrowserHeight), DialogOrder, _browserModel);

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
        UiWindow popup = new(
            "MultiPicker.xml",
            MenuTheme.DipSize(MenuTheme.MultiPickerWidth, MenuTheme.MultiPickerHeight),
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
        foreach (IPopupWindow popup in _popups) { ClosePopup(popup); }

        _popups.Clear();
    }

    // The host draws its pointer in the screen pass, which a screenshot does not take part in; it goes on top of the
    // windows here, in whatever state the menu is showing right now — the hand over buttons and rows, the arrow
    // elsewhere.
    private static void DrawCursor(SpriteBatch spriteBatch, Point shift)
    {
        if (!GameMain.WindowActive || GUI.HideCursor || !GUI.MouseCursorSprites.Prefabs.Any()) { return; }

        Sprite? sprite = GUI.MouseCursorSprites[GUI.MouseCursor] ?? GUI.MouseCursorSprites[CursorState.Default];

        if (sprite is null) { return; }

        sprite.Draw(
            spriteBatch,
            PlayerInput.LatestMousePosition + shift.ToVector2(),
            Color.White,
            sprite.Origin,
            0f,
            GUI.Scale / 1.5f,
            SpriteEffects.None,
            null);
    }

    private void ClosePopup(IPopupWindow popup)
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

        foreach (IPopupWindow popup in _popups)
        {
            if (popup.IsOpen && popup.Rect.Contains(mouse)) { return; }
        }

        ClosePopups();
    }
}
