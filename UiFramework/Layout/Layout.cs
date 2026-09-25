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
    public static Length Auto { get; } = new(LengthKind.Auto, 0f);

    public static Length Fill { get; } = new(LengthKind.Percent, 1f);

    public static Length Star(float weight = 1f) => new(LengthKind.Star, weight);

    public static Length Dip(float value) => new(LengthKind.Dip, value);

    public static Length Percent(float fraction) => new(LengthKind.Percent, fraction);

    public static Length Parse(string? text, Length fallback)
    {
        string trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0 || trimmed.Equals("Fill", StringComparison.OrdinalIgnoreCase)) { return trimmed.Length == 0 ? fallback : Fill; }
        if (trimmed.Equals("Auto", StringComparison.OrdinalIgnoreCase)) { return Auto; }
        if (trimmed.EndsWith('*')) { return float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float weight) ? Star(weight) : fallback; }
        if (trimmed.EndsWith('%')) { return float.TryParse(trimmed[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float percent) ? Percent(percent / 100f) : fallback; }

        return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float dip) ? Dip(dip) : fallback;
    }

    public int Resolve(int availablePixels, int measuredPixels, float starShare = 1f) => Kind switch
    {
        LengthKind.Auto => measuredPixels,
        LengthKind.Star => (int)MathF.Round(starShare * MathF.Max(0f, availablePixels)),
        LengthKind.Dip => (int)MathF.Round(UiMetrics.Dip(Value)),
        _ => (int)MathF.Round(Value * availablePixels)
    };
}

internal static class Layout
{
    public static Point Measure(ViewElement child)
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

    public static void Place(ViewElement child, Rectangle area)
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
