namespace UiFramework;

internal sealed class ElementContext(MarkupNode node, RectTransform parent, ViewLoadContext view)
{
    internal MarkupNode Node { get; } = node;

    internal RectTransform Parent { get; } = parent;

    internal ViewLoadContext View { get; } = view;

    internal string? Skin => Node.Text("Skin") is { } style
        ? string.Equals(style, "None", StringComparison.OrdinalIgnoreCase) ? null : style
        : string.Empty;

    internal MarkupPlacement Place(float defaultWidth, float defaultHeight) => MarkupPlacement.Of(Node, defaultWidth, defaultHeight);

    internal RectTransform Rect(RectTransform parent, float defaultWidth, float defaultHeight)
        => Place(defaultWidth, defaultHeight).ToRectTransform(parent);

    internal string? Text(string attribute) => Node.Text(attribute);
}
