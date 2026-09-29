using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Framework;

internal interface IDropTarget : UiFramework.Data.IItemDropTarget
{
}

internal interface IDialogHost
{
    void ShowItemBrowser(Action<string> onPicked);

    void ShowOptions(LocalizedString title, IEnumerable<PickerOption> options);

    void ShowMultiPicker(LocalizedString title, IEnumerable<PickerToggle> options);

    void ShowContextMenu(IEnumerable<MenuAction> actions, Vector2 position);

    void PickWorldPosition(Action<Vector2> onPicked);
}

internal sealed record MenuAction(string Label, Action Invoke);

internal sealed record PickerOption(LocalizedString Label, Action Picked);

internal sealed record PickerToggle(LocalizedString Label, Func<bool> IsTicked, Action<bool> Toggled);
