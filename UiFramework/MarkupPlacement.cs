using Microsoft.Xna.Framework;

namespace UiFramework;

internal readonly record struct MarkupPlacement(float Width, float Height, Anchor Anchor)
{
    internal static MarkupPlacement Of(MarkupNode node, float defaultWidth, float defaultHeight)
        => new(
            Read(node, "Width", defaultWidth),
            Measure(node, "Height", defaultHeight),
            ViewMarkup.AnchorOf(node.Text("Align"), Anchor.TopLeft));

    internal RectTransform ToRectTransform(RectTransform parent) => new(new Vector2(Width, Height), parent, Anchor);

    internal static float Read(MarkupNode node, string attribute, float fallback)
        => node.Text(attribute) is { } raw
            ? float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : fallback
            : fallback;

    internal static float Measure(MarkupNode node, string attribute, float fallback)
        => node.Text(attribute)?.ToLowerInvariant() switch
        {
            "row" => UiMetrics.RowHeight,
            "section" => UiMetrics.SectionHeight,
            "control" => UiMetrics.ControlHeight,
            "fill" => 1f,
            null => fallback,
            _ => Read(node, attribute, fallback)
        };
}
