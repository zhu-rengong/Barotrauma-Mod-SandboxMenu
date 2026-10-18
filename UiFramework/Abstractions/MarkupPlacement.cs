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

        Reading reading = ReadKind(node, attribute, ratio: true, diagnostics);

        if (!reading.Ok) { return (fallback, 0f); }

        return reading.Kind == LengthKind.Dip ? (0f, reading.Value) : (reading.Value, 0f);
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
        Reading reading = ReadKind(node, attribute, ratio, diagnostics);

        if (!reading.Ok) { return fallback; }

        if (reading.Kind == LengthKind.Dip && ratio)
        {
            Reject(node, attribute, "a fraction of the parent; DIP is taken at the window", diagnostics);
            return fallback;
        }

        if (reading.Kind == LengthKind.Percent && !ratio)
        {
            Reject(node, attribute, "a length; a percentage of the parent is not one", diagnostics);
            return fallback;
        }

        return reading.Value;
    }

    private static Reading ReadKind(MarkupNode node, string attribute, bool ratio, MarkupDiagnostics? diagnostics)
    {
        string text = node.Text(attribute)?.Trim() ?? string.Empty;

        if (text.Length == 0) { return Reading.Missing; }

        Length token = UiTokens.AsLength(text, Length.Auto);

        if (token.Kind is LengthKind.Dip or LengthKind.Percent) { return new Reading(token.Kind, token.Value); }

        if (text.EndsWith('%'))
        {
            return ValueParser.TryPercent(text, out float percent)
                ? new Reading(LengthKind.Percent, percent)
                : Invalid(node, attribute, "a percentage", diagnostics);
        }

        if (text.EndsWith("dip", StringComparison.OrdinalIgnoreCase))
        {
            return ValueParser.TryDip(text, out float dip)
                ? new Reading(LengthKind.Dip, dip)
                : Invalid(node, attribute, "a DIP length", diagnostics);
        }

        if (!ValueParser.TryFloat(text, out float value))
        {
            return Invalid(node, attribute, ratio ? "a fraction of the parent" : "a DIP length", diagnostics);
        }

        if (ratio && value > 1f)
        {
            diagnostics?.Report($"'{attribute}=\"{text}\"' is more than the whole parent; sizes inside a view are fractions", node);
        }

        return new Reading(ratio ? LengthKind.Percent : LengthKind.Dip, value);
    }

    private static Reading Invalid(MarkupNode node, string attribute, string expected, MarkupDiagnostics? diagnostics)
    {
        Reject(node, attribute, expected, diagnostics);
        return Reading.Missing;
    }

    private static void Reject(MarkupNode node, string attribute, string expected, MarkupDiagnostics? diagnostics)
    {
        diagnostics?.Report($"'{attribute}=\"{node.Text(attribute)?.Trim()}\"' is not {expected}", node);
    }

    private readonly record struct Reading(LengthKind Kind, float Value, bool Ok = true)
    {
        internal static Reading Missing { get; } = new(LengthKind.Auto, 0f, false);
    }
}
