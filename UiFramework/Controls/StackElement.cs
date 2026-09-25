namespace UiFramework.Controls;

[Element("Stack")]
internal sealed class StackElement : ViewElement
{
    public StackElement(ElementContext context)
        : base(Create(context))
    {
    }

    internal override void AddContent(ViewElement child)
    {
    }

    private static GUILayoutGroup Create(ElementContext context)
    {
        bool horizontal = string.Equals(context.Text("Orientation") ?? "Vertical", "Horizontal", StringComparison.OrdinalIgnoreCase);

        return new GUILayoutGroup(
            context.Rect(context.Parent, 1f, 1f),
            horizontal,
            ViewMarkup.AnchorOf(context.Text("ChildAnchor"), horizontal ? Anchor.CenterLeft : Anchor.TopLeft))
        {
            Stretch = ViewMarkup.ToBool(context.Text("Stretch"), false),
            AbsoluteSpacing = UiMetrics.DipInt(MarkupPlacement.Read(context.Node, "Spacing", 0f)),
            RelativeSpacing = MarkupPlacement.Read(context.Node, "RelativeSpacing", 0f)
        };
    }
}
