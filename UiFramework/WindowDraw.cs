using Microsoft.Xna.Framework.Graphics;

namespace UiFramework;

public static class WindowDraw
{
    public static Point Fit(Point size, Rectangle canvas)
        => new(
            Math.Clamp(size.X, 1, Math.Max(1, canvas.Width)),
            Math.Clamp(size.Y, 1, Math.Max(1, canvas.Height)));

    public static Point ClampToCanvas(Point location, Point size, Rectangle bounds)
        => new(
            Math.Clamp(location.X, bounds.X, Math.Max(bounds.X, bounds.Right - size.X)),
            Math.Clamp(location.Y, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - size.Y)));

    public static void DrawInto(GUIComponent root, SpriteBatch spriteBatch, Point shift)
    {
        RectTransform transform = root.RectTransform;
        Point original = transform.ScreenSpaceOffset;

        try
        {
            transform.ScreenSpaceOffset = original + shift;
            root.DrawManually(spriteBatch, alsoChildren: true, recursive: true);
        }
        finally
        {
            transform.ScreenSpaceOffset = original;

            RestoreAutoDraw(root);
        }
    }

    private static void RestoreAutoDraw(GUIComponent component)
    {
        component.AutoDraw = true;

        foreach (RectTransform child in component.RectTransform.Children) { RestoreAutoDraw(child.GUIComponent); }
    }
}
