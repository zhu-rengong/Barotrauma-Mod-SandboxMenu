namespace SandboxMenu.UI.ViewModels;

internal sealed class BrowseRow(EntryEditorViewModel editor, LocalizedString label, string text, Action<string> apply, Action browse) : RowViewModel(label)
{
    private readonly EntryEditorViewModel _editor = editor;
    private readonly Action<string> _apply = apply;

    private string _text = text ?? string.Empty;

    private bool _takeFocus;

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

    // Set on the row the editor puts up for a fresh selection: the text box takes the keyboard once, then writes the
    // flag back so a re-pointed binding does not grab it (and the caret) again.
    public bool TakeFocus
    {
        get => _takeFocus;
        set => Set(ref _takeFocus, value);
    }
}
