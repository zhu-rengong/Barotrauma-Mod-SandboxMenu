using System.Globalization;

namespace SandboxMenu.UI.ViewModels;

internal sealed class PropertyRowViewModel(
    EntryEditorViewModel editor,
    int index,
    PropertyOverride target,
    LocalizedString componentLabel,
    PropertyOption? property,
    Action browseComponent,
    Action browseProperty,
    Action browseEnum,
    Action pickColor,
    Action remove)
    : RowViewModel($"#{index}")
{
    private readonly EntryEditorViewModel _editor = editor;
    private readonly PropertyOverride _target = target;

    private LocalizedString _componentLabel = componentLabel;
    private PropertyOption? _property = property;

    public LocalizedString ComponentLabel => _componentLabel;

    public string ComponentName => _target.ComponentName;

    public int ComponentIndex => _target.ComponentIndex;

    public string PropertyName => _target.PropertyName;

    internal PropertyOption? Descriptor => _property;

    public RichString PropertyLabel => _property switch
    {
        { } declared => RichString.Rich($"{declared.Name} ({declared.TypeName})"),
        _ when _target.IsNamed => RichString.Rich(PropertyName),
        _ => RichString.Rich(TextManager.Get("sandboxmenu.browse.property"))
    };

    public PropertyKind Kind => _property?.Kind ?? PropertyKind.Text;

    public bool IsFloat => Kind == PropertyKind.Float;

    public bool IsInt => Kind == PropertyKind.Int;

    public bool IsText => Kind == PropertyKind.Text;

    public bool IsBool => Kind == PropertyKind.Bool;

    public bool IsVector => Kind == PropertyKind.Vector2;

    public bool IsPoint => Kind == PropertyKind.Point;

    public bool IsColor => Kind == PropertyKind.Color;

    public bool IsEnum => Kind is PropertyKind.Enum or PropertyKind.Flags;

    public float Min => _property?.Range?.Min ?? (Integral ? int.MinValue : float.MinValue);

    public float Max => _property?.Range?.Max ?? (Integral ? int.MaxValue : float.MaxValue);

    public float Step => _property?.Range?.Step ?? 1f;

    public int Decimals => _property?.Range?.Decimals ?? 3;

    public string Text
    {
        get => _target.Value;
        set => SetOwned(PropertyKind.Text, value ?? string.Empty);
    }

    public string FloatText
    {
        get => _target.Value;
        set
        {
            if (Kind != PropertyKind.Float || value is null) { return; }
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)) { return; }

            SetValue(parsed.ToString("G", CultureInfo.InvariantCulture));
        }
    }

    public float IntValue
    {
        get => MathF.Round(ReadFloat());
        set => SetOwned(PropertyKind.Int, MathF.Round(value).ToString(CultureInfo.InvariantCulture));
    }

    public bool Checked
    {
        get => bool.TryParse(_target.Value, out bool ticked) && ticked;
        set => SetOwned(PropertyKind.Bool, value ? "True" : "False");
    }

    public Action CommitEdit => Refresh;

    public string VectorXText
    {
        get => ReadVector().X.ToString("G", CultureInfo.InvariantCulture);
        set => SetVectorText(x: value, y: null);
    }

    public string VectorYText
    {
        get => ReadVector().Y.ToString("G", CultureInfo.InvariantCulture);
        set => SetVectorText(x: null, y: value);
    }

    public float PointX
    {
        get => ReadPoint().X;
        set => SetOwned(PropertyKind.Point, XMLExtensions.PointToString(new Point((int)MathF.Round(value), ReadPoint().Y)));
    }

    public float PointY
    {
        get => ReadPoint().Y;
        set => SetOwned(PropertyKind.Point, XMLExtensions.PointToString(new Point(ReadPoint().X, (int)MathF.Round(value))));
    }

    public Color Swatch
    {
        get => XMLExtensions.ParseColor(_target.Value, false);
        set => SetOwned(PropertyKind.Color, value.ToStringHex());
    }

    public string ColorLabel
    {
        get
        {
            Color color = Swatch;
            return $"{color.R}, {color.G}, {color.B}, {color.A}";
        }
    }

    public RichString EnumLabel => string.IsNullOrEmpty(_target.Value)
        ? RichString.Rich(TextManager.Get("sandboxmenu.summary.unset"))
        : RichString.Rich(_target.Value);

    public RelayCommand BrowseComponentCommand { get; } = new RelayCommand(browseComponent);

    public RelayCommand BrowsePropertyCommand { get; } = new RelayCommand(browseProperty);

    public RelayCommand BrowseEnumCommand { get; } = new RelayCommand(browseEnum);

    public RelayCommand PickColorCommand { get; } = new RelayCommand(pickColor);

    public RelayCommand DeleteCommand { get; } = new RelayCommand(remove);

    internal void SetComponent(OverrideTarget target, PropertyOption? property)
    {
        PropertyOption? kept = property is { } declared && declared.Kind == Kind ? declared : null;

        _componentLabel = target.Label;
        _property = kept;
        _target.ComponentName = target.ComponentName;
        _target.ComponentIndex = Math.Max(1, target.ComponentIndex);

        if (kept is null) { _target.PropertyName = string.Empty; }

        _editor.NotifyEdited();
        Refresh();
    }

    internal void SetProperty(PropertyOption option)
    {
        _property = option;
        _target.PropertyName = option.Name;
        _target.Value = option.DefaultValue;

        _editor.NotifyEdited();
        Refresh();
    }

    internal void SetEnumValue(string value)
    {
        if (Kind != PropertyKind.Enum) { return; }

        SetValue(value ?? string.Empty);
    }

    internal bool HasEnumFlag(string member)
        => ValueMembers().Any(part => string.Equals(part, member, StringComparison.OrdinalIgnoreCase));

    internal void SetEnumFlag(string member, bool on)
    {
        if (Kind != PropertyKind.Flags) { return; }

        List<string> members = [.. ValueMembers().Where(part => !string.Equals(part, member, StringComparison.OrdinalIgnoreCase))];

        if (on) { members.Add(member); }

        SetValue(members.Count > 0
            ? string.Join(", ", members)
            : _property?.Values.Any(value => string.Equals(value, "None", StringComparison.OrdinalIgnoreCase)) == true ? "None" : "0");
    }

    private IEnumerable<string> ValueMembers()
        => _target.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private bool Integral => Kind is PropertyKind.Int or PropertyKind.Point;

    private float ReadFloat()
        => float.TryParse(_target.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;

    private Vector2 ReadVector() => XMLExtensions.ParseVector2(_target.Value, false);

    private Point ReadPoint() => XMLExtensions.ParsePoint(_target.Value, false);

    private void SetOwned(PropertyKind kind, string value)
    {
        if (Kind != kind) { return; }

        SetValue(value);
    }

    private void SetVectorText(string? x, string? y)
    {
        if (Kind != PropertyKind.Vector2) { return; }

        Vector2 current = ReadVector();
        if (!TryReadComponent(x, current.X, out float newX)) { return; }
        if (!TryReadComponent(y, current.Y, out float newY)) { return; }

        SetValue(XMLExtensions.Vector2ToString(new Vector2(newX, newY)));
    }

    private static bool TryReadComponent(string? text, float fallback, out float value)
    {
        if (text is null) { value = fallback; return true; }

        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void SetValue(string value)
    {
        if (string.Equals(_target.Value, value, StringComparison.Ordinal)) { return; }

        _target.Value = value;
        _editor.NotifyEdited();
        Refresh();
    }

    private void Refresh() => Raise();
}
