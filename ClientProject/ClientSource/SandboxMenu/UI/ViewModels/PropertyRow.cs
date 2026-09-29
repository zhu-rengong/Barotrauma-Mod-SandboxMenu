namespace SandboxMenu.UI.ViewModels;

internal sealed class PropertyRow(
    EntryEditorViewModel editor,
    int index,
    PropertyOverride target,
    Action browseComponent,
    Action browseProperty,
    Action remove)
    : RowViewModel($"#{index}")
{
    private readonly EntryEditorViewModel _editor = editor;
    private readonly PropertyOverride _target = target;

    private string _component = target.ComponentName;
    private string _property = target.PropertyName;
    private string _value = target.Value;

    public string ComponentName
    {
        get => _component;
        set
        {
            if (!Set(ref _component, value ?? string.Empty)) { return; }
            _target.ComponentName = _component;
            _editor.NotifyEdited();
        }
    }

    public int ComponentIndex => _target.ComponentIndex;

    internal void SetComponent(OverrideTarget target)
    {
        ComponentName = target.ComponentName;
        _target.ComponentIndex = Math.Max(1, target.ComponentIndex);
    }

    internal void SetProperty(PropertyOption option)
    {
        PropertyName = option.Name;
        Value = option.DefaultValue;
    }

    public string PropertyName
    {
        get => _property;
        set
        {
            if (!Set(ref _property, value ?? string.Empty)) { return; }
            _target.PropertyName = _property;
            _editor.NotifyEdited();
        }
    }

    public string Value
    {
        get => _value;
        set
        {
            if (!Set(ref _value, value ?? string.Empty)) { return; }
            _target.Value = _value;
            _editor.NotifyEdited();
        }
    }

    public RelayCommand BrowseComponentCommand { get; } = new RelayCommand(browseComponent);

    public RelayCommand BrowsePropertyCommand { get; } = new RelayCommand(browseProperty);

    public RelayCommand DeleteCommand { get; } = new RelayCommand(remove);
}
