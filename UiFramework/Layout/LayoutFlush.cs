namespace UiFramework.Layout;

// Applying a layout change on the spot, without the trap the host's own way of doing it sets: GUIComponent's
// ForceLayoutRecalculation runs a component update on every component of the sub-tree, twice each, and a button's
// update is where it decides that the mouse has been clicked — one click on a tab would be heard, and run, as many
// times as the tree is deep. A button's update never touches layout (it reads input and sets its state), so the only
// components that have anything to do here are the layout groups: laying their children out is all their own update
// would have done, and doing it now is what keeps a freshly shown area from being drawn for a frame in its old shape.
//
// The walk goes parent first: a group places its children, and a child that is a group of its own needs the rect it
// has just been given before it can place its own.
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
