using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Framework;

// A menu row styles itself: the host's GUIButton frame is taken apart into a flat row with a text label, and the
// row paints its own fill and selection bar.
internal sealed class RowControl : GUIButton
{
    private const string BaseStyle = "GUIButton";

    // Which edge the selection mark sits on: a tree row marks its left, a function tab underlines its bottom.
    internal Anchor SelectedBar { get; set; } = Anchor.CenterLeft;

    internal RowControl(RectTransform rectT, LocalizedString text, Alignment alignment)
        : base(rectT, text, alignment, BaseStyle)
    {
        Color = Color.Transparent;
        HoverColor = Theme.RowHover;
        PressedColor = Theme.RowPressed;
        SelectedColor = Theme.RowSelected;
        DisabledColor = Color.Transparent;

        OutlineColor = Color.Transparent;

        for (int i = 0; i < CountChildren; i++)
        {
            if (GetChild(i) is not GUIFrame frame) { continue; }

            frame.Color = Color.Transparent;
            frame.HoverColor = Color.Transparent;
            frame.PressedColor = Color.Transparent;
            frame.SelectedColor = Color.Transparent;
            frame.DisabledColor = Color.Transparent;
            break;
        }

        Labels.Apply(TextBlock, Theme.Text);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
        if (!Visible) { return; }

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
                GUI.DrawRectangle(spriteBatch, SelectedBarRect(rect), Theme.Accent, true, 0f, 1f);
            }
        }
        catch (Exception e)
        {
            Log.Warn("Painting a list row failed", e);
        }
    }

    private Rectangle SelectedBarRect(Rectangle rect)
    {
        int thickness = Math.Max(1, UiMetrics.DipInt(Theme.SelectedBarWidth));

        return SelectedBar switch
        {
            Anchor.TopLeft or Anchor.TopCenter or Anchor.TopRight => new Rectangle(rect.X, rect.Y, rect.Width, thickness),
            Anchor.CenterRight => new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height),
            Anchor.BottomLeft or Anchor.BottomCenter or Anchor.BottomRight => new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness),
            _ => new Rectangle(rect.X, rect.Y, thickness, rect.Height)
        };
    }
}
