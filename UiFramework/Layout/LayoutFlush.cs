namespace UiFramework.Layout;

internal static class LayoutFlush
{
    internal static void Apply(GUIComponent root)
    {
        if (root is GUILayoutGroup group) { group.Recalculate(); }

        foreach (RectTransform child in root.RectTransform.Children)
        {
            if (child.GUIComponent is { } control) { Apply(control); }
        }
    }
}
