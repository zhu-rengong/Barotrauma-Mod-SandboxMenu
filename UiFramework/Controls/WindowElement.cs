using Microsoft.Xna.Framework.Graphics;

namespace UiFramework.Controls;

// The root of a dialog: the host's frame around a markup view, in the size the view is written at, with the content
// inset the frame leaves. Everything else a window does — what closing it means, where it is dragged by — is the
// shell's, and markup asks for it by name (CloseView, Drag).
[Element("Window")]
internal sealed class WindowElement : ViewElement, IViewWindow
{
    private readonly ViewContext _view;

    public WindowElement(ElementContext context)
        : base(Create(context, out GUIFrame content, out Point nominal))
    {
        _view = context.View;
        _view.Window = this;

        Content = content;
        NominalSize = nominal;

        // The shell shows the window when it opens; until then it is kept out of the way like the host's own dialogs.
        Control.Visible = false;

        // The drop indicator and the frames the list draws go over the whole window, not over its content.
        _ = new GUICustomComponent(
            new RectTransform(Vector2.One, Control.RectTransform),
            (spriteBatch, _) => DrawOverlays(spriteBatch))
        {
            CanBeFocused = false
        };
    }

    public GUIComponent Frame => Control;

    public Point NominalSize { get; }

    internal GUIFrame Content { get; }

    public override RectTransform ContentParent => Content.RectTransform;

    public override void AddContent(ViewElement child)
    {
    }

    private void DrawOverlays(SpriteBatch spriteBatch)
    {
        if (!Control.Visible) { return; }

        List<Action<SpriteBatch>> overlays = _view.Overlays;

        for (int i = 0; i < overlays.Count; i++)
        {
            try
            {
                overlays[i](spriteBatch);
            }
            catch (Exception e)
            {
                UiLog.Warn("Drawing a view overlay failed", e);
            }
        }
    }

    // The window is written in DIP and the host takes pixels: the frame is the size the markup asks for, held inside
    // the canvas, and what is drawn inside it is the inset the frame leaves.
    private static GUIFrame Create(ElementContext context, out GUIFrame content, out Point nominal)
    {
        nominal = UiMetrics.DipSize(
            context.Metric("Width", 640f),
            context.Metric("Height", 480f));

        GUIFrame frame = new(
            new RectTransform(WindowDraw.Fit(nominal, GUI.Canvas.Rect), context.Parent, Anchor.Center, null, ScaleBasis.Normal, isFixedSize: true),
            "GUIFrame")
        {
            CanBeFocused = false
        };

        content = new GUIFrame(new RectTransform(new Vector2(0.97f, 0.93f), frame.RectTransform, Anchor.Center), style: null)
        {
            HoverCursor = CursorState.Default
        };

        return frame;
    }
}
