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
