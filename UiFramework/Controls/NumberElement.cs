namespace UiFramework.Controls;

[Element("Number")]
internal sealed class NumberElement : ViewElement, IPropertyObserver
{
    private readonly GUINumberInput _input;
    private readonly bool _integer;
    private Action<object?>? _changed;

    public NumberElement(ElementContext context)
        : base(new GUINumberInput(
            context.Rect(context.Parent, 1f, UiMetrics.ControlHeight),
            IsInteger(context) ? NumberType.Int : NumberType.Float,
            textAlignment: ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.CenterLeft)))
    {
        _input = (GUINumberInput)Control;
        _integer = IsInteger(context);

        // Left to itself the engine takes the step of the +/- buttons from the range — one percent of it, which
        // for a limit of a hundred thousand is a thousand a click. What these inputs hold are counts, so one it
        _input.ValueStep = MarkupPlacement.Read(context.Node, "Step", 1f);

        // An integer input keeps its value in a field of its own and only that field is shown and reported: read
        // and write the one the input was built with, or the field that never moves is what travels both ways.
        _input.OnValueChanged = _ => _changed?.Invoke(_integer ? _input.IntValue : _input.FloatValue);
    }

    [ElementProperty(Mode = BindingMode.TwoWay)]
    public float Value
    {
        set
        {
            if (_integer) { _input.IntValue = (int)MathF.Round(value); }
            else { _input.FloatValue = value; }
        }
    }

    [ElementProperty]
    public float Step { set => _input.ValueStep = value; }

    [ElementProperty]
    public float Min
    {
        set
        {
            if (_integer) { _input.MinValueInt = (int)value; }
            else { _input.MinValueFloat = value; }
        }
    }

    [ElementProperty]
    public float Max
    {
        set
        {
            if (_integer) { _input.MaxValueInt = (int)value; }
            else { _input.MaxValueFloat = value; }
        }
    }

    internal override void AddContent(ViewElement child) => throw new NotSupportedException("Number takes no content");

    void IPropertyObserver.Observe(string property, Action<object?> changed)
    {
        if (string.Equals(property, nameof(Value), StringComparison.OrdinalIgnoreCase)) { _changed = changed; }
    }

    private static bool IsInteger(ElementContext context) => ViewMarkup.ToBool(context.Text("Integer"), false);
}
