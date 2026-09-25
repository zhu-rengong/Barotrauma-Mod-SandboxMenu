namespace SandboxMenu.UI.ViewModels;

public interface IEditorRow
{
}

public abstract class RowViewModel(LocalizedString label, bool labelActive = true) : Notifiable, IEditorRow
{
    private bool _labelActive = labelActive;

    public LocalizedString Label { get; } = label;

    public bool LabelActive
    {
        get => _labelActive;
        private protected set => Set(ref _labelActive, value);
    }
}

public sealed class SectionRow(LocalizedString title) : RowViewModel(title)
{
}

public sealed class HintRow(LocalizedString text) : RowViewModel(text)
{
}

public sealed class ButtonRow(LocalizedString label, Action invoke) : RowViewModel(label)
{
    public RelayCommand Command { get; } = new RelayCommand(invoke);
}

public sealed class TickRow(LocalizedString label, bool value, Action<bool> apply, Action after) : RowViewModel(label)
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
