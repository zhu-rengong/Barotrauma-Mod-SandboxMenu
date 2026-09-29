using System.Globalization;
using Microsoft.Xna.Framework;

namespace UiFramework;

internal readonly record struct MarkupPlacement(float Width, float Height, Anchor Anchor)
{
    internal static MarkupPlacement Of(MarkupNode node, float defaultWidth, float defaultHeight, MarkupDiagnostics? diagnostics = null)
        => new(
            Size(node, "Width", defaultWidth, diagnostics),
            Measure(node, "Height", defaultHeight, diagnostics),
            ViewMarkup.AnchorOf(node.Text("Align"), Anchor.TopLeft));

    internal RectTransform ToRectTransform(RectTransform parent) => new(new Vector2(Width, Height), parent, Anchor);

    internal static float Size(MarkupNode node, string attribute, float fallback, MarkupDiagnostics? diagnostics = null)
        => node.Text(attribute)?.Trim().ToLowerInvariant() switch
        {
            "fill" or "*" => 1f,
            null or "" => fallback,
            _ => Read(node, attribute, fallback, diagnostics, ratio: true)
        };

    internal static float Metric(MarkupNode node, string attribute, float fallback, MarkupDiagnostics? diagnostics = null)
        => Read(node, attribute, fallback, diagnostics, ratio: false);

    internal static float Measure(MarkupNode node, string attribute, float fallback, MarkupDiagnostics? diagnostics = null)
        => node.Text(attribute)?.Trim().ToLowerInvariant() switch
        {
            "row" => UiMetrics.RowHeight,
            "section" => UiMetrics.SectionHeight,
            "control" => UiMetrics.ControlHeight,
            "fill" or "*" => 1f,
            null or "" => fallback,
            _ => Size(node, attribute, fallback, diagnostics)
        };

    private static float Read(MarkupNode node, string attribute, float fallback, MarkupDiagnostics? diagnostics, bool ratio)
    {
        if (node.Text(attribute) is not { } raw) { return fallback; }

        string text = raw.Trim();
        if (text.Length == 0) { return fallback; }

        if (text.EndsWith('%'))
        {
            if (!ratio) { return Reject(text, fallback, attribute, "a length; a percentage of the parent is not one", diagnostics, node); }

            return float.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float percent)
                ? percent / 100f
                : Reject(text, fallback, attribute, "a percentage", diagnostics, node);
        }

        if (text.EndsWith("dip", StringComparison.OrdinalIgnoreCase))
        {
            if (ratio) { return Reject(text, fallback, attribute, "a fraction of the parent; DIP is taken at the window", diagnostics, node); }

            return float.TryParse(text[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out float dip)
                ? dip
                : Reject(text, fallback, attribute, "a DIP length", diagnostics, node);
        }

        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            return Reject(text, fallback, attribute, ratio ? "a fraction of the parent" : "a DIP length", diagnostics, node);
        }

        if (ratio && value > 1f)
        {
            diagnostics?.Report($"'{attribute}=\"{text}\"' is more than the whole parent; sizes inside a view are fractions", node);
        }

        return value;
    }

    private static float Reject(string text, float fallback, string attribute, string expected, MarkupDiagnostics? diagnostics, MarkupNode node)
    {
        diagnostics?.Report($"'{attribute}=\"{text}\"' is not {expected}", node);
        return fallback;
    }
}
