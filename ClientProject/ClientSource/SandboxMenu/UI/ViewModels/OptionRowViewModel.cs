namespace SandboxMenu.UI.ViewModels;

internal sealed class OptionRowViewModel(LocalizedString text, Action onPicked) : PickerRowViewModel
{
    public LocalizedString Text { get; } = text;

    public RelayCommand PickCommand { get; } = new(onPicked);
}
