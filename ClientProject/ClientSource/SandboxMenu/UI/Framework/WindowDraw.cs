using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Framework;

// What a dialog window needs to be drawn twice: once by the game's own draw pass, once into a screenshot, which
// cannot wait for it.
internal static class WindowDraw
{
    internal static Point Fit(Point size, Rectangle canvas)
        => new(
            Math.Clamp(size.X, 1, Math.Max(1, canvas.Width)),
            Math.Clamp(size.Y, 1, Math.Max(1, canvas.Height)));

    internal static Point ClampToCanvas(Point location, Point size, Rectangle bounds)
        => new(
            Math.Clamp(location.X, bounds.X, Math.Max(bounds.X, bounds.Right - size.X)),
            Math.Clamp(location.Y, bounds.Y, Math.Max(bounds.Y, bounds.Bottom - size.Y)));

    internal static void DrawInto(GUIComponent root, SpriteBatch spriteBatch, Point shift)
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

            // DrawManually takes a component off the automatic draw pass so that it is not drawn twice; this draw is
            // only a copy, so the window has to go back on it.
            RestoreAutoDraw(root);
        }
    }

    private static void RestoreAutoDraw(GUIComponent component)
    {
        component.AutoDraw = true;

        foreach (RectTransform child in component.RectTransform.Children) { RestoreAutoDraw(child.GUIComponent); }
    }
}
