using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI;

internal sealed class UiWindow : IPopupWindow
{
    private static int _instances;

    private readonly GUIFrame _frame;
    private readonly GUIFrame _content;
    private readonly ViewLoadContext _view;
    private readonly int _order;
    private readonly Point _size;
    private readonly int _id = ++_instances;

    internal UiWindow(string markupFile, Point size, int updateOrder, object viewModel)
    {
        _size = size;
        _order = updateOrder;

        Point fit = Fit(size, GUI.Canvas.Rect);
        _frame = new GUIFrame(new RectTransform(fit, GUI.Canvas, Anchor.Center, null, ScaleBasis.Normal, isFixedSize: true), "GUIFrame")
        {
            Visible = false,
            CanBeFocused = false
        };

        _content = new GUIFrame(new RectTransform(new Vector2(0.97f, 0.93f), _frame.RectTransform, Anchor.Center), style: null)
        {
            HoverCursor = CursorState.Default
        };

        _view = ViewLoader.Load(markupFile, viewModel, message => Log.Warn($"#{_id} {message}"), _content.RectTransform);
        _view.Dispatch = MenuActions.Run;
        _view.DiagnosticSink = message => Log.Warn($"#{_id} {message}");
        _view.IsInputBlocked = () => UiWindow.InputBlocked;

        if (Find<GUIComponent>("DragArea") is { } dragArea)
        {
            _dragHandle = new GUIDragHandle(new RectTransform(Vector2.One, dragArea.RectTransform), _frame.RectTransform, null);
        }

        if (Find<GUIButton>("Close") is { } close)
        {
            close.OnClicked = (_, _) =>
            {
                MenuActions.Run(Close);
                return false;
            };
        }

        _ = new GUICustomComponent(
            new RectTransform(Vector2.One, _frame.RectTransform),
            (spriteBatch, _) => DrawOverlay(spriteBatch))
        {
            CanBeFocused = false
        };
    }

    public bool IsOpen { get; private set; }

    public static bool InputBlocked { get; set; }

    static UiWindow() => StaticState.Register(() =>
    {
        InputBlocked = false;
        _instances = 0;
    });

    private GUIDragHandle? _dragHandle;

    private bool _closing;

    public Action? Closing { get; set; }

    private void NotifyClosing()
    {
        if (_closing) { return; }

        _closing = true;
        Guard.Run($"Handing over the state of window #{_id} failed", () => Closing?.Invoke());
    }

    public Rectangle Rect => _frame.Rect;

    internal void PositionAt(Vector2 position)
    {
        Rectangle current = _frame.Rect;
        Rectangle bounds = GUI.Canvas.Rect;

        _frame.RectTransform.AbsoluteOffset +=
            ClampToCanvas(new Point((int)position.X, (int)position.Y), current.Size, bounds) - current.Location;
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

    public T? Find<T>(string name) where T : GUIComponent => _view.Names.Find(name)?.Control as T;

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

    private void DrawOverlay(SpriteBatch spriteBatch)
    {
        if (!IsOpen) { return; }

        for (int i = 0; i < _view.Overlays.Count; i++)
        {
            try
            {
                _view.Overlays[i](spriteBatch);
            }
            catch (Exception e)
            {
                Log.Warn($"#{_id} overlay failed", e);
            }
        }
    }

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

    private void KeepOnScreen()
    {
        Rectangle canvas = GUI.Canvas.Rect;
        Point fit = Fit(_size, canvas);
        if (_frame.RectTransform.NonScaledSize != fit) { _frame.RectTransform.NonScaledSize = fit; }

        if (_dragHandle is { } handle) { handle.DragArea = canvas; }

        Rectangle rect = _frame.Rect;
        _frame.RectTransform.ScreenSpaceOffset += ClampToCanvas(rect.Location, rect.Size, canvas) - rect.Location;
    }

    private static Point ClampToCanvas(Point location, Point size, Rectangle bounds)
        => new(
            Math.Clamp(location.X, bounds.X, Math.Max(bounds.X, bounds.Right - size.X)),
            Math.Clamp(location.Y, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - size.Y)));

    private static Point Fit(Point size, Rectangle canvas)
        => new(
            Math.Clamp(size.X, 1, Math.Max(1, canvas.Width)),
            Math.Clamp(size.Y, 1, Math.Max(1, canvas.Height)));
}
