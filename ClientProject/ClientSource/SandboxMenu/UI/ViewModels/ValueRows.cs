namespace SandboxMenu.UI.ViewModels;

public sealed class RangeRow(EntryEditorViewModel editor, LocalizedString label, ValueRange? value, float fallback, float limit, Action<ValueRange?> apply)
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

public sealed class IntRow(EntryEditorViewModel editor, LocalizedString label, int? value, int fallback, int limit, Action<int?> apply)
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

public sealed class TextRow(EntryEditorViewModel editor, LocalizedString label, string text, Action<string> apply) : RowViewModel(label)
{
    private readonly EntryEditorViewModel _editor = editor;
    private readonly Action<string> _apply = apply;

    private string _text = text ?? string.Empty;

    public string Text
    {
        get => _text;
        set
        {
            if (!Set(ref _text, value ?? string.Empty)) { return; }

            _apply(_text);
            _editor.NotifyEdited();
        }
    }
}

public sealed class BrowseRow(EntryEditorViewModel editor, LocalizedString label, string text, Action<string> apply, Action browse) : RowViewModel(label)
{
    private readonly EntryEditorViewModel _editor = editor;
    private readonly Action<string> _apply = apply;

    private string _text = text ?? string.Empty;

    public string Text
    {
        get => _text;
        set
        {
            if (!Set(ref _text, value ?? string.Empty)) { return; }

            _apply(_text);
            _editor.NotifyEdited();
        }
    }

    public RelayCommand BrowseCommand { get; } = new RelayCommand(browse);
}
