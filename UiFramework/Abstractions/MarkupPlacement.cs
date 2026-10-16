namespace UiFramework.Abstractions;

internal readonly record struct MarkupPlacement(float Width, float Height, Anchor Anchor, float HeightDip = 0f)
{
    internal static MarkupPlacement Of(MarkupNode node, float defaultWidth, float defaultHeight, MarkupDiagnostics? diagnostics = null)
    {
        (float height, float heightDip) = Measure(node, "Height", defaultHeight, diagnostics);

        return new(
            Size(node, "Width", defaultWidth, diagnostics),
            height,
            ViewMarkup.AnchorOf(node.Text("Align"), Anchor.TopLeft),
            heightDip);
    }

    internal RectTransform ToRectTransform(RectTransform parent)
        => HeightDip > 0f ? Fixed(parent) : new RectTransform(new Vector2(Width, Height), parent, Anchor);

    internal static float Size(MarkupNode node, string attribute, float fallback, MarkupDiagnostics? diagnostics = null)
    {
        string? raw = node.Text(attribute);

        if (Length.TryWeight(raw, out _)) { return 1f; }

        string text = raw?.Trim() ?? string.Empty;

        if (text.Length == 0) { return fallback; }
        if (text.Equals("Fill", StringComparison.OrdinalIgnoreCase) || text.Equals("*", StringComparison.Ordinal)) { return 1f; }

        return Read(node, attribute, fallback, diagnostics, ratio: true);
    }

    internal static float Metric(MarkupNode node, string attribute, float fallback, MarkupDiagnostics? diagnostics = null)
        => Read(node, attribute, fallback, diagnostics, ratio: false);

    private static (float Ratio, float Dip) Measure(
        MarkupNode node,
        string attribute,
        float fallback,
        MarkupDiagnostics? diagnostics)
    {
        string text = node.Text(attribute)?.Trim() ?? string.Empty;

        if (text.Length == 0) { return (fallback, 0f); }
        if (Length.TryWeight(text, out _)) { return (1f, 0f); }
        if (text.Equals("Fill", StringComparison.OrdinalIgnoreCase)) { return (1f, 0f); }

        Length token = UiTokens.AsLength(text, Length.Auto);

        if (token.Kind == LengthKind.Dip) { return (0f, token.Value); }
        if (token.Kind == LengthKind.Percent) { return (token.Value, 0f); }

        if (text.EndsWith('%'))
        {
            return ValueParser.TryPercent(text, out float percent)
                ? (percent, 0f)
                : (Reject(text, fallback, attribute, "a percentage", diagnostics, node), 0f);
        }

        if (text.EndsWith("dip", StringComparison.OrdinalIgnoreCase))
        {
            return ValueParser.TryDip(text, out float dip)
                ? (0f, dip)
                : (Reject(text, fallback, attribute, "a DIP length", diagnostics, node), 0f);
        }

        if (!ValueParser.TryFloat(text, out float value))
        {
            return (Reject(text, fallback, attribute, "a fraction of the parent", diagnostics, node), 0f);
        }

        if (value > 1f)
        {
            diagnostics?.Report($"'{attribute}=\"{text}\"' is more than the whole parent; sizes inside a view are fractions", node);
        }

        return (value, 0f);
    }

    private RectTransform Fixed(RectTransform parent)
    {
        int height = UiMetrics.DipInt(HeightDip);

        RectTransform transform = new(new Vector2(Width, 1f), parent, Anchor)
        {
            MinSize = new Point(0, height),
            MaxSize = new Point(int.MaxValue, height)
        };

        return transform;
    }

    private static float Read(MarkupNode node, string attribute, float fallback, MarkupDiagnostics? diagnostics, bool ratio)
    {
        if (node.Text(attribute) is not { } raw) { return fallback; }

        string text = raw.Trim();
        if (text.Length == 0) { return fallback; }

        if (text.EndsWith('%'))
        {
            if (!ratio) { return Reject(text, fallback, attribute, "a length; a percentage of the parent is not one", diagnostics, node); }

            return ValueParser.TryPercent(text, out float percent)
                ? percent
                : Reject(text, fallback, attribute, "a percentage", diagnostics, node);
        }

        if (text.EndsWith("dip", StringComparison.OrdinalIgnoreCase))
        {
            if (ratio) { return Reject(text, fallback, attribute, "a fraction of the parent; DIP is taken at the window", diagnostics, node); }

            return ValueParser.TryDip(text, out float dip)
                ? dip
                : Reject(text, fallback, attribute, "a DIP length", diagnostics, node);
        }

        if (!ValueParser.TryFloat(text, out float value))
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
