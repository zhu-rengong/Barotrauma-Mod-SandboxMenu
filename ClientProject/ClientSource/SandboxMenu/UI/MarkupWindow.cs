using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI;

// What is left after markup builds the window: being put in and taken out of the host's update list, being kept on
// screen, and handing the input to the dialog on top.
internal sealed class MarkupWindow : IDialogWindow
{
    private static int _instances;

    private readonly IViewWindow _chrome;
    private readonly GUIComponent _frame;
    private readonly ViewContext _view;
    private readonly int _order;
    private readonly int _id = ++_instances;

    private GUIDragHandle? _dragHandle;
    private bool _closing;

    internal MarkupWindow(string markupFile, int updateOrder, object viewModel)
    {
        _order = updateOrder;

        _view = ViewLoader.Load(markupFile, viewModel, Configure, message => Log.Warn($"#{_id} {message}"), GUI.Canvas);

        if (_view.RootElement is not IViewWindow chrome)
        {
            throw new InvalidDataException($"The view '{markupFile}' does not open with a <Window>");
        }

        _chrome = chrome;
        _frame = chrome.Frame;
    }

    public bool IsOpen { get; private set; }

    public static bool InputBlocked { get; set; }

    static MarkupWindow() => ModLifetime.Unloading += () =>
    {
        InputBlocked = false;
        _instances = 0;
    };

    // Handed to the view before it is built: what markup asks for by name (CloseView, Drag) arrives here rather
    // than being wired up to controls looked up afterwards.
    private void Configure(ViewContext view)
    {
        view.Dispatch = FrameActions.Run;
        view.IsInputBlocked = () => InputBlocked;
        view.Close = Close;
        view.MakeDraggable = (region, target) => _dragHandle = new GUIDragHandle(new RectTransform(Vector2.One, region), target, null);
    }

    public Action? Closing { get; set; }

    private void NotifyClosing()
    {
        if (_closing) { return; }

        _closing = true;
        Guard.Run(() => Closing?.Invoke(), $"Handing over the state of window #{_id} failed");
    }

    public Rectangle Rect => _frame.Rect;

    public void DrawInto(SpriteBatch spriteBatch, Point shift) => WindowDraw.DrawInto(_frame, spriteBatch, shift);

    internal void PositionAt(Vector2 position)
    {
        Rectangle current = _frame.Rect;
        Rectangle bounds = GUI.Canvas.Rect;

        _frame.RectTransform.AbsoluteOffset +=
            WindowDraw.ClampToCanvas(new Point((int)position.X, (int)position.Y), current.Size, bounds) - current.Location;
    }

    public void Open()
    {
        _frame.RectTransform.Parent = GUI.Canvas;
        IsOpen = true;
        _frame.Visible = true;
        InputBlocked = true;
        _view.RequestFocus();
    }

    internal void Close()
    {
        IsOpen = false;
        _frame.Visible = false;
        _frame.RemoveFromGUIUpdateList();
        _frame.RectTransform.Parent = null;
        InputBlocked = false;
        NotifyClosing();
    }

    public void Register()
    {
        if (!IsOpen || !_frame.Visible) { return; }

        KeepOnScreen();

        _frame.AddToGUIUpdateList(false, _order);
    }

    public void Update()
    {
        if (!IsOpen) { return; }

        _view.RunFrameActions();
    }

    // The keys the view declares; the shell decides when they are asked for.
    internal void RunInputBindings() => _view.RunInputBindings();

    public void Dispose()
    {
        IsOpen = false;
        _frame.Visible = false;
        _frame.RemoveFromGUIUpdateList();
        _frame.RectTransform.Parent = null;
        InputBlocked = false;
        NotifyClosing();
        _view.Dispose();
    }

    // A dialog on top takes the input: the window under it stays drawn but is kept out of the update pass, so a click
    // that lands inside the dialog never also reaches the controls of the window behind it.
    internal void SetInteractive(bool interactive) => SetAutoUpdate(_frame, interactive);

    private static void SetAutoUpdate(GUIComponent component, bool enabled)
    {
        component.AutoUpdate = enabled;

        // A rect can hold no component at all: the host does that itself in places, so the walk skips them.
        foreach (RectTransform child in component.RectTransform.Children)
        {
            if (child.GUIComponent is { } control) { SetAutoUpdate(control, enabled); }
        }
    }

    private void KeepOnScreen()
    {
        Rectangle canvas = GUI.Canvas.Rect;
        Point fit = WindowDraw.Fit(_chrome.NominalSize, canvas);
        if (_frame.RectTransform.NonScaledSize != fit) { _frame.RectTransform.NonScaledSize = fit; }

        if (_dragHandle is { } handle) { handle.DragArea = canvas; }

        Rectangle rect = _frame.Rect;
        _frame.RectTransform.ScreenSpaceOffset += WindowDraw.ClampToCanvas(rect.Location, rect.Size, canvas) - rect.Location;
    }
}
