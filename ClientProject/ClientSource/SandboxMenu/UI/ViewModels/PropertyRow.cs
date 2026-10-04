using System.Globalization;

namespace SandboxMenu.UI.ViewModels;

// One property override: the two pickers that name it and the editor its value's type asks for. Every editor reads
// and writes the string the override stores, so presets, the spawn path and the network payload keep their format.
internal sealed class PropertyRow(
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

    // A name whose metadata cannot be read (the item or the component is not known right now) is shown by the stored
    // name itself, so the row never claims to name nothing while it holds one.
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

    // The host's number input reads a null range as "no limit", and hides the step buttons of an unbounded float, so
    // "no bound" is handed over as the sentinel of the family the value belongs to.
    public float Min => _property?.Range?.Min ?? (Integral ? int.MinValue : float.MinValue);

    public float Max => _property?.Range?.Max ?? (Integral ? int.MaxValue : float.MaxValue);

    public float Step => _property?.Range?.Step ?? 1f;

    public int Decimals => _property?.Range?.Decimals ?? 3;

    public string Text
    {
        get => _target.Value;
        set => SetOwned(PropertyKind.Text, value ?? string.Empty);
    }

    // A float is written as the value is, not padded out to a fixed number of decimals: the box is a plain text one
    // and what it holds goes through here. Text that does not read as a number is left where it is, so a half-typed
    // "51." is not thrown away under the caret.
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

    // What the box shows once an edit is over is the value that stands: a number is written the way it is held, and
    // text that was not taken gives way to the value it left behind.
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
        // The same property name on the new component, but only as the kind the row was editing: a value typed for one
        // editor means nothing to another.
        PropertyOption? kept = property is { } declared && declared.Kind == Kind ? declared : null;

        _componentLabel = target.Label;
        _property = kept;
        _target.ComponentName = target.ComponentName;
        _target.ComponentIndex = Math.Max(1, target.ComponentIndex);

        // A property the new component does not declare (or declares as something else) is dropped with the old
        // component: the row is back to picking one, and an override that names nothing is the one a preset leaves out.
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

        // Taking every flag away leaves nothing for the host to parse, so the value goes back to zero: a numeric name
        // Enum.Parse takes, or the enum's own "None" when it declares one.
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

    // An editor only writes the value its own type owns: the editors of the other types are bound all the same, and
    // would otherwise push their own reading of the value back over the one that was just edited.
    private void SetOwned(PropertyKind kind, string value)
    {
        if (Kind != kind) { return; }

        SetValue(value);
    }

    // Either component may be left out: the box that did not change hands its own reading over untouched.
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

    // One raise is what the row needs: a binding re-reads its own path when a source it is hooked to reports a change,
    // no matter which property the report names.
    private void Refresh() => Raise();
}
