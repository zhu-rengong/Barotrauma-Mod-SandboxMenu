namespace SandboxMenu.UI.ViewModels;

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
