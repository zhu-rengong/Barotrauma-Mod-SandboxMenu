using Microsoft.Xna.Framework.Graphics;
using UiFramework.Styling;

namespace UiFramework.Controls;

[Element("Window")]
internal sealed class WindowElement : ViewElement, IViewWindow
{
    private const float ContentWidth = 0.97f;

    private const float ContentHeight = 0.93f;

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

    [ElementProperty(Phase = ApplyPhase.Construct)]
    internal string? ContentInset => Node?.Text("ContentInset");

    public override RectTransform ContentParent => Content.RectTransform;

    private static Point WindowSizeFor(Point content)
        => new(
            Math.Max(1, (int)MathF.Round(content.X / ContentWidth)),
            Math.Max(1, (int)MathF.Round(content.Y / ContentHeight)));

    private static Vector2 ContentScale(Point nominal, int inset)
    {
        if (inset <= 0) { return new Vector2(ContentWidth, ContentHeight); }

        return new Vector2(
            Math.Max(0.1f, (nominal.X - 2f * inset) / nominal.X),
            Math.Max(0.1f, (nominal.Y - 2f * inset) / nominal.Y));
    }

    public override void AddContent(ViewElement child)
    {
    }

    private void DrawOverlays(SpriteBatch spriteBatch)
    {
        if (!Control.Visible) { return; }

        IReadOnlyList<Action<SpriteBatch>> overlays = _view.Overlays;

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
        nominal = context.View.ContentSize is { } requested
            ? WindowSizeFor(requested)
            : UiMetrics.DipSize(context.Metric("Width", 640f), context.Metric("Height", 480f));

        GUIComponentStyle? style = GuiStyles.Resolve(
            context.Skin is { Length: > 0 } skin ? skin : "GUIFrame",
            message => context.View.Diagnostics.Report(message, context.Node));

        GUIFrame frame = new(
            new RectTransform(WindowDraw.Fit(nominal, GUI.Canvas.Rect), context.Parent, Anchor.Center, null, ScaleBasis.Normal, isFixedSize: true),
            style: null)
        {
            CanBeFocused = false
        };

        if (style is not null) { frame.ApplyStyle(style); }

        content = new GUIFrame(new RectTransform(ContentScale(nominal, UiMetrics.DipInt(context.Metric("ContentInset", 0f))), frame.RectTransform, Anchor.Center), style: null)
        {
            HoverCursor = CursorState.Default
        };

        return frame;
    }
}
