namespace SandboxMenu.UI.ViewModels;

internal sealed class PickerRowViewModel(LocalizedString text, Action onPicked) : PickerRow
{
    public LocalizedString Text { get; } = text;

    public RelayCommand PickCommand { get; } = new(onPicked);
}
