using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Framework;

internal interface IDialogHost
{
    void ShowItemBrowser(Action<string> onPicked, ItemEntry? container = null, Action<string>? onSelf = null);

    void ShowOptions(LocalizedString title, IEnumerable<PickerOption> options, bool filterable = false);

    void ShowMultiPicker(LocalizedString title, IEnumerable<PickerToggle> options);

    void ShowContextMenu(IEnumerable<MenuCommand> actions, Vector2? position = null);

    void ShowColorPicker(Color current, Action<Color> onPicked);

    void PickWorldPosition(Action<Vector2> onPicked);
}

internal sealed record MenuCommand(string Label, Action Invoke, LocalizedString? Shortcut = null);

internal sealed record PickerOption(LocalizedString Label, Action Picked, bool Editable = true, bool Saveable = true);

internal sealed record PickerToggle(RichString Label, Func<bool> IsTicked, Action<bool> Toggled, Sprite? Icon = null);
