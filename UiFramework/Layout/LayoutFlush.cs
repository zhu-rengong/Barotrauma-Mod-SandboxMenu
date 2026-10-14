namespace UiFramework.Layout;

internal static class LayoutFlush
{
    internal static void Apply(GUIComponent root)
    {
        RectTransform transform = root.RectTransform;

        if (root is GUILayoutGroup group && group.NeedsToRecalculate) { group.Recalculate(); }

        for (int i = 0; i < transform.CountChildren; i++)
        {
            if (transform.GetChild(i).GUIComponent is { } control) { Apply(control); }
        }
    }
}
