using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

public sealed class MenuOptionViewModel(MenuAction action, Action onInvoked)
{
    // The game's own string, handed over as it is: a menu that outlives a language switch reads it again by itself.
    public LocalizedString Label => TextManager.Get(action.Label);

    public RelayCommand InvokeCommand { get; } = new(() =>
    {
        onInvoked();
        action.Invoke();
    });
}

public sealed class ContextMenuViewModel
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
