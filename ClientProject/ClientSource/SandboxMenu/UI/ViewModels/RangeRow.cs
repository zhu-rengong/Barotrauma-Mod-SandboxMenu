namespace SandboxMenu.UI.ViewModels;

internal sealed class RangeRow(EntryEditorViewModel editor, LocalizedString label, ValueRange? value, float fallback, float limit, Action<ValueRange?> apply)
    : RowViewModel(label, value.HasValue)
{
    private readonly EntryEditorViewModel _editor = editor;
    private readonly Action<ValueRange?> _apply = apply;

    private bool _enabled = value.HasValue;
    private float _min = value?.Min ?? fallback;
    private float _max = value?.Max ?? fallback;

    public float Limit { get; } = limit;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (Set(ref _enabled, value)) { Apply(); }
        }
    }

    public float Min
    {
        get => _min;
        set
        {
            if (Set(ref _min, value)) { Apply(); }
        }
    }

    public float Max
    {
        get => _max;
        set
        {
            if (Set(ref _max, value)) { Apply(); }
        }
    }

    private void Apply()
    {
        LabelActive = _enabled;
        _apply(_enabled ? new ValueRange(_min, _max) : null);
        _editor.NotifyEdited();
    }
}
