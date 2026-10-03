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

    internal static void SetLabelEnabled(GUITextBlock label, bool enabled)
    {
        Color color = enabled ? Theme.Text : Theme.TextDim;
        label.TextColor = color;
        label.OverrideTextColor(color);
    }

}
