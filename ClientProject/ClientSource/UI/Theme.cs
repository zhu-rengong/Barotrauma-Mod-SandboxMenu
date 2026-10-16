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

    public const float MenuRowHeight = 21f;

    public const int MenuMinRows = 5;

    public const float MenuRowIndent = 10f;

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

    public static readonly Color BuffText = new(235, 219, 143);

    public const float SubTextRatio = 0.5f;

    public const float TileTextRatio = 0.24f;

    public const float TileLabelHeight = 16f;

    public const float TileValueHeight = 11f;

    public const float BadgeRatio = 0.18f;

    public const float PortraitExtent = 0.78f;

    public const float GroupIndent = 8f;

    public static readonly Color TagText = new(0x61, 0xAF, 0xEF, 0xFF);

    public const float TreeIndentStart = 8f;

    public static readonly Color OverlayFill = new(0, 0, 0, 190);

    public static readonly Color OverlayLine = new(148, 154, 160);

    public const float LineThickness = 1f;

    private static DipValue _lineDip;

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
        => AccentMarkup(text, package.GetAccentColor());

    internal static LocalizedString AccentMarkup(LocalizedString text, Color accent)
        => "‖color:" + accent.ToStringHex() + "‖" + text + "‖color:end‖";

    public static void Outline(SpriteBatch spriteBatch, Rectangle rect, Color color, float? thickness = null)
        => GUI.DrawRectangle(
            spriteBatch,
            rect,
            color,
            false,
            0f,
            Math.Max(1f, thickness ?? _lineDip.Pixels(LineThickness)));
}
