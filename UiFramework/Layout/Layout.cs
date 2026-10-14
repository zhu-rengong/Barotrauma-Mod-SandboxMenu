namespace UiFramework.Layout;

internal static class Layout
{
    internal static Point Measure(ViewElement child)
    {
        if (child.Control is not GUITextBlock block || block.Font is not { } font) { return child.Control.Rect.Size; }

        if (child.MeasureCached(block, font, out Point cached)) { return cached; }

        Vector2 size = font.MeasureString(block.Text?.SanitizedValue ?? string.Empty, false) * block.TextScale;
        Point measured = new(
            (int)(size.X + block.Padding.X + block.Padding.Z),
            (int)(size.Y + block.Padding.Y + block.Padding.W));

        child.RememberMeasure(block, font, measured);

        return measured;
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
