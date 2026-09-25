using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Framework;

internal sealed class MenuRow(RectTransform rectT, LocalizedString text, Alignment alignment)
    : GUIButton(rectT, text, alignment, BaseStyle)
{
    private const string BaseStyle = "GUIButton";

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
        if (!Visible) { return; }

        // This runs inside the engine's own drawing loop. What the row paints is the mod's own, so a failure in
        // it must not travel back into the game's drawing — and a plain try/catch keeps the per frame cost at
        try
        {
            Rectangle rect = Rect;
            Color fill = GetColor(State);

            if (fill.A > 0f)
            {
                GUI.DrawRectangle(spriteBatch, rect, fill * (fill.A / 255f), true, 0f, 1f);
            }

            if (Selected)
            {
                GUI.DrawRectangle(
                    spriteBatch,
                    new Rectangle(rect.X, rect.Y, MenuTheme.DipInt(MenuTheme.SelectedBarWidth), rect.Height),
                    MenuTheme.Accent,
                    true,
                    0f,
                    1f);
            }
        }
        catch (Exception e)
        {
            Log.Warn("Painting a list row failed", e);
        }
    }
}
