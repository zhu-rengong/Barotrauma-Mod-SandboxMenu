using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Framework;

internal sealed class RowControl : GUIButton
{
    private const string BaseStyle = "GUIButton";

    internal Anchor SelectedBar { get; set; } = Anchor.CenterLeft;

    internal RectTransform? SlotIcon { get; set; }

    internal bool SlotOnly { get; set; }

    internal Color SlotFill { get; set; } = Theme.SlotFill;

    internal Color SlotLine { get; set; } = Theme.SlotLine;

    internal float SlotPadding { get; set; } = Theme.SlotPadding;

    internal bool Highlight { get; set; } = true;

    internal Color Fill { get; set; } = Color.Transparent;

    internal Color SelectedBarFill { get; set; } = Theme.Accent;

    internal float SelectedBarWidth { get; set; } = Theme.SelectedBarWidth;

    private DipValue _slotPaddingDip;
    private DipValue _selectedBarDip;

    internal RowControl(RectTransform rectT, LocalizedString text, Alignment alignment)
        : base(rectT, text, alignment, BaseStyle)
    {
        Color = Color.Transparent;
        HoverColor = Color.Transparent;
        PressedColor = Color.Transparent;
        SelectedColor = Color.Transparent;
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

        Theme.ApplyLabel(TextBlock, Theme.Text);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
        if (!Visible) { return; }

        try
        {
            Rectangle rect = Rect;
            Rectangle? slot = SlotBox();

            if (slot is { } box)
            {
                Theme.Fill(spriteBatch, box, SlotFill);
                Theme.Outline(spriteBatch, box, SlotLine);
            }

            if (Highlight && Fill.A > 0f)
            {
                GUI.DrawRectangle(spriteBatch, SlotOnly && slot is { } marked ? marked : rect, Fill * (Fill.A / 255f), true, 0f, 1f);
            }

            if (Selected && SelectedBarFill.A > 0f)
            {
                GUI.DrawRectangle(spriteBatch, SelectedBarRect(rect), SelectedBarFill, true, 0f, 1f);
            }

            if (OutlineColor.A > 0)
            {
                Theme.Outline(spriteBatch, rect, OutlineColor);
            }
        }
        catch (Exception e)
        {
            DebugConsole.AddWarning($"Painting a list row failed: {e}");
        }
    }

    internal Rectangle? SlotBoxRect => SlotBox();

    private Rectangle? SlotBox()
    {
        if (SlotIcon is not { } icon) { return null; }

        Rectangle box = icon.Rect;
        if (box.Width <= 0 || box.Height <= 0) { return null; }

        int pad = _slotPaddingDip.Pixels(SlotPadding);

        return new Rectangle(box.X - pad, box.Y - pad, box.Width + 2 * pad, box.Height + 2 * pad);
    }

    private Rectangle SelectedBarRect(Rectangle rect)
    {
        int thickness = Math.Max(1, _selectedBarDip.Pixels(SelectedBarWidth));

        return SelectedBar switch
        {
            Anchor.TopLeft or Anchor.TopCenter or Anchor.TopRight => new Rectangle(rect.X, rect.Y, rect.Width, thickness),
            Anchor.CenterRight => new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height),
            Anchor.BottomLeft or Anchor.BottomCenter or Anchor.BottomRight => new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness),
            _ => new Rectangle(rect.X, rect.Y, thickness, rect.Height)
        };
    }
}
