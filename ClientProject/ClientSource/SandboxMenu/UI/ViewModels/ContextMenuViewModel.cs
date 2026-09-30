using System.Collections.ObjectModel;

namespace SandboxMenu.UI.ViewModels;

internal sealed class MenuOptionViewModel(MenuAction action, Action onInvoked)
{
    public LocalizedString Label => TextManager.Get(action.Label);

    public RichString? Shortcut => action.Shortcut is { } shortcut ? RichString.Rich(shortcut) : null;

    public RelayCommand InvokeCommand { get; } = new(() =>
    {
        onInvoked();
        action.Invoke();
    });
}

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
