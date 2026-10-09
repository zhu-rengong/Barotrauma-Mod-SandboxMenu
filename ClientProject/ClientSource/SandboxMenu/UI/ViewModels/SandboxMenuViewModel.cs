using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.ViewModels;

internal sealed class SandboxMenuViewModel : IItemDropTarget, IListBackground
{
    internal SandboxMenuViewModel(IDialogHost host) => Spawn = new SpawnPanelViewModel(host);

    public FunctionSelectorViewModel Functions { get; } = new();

    public SpawnPanelViewModel Spawn { get; }

    public bool CanDrag(object item) => Spawn.CanDrag(item);

    public bool CanNest(object target) => Spawn.CanNest(target);

    public void Drop(object source, object? target, DropMode mode) => Spawn.Drop(source, target, mode);

    public void ShowBackgroundMenu(Vector2 position) => Spawn.ShowBackgroundMenu(position);
}
