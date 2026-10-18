namespace SandboxMenu.UI.ViewModels;

internal abstract class RowViewModel(LocalizedString label, bool labelActive = true) : Notifiable, IEditorRow
{
    private bool _labelActive = labelActive;

    public LocalizedString Label { get; } = label;

    public bool LabelActive
    {
        get => _labelActive;
        private protected set => Set(ref _labelActive, value);
    }
}
