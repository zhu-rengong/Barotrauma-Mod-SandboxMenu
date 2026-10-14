namespace UiFramework.Layout;

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
