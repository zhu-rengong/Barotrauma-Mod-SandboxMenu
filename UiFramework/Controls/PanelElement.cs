namespace UiFramework.Controls;

[Element("Panel")]
internal sealed class PanelElement : ViewElement
{
    public PanelElement(ElementContext context)
        : base(new GUIFrame(context.Rect(context.Parent, 1f, 1f), context.Skin)
        {
            HoverCursor = CursorState.Default
        })
    {
    }

    internal override void AddContent(ViewElement child)
    {
    }
}
