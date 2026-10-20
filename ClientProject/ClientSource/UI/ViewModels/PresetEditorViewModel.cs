namespace SandboxMenu.UI.ViewModels;

internal sealed class PresetEditorViewModel : Notifiable
{
    private readonly PresetEditorRequest _request;
    private string _text;

    internal PresetEditorViewModel(PresetEditorRequest request)
    {
        _request = request;
        _text = request.Text;

        SaveCommand = new RelayCommand(Save);
        RevertCommand = new RelayCommand(Revert);
        CopyPathCommand = new RelayCommand(request.CopyPath);
    }

    public LocalizedString Title => _request.Title;

    public string Text
    {
        get => _text;
        set
        {
            string text = value ?? string.Empty;
            if (string.Equals(_text, text, StringComparison.Ordinal)) { return; }

            _text = text;
            Raise();
        }
    }

    public RelayCommand SaveCommand { get; }

    public RelayCommand RevertCommand { get; }

    public RelayCommand CopyPathCommand { get; }

    private void Save()
    {
        if (_request.Save(_text) is { } saved) { Text = saved; }
    }

    private void Revert()
    {
        if (_request.Revert() is { } text) { Text = text; }
    }
}
