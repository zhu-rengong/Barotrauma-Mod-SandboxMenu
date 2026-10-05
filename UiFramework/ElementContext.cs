namespace UiFramework;

// What an element is handed while it is built: the node it came from, the transform it sits in and the view it
// belongs to. The node itself stays inside the framework; the helpers below are what an element author uses.
public sealed class ElementContext
{
    internal ElementContext(MarkupNode node, RectTransform parent, ViewContext view)
    {
        Node = node;
        Parent = parent;
        View = view;
    }

    internal MarkupNode Node { get; }

    public RectTransform Parent { get; }

    public ViewContext View { get; }

    public string? Skin => Node.Text("Skin") is { } style
        ? string.Equals(style, "None", StringComparison.OrdinalIgnoreCase) ? null : style
        : string.Empty;

    internal MarkupPlacement Place(float defaultWidth, float defaultHeight) => MarkupPlacement.Of(Node, defaultWidth, defaultHeight, View.Diagnostics);

    public RectTransform Rect(RectTransform parent, float defaultWidth, float defaultHeight)
        => Place(defaultWidth, defaultHeight).ToRectTransform(parent);

    public string? Text(string attribute) => Node.Text(attribute);

    public float Size(string attribute, float fallback) => MarkupPlacement.Size(Node, attribute, fallback, View.Diagnostics);

    public float Metric(string attribute, float fallback) => MarkupPlacement.Metric(Node, attribute, fallback, View.Diagnostics);
}
