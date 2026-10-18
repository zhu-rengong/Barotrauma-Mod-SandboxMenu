using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI;

internal sealed class MarkupWindow : IDialogWindow
{
    private static int _instances;

    private readonly IViewWindow _chrome;
    private readonly GUIComponent _frame;
    private readonly ViewContext _view;
    private readonly int _order;
    private readonly int _id = ++_instances;
    private readonly Point? _contentSize;

    internal MarkupWindow(string markupFile, int updateOrder, object viewModel, Point? contentSize = null)
    {
        _order = updateOrder;
        _contentSize = contentSize;

        _view = ViewLoader.Load(markupFile, viewModel, Configure, message => Services.DebugConsole.AddWarning($"#{_id} {message}"), GUI.Canvas);

        if (_view.RootElement is not IViewWindow chrome)
        {
            throw new InvalidDataException($"The view '{markupFile}' does not open with a <Window>");
        }

        _chrome = chrome;
        _frame = chrome.Frame;
    }

    public bool IsOpen { get; private set; }

    public Action? Closed { get; set; }

    internal bool ListDragBlocked { get; set; }

    private void Configure(ViewContext view)
    {
        view.Dispatch = FrameActions.Run;
        view.IsListDragBlocked = () => ListDragBlocked;
        view.Close = Close;
        view.ContentSize = _contentSize;
    }

    public Rectangle Rect => _frame.Rect;

    internal bool HasTransient => _view.CloseTransient is not null;

    internal bool DismissTransient() => _view.CloseTransient?.Invoke() ?? false;

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
        _view.RequestFocus();
    }

    public void Close()
    {
        DismissTransient();

        IsOpen = false;
        _frame.Visible = false;
        _frame.RemoveFromGUIUpdateList();
        _frame.RectTransform.Parent = null;

        Closed?.Invoke();
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

    internal void RunInputBindings() => _view.RunInputBindings();

    public void Dispose()
    {
        IsOpen = false;
        _frame.Visible = false;
        _frame.RemoveFromGUIUpdateList();
        _frame.RectTransform.Parent = null;
        _view.Dispose();
    }

    private void KeepOnScreen()
    {
        Rectangle canvas = GUI.Canvas.Rect;
        Point fit = WindowDraw.Fit(_chrome.NominalSize, canvas);
        if (_frame.RectTransform.NonScaledSize != fit) { _frame.RectTransform.NonScaledSize = fit; }

        Rectangle rect = _frame.Rect;
        _frame.RectTransform.ScreenSpaceOffset += WindowDraw.ClampToCanvas(rect.Location, rect.Size, canvas) - rect.Location;
    }
}
