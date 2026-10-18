namespace SandboxMenu.UI.ViewModels;

internal sealed class ButtonRowViewModel(LocalizedString label, Action onInvoke) : RowViewModel(label)
{
    public RelayCommand InvokeCommand { get; } = new RelayCommand(onInvoke);
}
