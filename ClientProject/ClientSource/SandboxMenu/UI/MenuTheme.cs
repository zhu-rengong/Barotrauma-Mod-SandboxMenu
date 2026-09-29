using Microsoft.Xna.Framework;

namespace SandboxMenu.UI;

internal static class MenuTheme
{
    public static float Dip(float value) => UiMetrics.Dip(value);

    public static int DipInt(float value) => UiMetrics.DipInt(value);

    public static float FontScale(float dipSize, GUIFont font) => UiMetrics.FontScale(dipSize, font);

    public static Color Text => UiMetrics.Text;

    public static Color TextDim => UiMetrics.TextDim;

    public static Color Accent => UiMetrics.Accent;

    public static Color TextDisabled => UiMetrics.TextDisabled;

    public static float SelectedBarWidth => UiMetrics.SelectedBarWidth;

    public static float TreeIndentStep => UiMetrics.TreeIndentStep;

    public static float Pad => UiMetrics.Pad;

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

    public const float WindowWidth = 1008f;
    public const float WindowHeight = 624f;

    public const float BrowserWidth = 608f;
    public const float BrowserHeight = 528f;

    public const float MultiPickerWidth = 608f;
    public const float MultiPickerHeight = 528f;

    public const float OptionsWidth = 496f;
    public const float OptionsHeight = 432f;

    public const float ContextMenuWidth = 288f;
    public const float ContextMenuHeight = 352f;

    public static Point DipSize(float dipWidth, float dipHeight) => new(DipInt(dipWidth), DipInt(dipHeight));
}
