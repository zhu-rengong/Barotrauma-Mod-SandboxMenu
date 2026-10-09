using System.Globalization;

namespace UiFramework.Layout;

internal enum LengthKind
{
    Auto,

    Star,

    Dip,

    Percent
}

internal readonly record struct Length(LengthKind Kind, float Value)
{
    internal static Length Auto { get; } = new(LengthKind.Auto, 0f);

    internal static Length Fill { get; } = new(LengthKind.Percent, 1f);

    internal static Length Star(float weight = 1f) => new(LengthKind.Star, weight);

    internal static Length Dip(float value) => new(LengthKind.Dip, value);

    internal static Length Percent(float fraction) => new(LengthKind.Percent, fraction);

    internal static bool TryWeight(string? text, out float weight)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Equals("*", StringComparison.Ordinal)) { weight = 1f; return true; }

        if (trimmed.EndsWith('*')
            && float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out weight)
            && weight > 0f)
        {
            return true;
        }

        weight = 0f;
        return false;
    }

    internal static Length Parse(string? text, Length fallback, Action<string>? report = null)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0) { return fallback; }
        if (trimmed.Equals("Fill", StringComparison.OrdinalIgnoreCase)) { return Fill; }
        if (trimmed.Equals("Auto", StringComparison.OrdinalIgnoreCase)) { return Auto; }

        if (trimmed.EndsWith('*'))
        {
            return TryWeight(trimmed, out float weight)
                ? Star(weight)
                : Reject(trimmed, fallback, report);
        }

        if (trimmed.EndsWith('%'))
        {
            return float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float percent)
                ? Percent(percent / 100f)
                : Reject(trimmed, fallback, report);
        }

        if (trimmed.EndsWith("dip", StringComparison.OrdinalIgnoreCase))
        {
            return float.TryParse(trimmed[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out float dip)
                ? Dip(dip)
                : Reject(trimmed, fallback, report);
        }

        if (Keyword(trimmed) is { } keyword) { return keyword; }

        if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            return Reject(trimmed, fallback, report);
        }

        if (value > 1f) { report?.Invoke($"'{trimmed}' is a fraction of the panel; write {trimmed}dip for DIP"); }

        return Percent(value);
    }

    internal int Resolve(int available, int measured, float starShare = 1f) => Kind switch
    {
        LengthKind.Auto => measured,
        LengthKind.Star => (int)MathF.Round(starShare * MathF.Max(0f, available)),
        LengthKind.Dip => (int)MathF.Round(UiMetrics.Dip(Value)),
        _ => (int)MathF.Round(Value * available)
    };

    private static Length? Keyword(string text) => text.ToLowerInvariant() switch
    {
        "row" => UiTokens.AsLength("row", Percent(0.075f)),
        "section" => UiTokens.AsLength("section", Percent(0.085f)),
        "control" => UiTokens.AsLength("control", Percent(0.84f)),
        _ => null
    };

    private static Length Reject(string text, Length fallback, Action<string>? report)
    {
        report?.Invoke($"'{text}' is not a track size (a fraction, N%, N*, N dip, Auto or Fill)");
        return fallback;
    }
}

internal static class Layout
{
    internal static Point Measure(ViewElement child)
    {
        if (child.Control is GUITextBlock block && block.Font is { } font)
        {
            Vector2 size = font.MeasureString(block.Text.ToString() ?? string.Empty, false) * block.TextScale;
            return new Point(
                (int)(size.X + block.Padding.X + block.Padding.Z),
                (int)(size.Y + block.Padding.Y + block.Padding.W));
        }

        return child.Control.Rect.Size;
    }

    internal static void Place(ViewElement child, Rectangle area)
    {
        RectTransform transform = child.Control.RectTransform;
        RectTransform parent = transform.Parent ?? GUI.Canvas;

        transform.SetPosition(Anchor.TopLeft, Pivot.TopLeft);
        transform.AbsoluteOffset = new Point(area.X, area.Y);

        Rectangle parentRect = parent.Rect;
        transform.RelativeSize = parentRect.Width <= 0 || parentRect.Height <= 0
            ? Vector2.Zero
            : new Vector2(area.Width / (float)parentRect.Width, area.Height / (float)parentRect.Height);
    }
}
