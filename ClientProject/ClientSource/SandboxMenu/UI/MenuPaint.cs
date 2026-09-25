using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Framework;

internal static class MenuPaint
{
    internal static void Fill(SpriteBatch spriteBatch, Rectangle rect, Color color)
        => GUI.DrawRectangle(spriteBatch, rect, color, true);

    internal static void Outline(SpriteBatch spriteBatch, Rectangle rect, Color color, float? thickness = null)
        => GUI.DrawRectangle(
            spriteBatch,
            rect,
            color,
            false,
            0f,
            thickness ?? MenuTheme.DipInt(MenuTheme.LineThickness));
}
