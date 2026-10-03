using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

internal sealed class ContextMenuViewModel
{
    public ContextMenuViewModel(IEnumerable<MenuAction> actions, Action onInvoked)
    {
        foreach (MenuAction action in actions)
        {
            Options.Add(new MenuOptionViewModel(action, onInvoked));
        }
    }

    public ObservableCollection<MenuOptionViewModel> Options { get; } = [];
}
