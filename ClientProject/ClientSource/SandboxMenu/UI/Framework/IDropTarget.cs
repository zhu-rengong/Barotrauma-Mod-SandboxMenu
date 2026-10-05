using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Framework;

internal interface IDropTarget : UiFramework.Data.IItemDropTarget
{
}

internal interface IDialogHost
{
    void ShowItemBrowser(Action<string> onPicked, ItemEntry? container = null);

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

internal sealed record PickerToggle(LocalizedString Label, Func<bool> IsTicked, Action<bool> Toggled);
