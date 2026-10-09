using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Framework;

internal sealed class RowControl : GUIButton
{
    private const string BaseStyle = "GUIButton";

    internal Anchor SelectedBar { get; set; } = Anchor.CenterLeft;

    internal RectTransform? SlotIcon { get; set; }

    internal bool SlotOnly { get; set; }

    internal bool Hovered => State is GUIComponent.ComponentState.Hover or GUIComponent.ComponentState.Pressed;

    internal bool Highlight { get; set; } = true;

    internal Action<float>? Animate { get; set; }

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
            DebugConsole.AddWarning($"Painting a list row failed: {e}");
        }
    }

    private static Color FillOf(GUIComponent.ComponentState state) => state switch
    {
        GUIComponent.ComponentState.Hover or GUIComponent.ComponentState.HoverSelected => Theme.RowHover,
        GUIComponent.ComponentState.Pressed => Theme.RowPressed,
        GUIComponent.ComponentState.Selected => Theme.RowSelected,
        _ => Color.Transparent
    };

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
