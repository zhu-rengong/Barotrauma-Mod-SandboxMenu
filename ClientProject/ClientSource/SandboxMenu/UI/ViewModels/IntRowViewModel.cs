namespace SandboxMenu.UI.ViewModels;

internal sealed class IntRowViewModel(EntryEditorViewModel editor, LocalizedString label, int? value, int fallback, int limit, Action<int?> apply)
    : RowViewModel(label, value.HasValue)
{
    private readonly EntryEditorViewModel _editor = editor;
    private readonly Action<int?> _apply = apply;

    private bool _enabled = value.HasValue;
    private int _value = value ?? fallback;

    public int Limit { get; } = limit;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (Set(ref _enabled, value)) { Apply(); }
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            if (Set(ref _value, value)) { Apply(); }
        }
    }

    private void Apply()
    {
        LabelActive = _enabled;
        _apply(_enabled ? Math.Clamp(_value, 0, Limit) : null);
        _editor.NotifyEdited();
    }
}
