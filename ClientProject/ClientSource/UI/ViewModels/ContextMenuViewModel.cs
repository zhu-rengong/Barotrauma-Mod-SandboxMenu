using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

internal sealed class ContextMenuViewModel
{
    public ContextMenuViewModel(IEnumerable<MenuCommand> actions, Action onInvoked)
    {
        foreach (MenuCommand action in actions)
        {
            Options.Add(new ContextOptionViewModel(action, onInvoked));
        }
    }

    public ObservableCollection<ContextOptionViewModel> Options { get; } = [];
}
