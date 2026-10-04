using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.ViewModels;

// The main window itself: the function tabs on the title band and one view model per function. Each function's area
// binds against its own view model, so what a new function brings stays out of the others. The list under a function
// asks the window for what it needs to drag, and the window hands that to the function that owns the list.
internal sealed class SandboxMenuViewModel : IDropTarget, IListBackground
{
    internal SandboxMenuViewModel(IDialogHost host) => Spawn = new SpawnMenuViewModel(host);

    public FunctionSelectorViewModel Functions { get; } = new();

    public SpawnMenuViewModel Spawn { get; }

    public bool CanDrag(object item) => Spawn.CanDrag(item);

    public bool CanNest(object target) => Spawn.CanNest(target);

    public void Drop(object source, object? target, DropMode mode) => Spawn.Drop(source, target, mode);

    public void ShowBackgroundMenu(Vector2 position) => Spawn.ShowBackgroundMenu(position);
}
