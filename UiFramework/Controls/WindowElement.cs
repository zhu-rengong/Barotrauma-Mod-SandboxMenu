using Microsoft.Xna.Framework.Graphics;

namespace UiFramework.Controls;

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

        Control.Visible = false;

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
                DebugConsole.AddWarning($"Drawing a view overlay failed: {e}");
            }
        }
    }

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
