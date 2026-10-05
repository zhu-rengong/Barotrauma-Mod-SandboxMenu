using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Framework;

// A menu row styles itself: the host's GUIButton frame is taken apart into a flat row with a text label, and the row
// paints its own fill and selection bar — no state leaves the host a colour, so no skin sprite is drawn.
internal sealed class RowControl : GUIButton
{
    private const string BaseStyle = "GUIButton";

    // Which edge the selection mark sits on: a tree row marks its left, a function tab underlines its bottom.
    internal Anchor SelectedBar { get; set; } = Anchor.CenterLeft;

    // The icon the row frames, if it has one: the slot it sits in is worked out at draw time from the icon's own rect,
    // because a list recycles its rows and moves them to a new cell — a box remembered from when the row was built
    // would stay where the row used to be while the icon went with the row.
    internal RectTransform? SlotIcon { get; set; }

    // A tile is nothing but its slot, so that is where its state is drawn: the highlight then takes the slot's square
    // instead of the shape of the cell the tile fills.
    internal bool SlotOnly { get; set; }

    internal bool Hovered => State is GUIComponent.ComponentState.Hover or GUIComponent.ComponentState.Pressed;

    // A row that answers no click keeps no highlight either: the list hands it the hover state all the same.
    internal bool Highlight { get; set; } = true;

    // What the row animates for as long as it is on screen, asked with the frame's own time. The update pass reaches a
    // row anyway, so an animation costs nothing extra and runs for the rows the list has built, never for the items it
    // has not.
    internal Action<float>? Animate { get; set; }

    internal RowControl(RectTransform rectT, LocalizedString text, Alignment alignment)
        : base(rectT, text, alignment, BaseStyle)
    {
        // Every state colour stays transparent: what the host draws for a state is the skin's own graphic — a button bar
        // with a gradient in it, or the square slot's glow — and a row paints the fill below instead.
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

        Labels.Apply(TextBlock, Theme.Text);
    }

    public override void Update(float deltaTime)
    {
        base.Update(deltaTime);

        Animate?.Invoke(deltaTime);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
        if (!Visible) { return; }

        try
        {
            Rectangle rect = Rect;
            Rectangle? slot = SlotBox();
            Color fill = Highlight ? FillOf(State) : Color.Transparent;

            if (slot is { } box)
            {
                Theme.Fill(spriteBatch, box, Theme.SlotFill);
                Theme.Outline(spriteBatch, box, Theme.SlotLine);
            }

            if (fill.A > 0f)
            {
                GUI.DrawRectangle(spriteBatch, SlotOnly && slot is { } marked ? marked : rect, fill * (fill.A / 255f), true, 0f, 1f);
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

    // What a row shows for a state, in the menu's own palette: hover is the standard lift, pressed is the same fill
    // brighter, and a marked row takes the selected tint.
    private static Color FillOf(GUIComponent.ComponentState state) => state switch
    {
        GUIComponent.ComponentState.Hover or GUIComponent.ComponentState.HoverSelected => Theme.RowHover,
        GUIComponent.ComponentState.Pressed => Theme.RowPressed,
        GUIComponent.ComponentState.Selected => Theme.RowSelected,
        _ => Color.Transparent
    };

    // The slot is the icon's own box with the padding around it, so it sits exactly on the icon however the list moved
    // it, and an icon whose sprite is smaller than its box keeps the slot at one size.
    private Rectangle? SlotBox()
    {
        if (SlotIcon is not { } icon) { return null; }

        Rectangle box = icon.Rect;
        if (box.Width <= 0 || box.Height <= 0) { return null; }

        int pad = UiMetrics.DipInt(Theme.SlotPadding);

        return new Rectangle(box.X - pad, box.Y - pad, box.Width + 2 * pad, box.Height + 2 * pad);
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
