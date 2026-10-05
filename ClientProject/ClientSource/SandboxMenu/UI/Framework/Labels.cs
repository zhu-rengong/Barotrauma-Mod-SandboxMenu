using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Framework;

internal static class Labels
{
    internal static void Apply(GUITextBlock block, Color color)
    {
        block.TextColor = color;
        block.HoverTextColor = color;
        block.SelectedTextColor = color;
        block.PressedColor = color;
        block.DisabledTextColor = Theme.TextDisabled;

        block.PressedTextColor = color;
        block.HoverSelectedTextColor = color;
    }

    // The package's own accent colour, spelled the one way the mod spells it: an item hint wears it, and so does the
    // package filter.
    internal static LocalizedString AccentMarkup(LocalizedString text, ContentPackage package)
        => "‖color:" + package.GetAccentColor().ToStringHex() + "‖" + text + "‖color:end‖";
}
