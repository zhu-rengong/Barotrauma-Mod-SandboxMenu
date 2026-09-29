using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Framework;

internal static class MenuText
{
    internal static void Apply(GUITextBlock block, Color color)
    {
        block.TextColor = color;
        block.HoverTextColor = color;
        block.SelectedTextColor = color;
        block.PressedColor = color;
        block.DisabledTextColor = MenuTheme.TextDisabled;

        block.PressedTextColor = color;
        block.HoverSelectedTextColor = color;
    }

    internal static void SetLabelEnabled(GUITextBlock label, bool enabled)
    {
        Color color = enabled ? MenuTheme.Text : MenuTheme.TextDim;
        label.TextColor = color;
        label.OverrideTextColor(color);
    }

    internal static void MakeRow(GUIButton row)
    {
        row.Color = Color.Transparent;
        row.HoverColor = MenuTheme.RowHover;
        row.PressedColor = MenuTheme.RowPressed;
        row.SelectedColor = MenuTheme.RowSelected;
        row.DisabledColor = Color.Transparent;

        row.OutlineColor = Color.Transparent;

        for (int i = 0; i < row.CountChildren; i++)
        {
            if (row.GetChild(i) is not GUIFrame frame) { continue; }

            frame.Color = Color.Transparent;
            frame.HoverColor = Color.Transparent;
            frame.PressedColor = Color.Transparent;
            frame.SelectedColor = Color.Transparent;
            frame.DisabledColor = Color.Transparent;
            break;
        }

        Apply(row.TextBlock, MenuTheme.Text);
    }
}
