namespace SandboxMenu.UI.ViewModels;

internal sealed class TextRowViewModel(EntryEditorViewModel editor, LocalizedString label, string text, Action<string> apply) : RowViewModel(label)
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
