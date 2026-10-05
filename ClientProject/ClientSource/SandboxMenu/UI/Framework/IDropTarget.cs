using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Framework;

internal interface IDropTarget : UiFramework.Data.IItemDropTarget
{
}

internal interface IDialogHost
{
    // The second errand a browser can carry: what the right mouse button does with the item. A caller that has nowhere
    // to put it leaves it out and the browser offers the left mouse button alone.
    void ShowItemBrowser(Action<string> onPicked, ItemEntry? container = null, Action<string>? onSelf = null);

    void ShowOptions(LocalizedString title, IEnumerable<PickerOption> options, bool filterable = false);

    void ShowMultiPicker(LocalizedString title, IEnumerable<PickerToggle> options);

    // Where it opens is the shell's business: a menu opened by a key lands where the pointer is, one opened by a
    // click inside a list lands where the click did.
    void ShowContextMenu(IEnumerable<MenuAction> actions, Vector2? position = null);

    void ShowColorPicker(Color current, Action<Color> onPicked);

    void PickWorldPosition(Action<Vector2> onPicked);
}

internal sealed record MenuAction(string Label, Action Invoke, LocalizedString? Shortcut = null);

internal sealed record PickerOption(LocalizedString Label, Action Picked, bool Editable = true, bool Saveable = true);

// The label is rich text because a package carries its own accent colour, and the icon is what a category is shown by.
internal sealed record PickerToggle(RichString Label, Func<bool> IsTicked, Action<bool> Toggled, Sprite? Icon = null);
