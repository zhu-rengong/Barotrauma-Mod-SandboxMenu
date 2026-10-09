using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI;

internal static class Theme
{
    public static Color Text => UiMetrics.Text;

    public static Color TextDim => UiMetrics.TextDim;

    public static Color Accent => UiMetrics.Accent;

    public static Color TextDisabled => UiMetrics.TextDisabled;

    public static Color Callout => Color.LightGray * 0.7f;

    public const float SelectedBarWidth = 3f;

    public const float TreeIndentStep = 14f;

    public const float Pad = 6f;

    public const float Gap = 4f;

    public const float RowHeight = 0.075f;

    public const float SectionHeight = 0.085f;

    public const float ControlHeight = 0.84f;

    public const float LabelWidth = 0.38f;

    public const float IconBox = 24f;

    public const float IconGap = 6f;

    public const float TileSize = 46f;

    public const float SlotPadding = 3f;

    public const float IconShadowOffset = 2f;

    public static readonly Color SlotFill = new(56, 56, 56, 220);

    public static readonly Color SlotLine = new(122, 137, 152, 110);

    public static readonly Color IconShadow = new(0, 0, 0, 153);

    public const float IconSwell = 0.36f;

    public const float IconSwellSeconds = 0.12f;

    public const float FlashSeconds = 0.5f;

    public const float SubTextRatio = 0.5f;

    public static readonly Color RowHover = new(255, 255, 255, 84);

    public static readonly Color RowSelected = new(255, 255, 255, 60);

    public static readonly Color RowPressed = new(255, 255, 255, 110);

    public static readonly Color TagText = new(0x61, 0xAF, 0xEF, 0xFF);

    public const float TreeIndentStart = 8f;

    public static readonly Color OverlayFill = new(0, 0, 0, 190);

    public static readonly Color OverlayLine = new(148, 154, 160);

    public const float LineThickness = 1f;

    public const float PickerHintFontSize = 14f;

    public const float PickerHintOffsetX = 24f;
    public const float PickerHintOffsetY = 18f;

    public const float PickerHintArmLength = 8f;

    public const float PickerHintPaddingY = 4f;

    public static void Fill(SpriteBatch spriteBatch, Rectangle rect, Color color)
        => GUI.DrawRectangle(spriteBatch, rect, color, true);

    internal static void ApplyLabel(GUITextBlock block, Color color)
    {
        block.TextColor = color;
        block.HoverTextColor = color;
        block.SelectedTextColor = color;
        block.PressedColor = color;
        block.DisabledTextColor = TextDisabled;

        block.PressedTextColor = color;
        block.HoverSelectedTextColor = color;
    }

    internal static LocalizedString AccentMarkup(LocalizedString text, ContentPackage package)
        => "‖color:" + package.GetAccentColor().ToStringHex() + "‖" + text + "‖color:end‖";

    public static void Outline(SpriteBatch spriteBatch, Rectangle rect, Color color, float? thickness = null)
        => GUI.DrawRectangle(
            spriteBatch,
            rect,
            color,
            false,
            0f,
            Math.Max(1, thickness ?? UiMetrics.DipInt(LineThickness)));
}
