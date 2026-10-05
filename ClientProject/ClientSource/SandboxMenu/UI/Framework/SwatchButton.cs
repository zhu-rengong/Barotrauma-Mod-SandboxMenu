using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Framework;

// The host has no colour button, so the swatch is painted by hand: it fills its rect with the colour it holds and
// outlines it, which is what also keeps a fully transparent colour visible.
internal sealed class SwatchButton : GUIButton
{
    private const string BaseStyle = "GUIButton";

    // What the chip leaves free above and below itself, in DIP: the swatch takes the height the layout gives it, and
    // what it paints is that less the padding, so nothing here has to know how tall a row is.
    internal float Padding { get; set; } = Theme.Pad;

    internal SwatchButton(RectTransform rectT, LocalizedString text)
        : base(rectT, text, Alignment.Center, BaseStyle)
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

        TextBlock.TextScale = UiMetrics.TextScale;
        TextBlock.AutoScaleHorizontal = true;
    }

    internal Color Swatch { get; set; } = Color.White;

    // What reads on a filled chip: the far end of the colour by how light it looks through the dark window behind it. A
    // colour that lets more of that window through is darker itself, so its label turns light along with it.
    internal static Color Contrast(Color color)
    {
        float shown = (0.299f * color.R + 0.587f * color.G + 0.114f * color.B) / 255f * (color.A / 255f);

        return shown > 0.55f ? Color.Black : Color.White;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
        if (!Visible) { return; }

        try
        {
            // The button keeps the whole slot as its hit area; what is drawn is the chip the padding leaves.
            Rectangle rect = Rect;
            int inset = Math.Clamp(UiMetrics.DipInt(Padding), 0, Math.Max(0, (rect.Height - 1) / 2));
            int side = Math.Min(inset, Math.Max(0, (rect.Width - 1) / 2));
            Rectangle chip = new(rect.X + side, rect.Y + inset, Math.Max(1, rect.Width - side * 2), Math.Max(1, rect.Height - inset * 2));

            GUI.DrawRectangle(spriteBatch, chip, Swatch, true, 0f, 1f);

            Color hint = GetColor(State);
            if (hint.A > 0f) { GUI.DrawRectangle(spriteBatch, chip, hint, true, 0f, 1f); }

            // The frame matches the one a text box draws, in the far end of the colour it surrounds.
            Theme.Outline(spriteBatch, chip, Color.Gray);
        }
        catch (Exception e)
        {
            Log.Warn("Painting the colour swatch failed", e);
        }
    }
}
