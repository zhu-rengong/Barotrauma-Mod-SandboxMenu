namespace SandboxMenu.UI.ViewModels;

internal interface IEditorRow
{
}

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

internal sealed class SectionRowViewModel(LocalizedString title) : RowViewModel(title)
{
}

internal sealed class HintRowViewModel(LocalizedString text) : RowViewModel(text)
{
}

internal sealed class ButtonRowViewModel(LocalizedString label, Action onInvoke) : RowViewModel(label)
{
    public RelayCommand InvokeCommand { get; } = new RelayCommand(onInvoke);
}

internal sealed class TickRowViewModel(LocalizedString label, bool value, Action<bool> apply, Action after) : RowViewModel(label)
{
    private readonly Action<bool> _apply = apply;
    private readonly Action _after = after;

    private bool _value = value;

    public bool Value
    {
        get => _value;
        set
        {
            if (!Set(ref _value, value)) { return; }

            _apply(_value);
            _after();
        }
    }
}
