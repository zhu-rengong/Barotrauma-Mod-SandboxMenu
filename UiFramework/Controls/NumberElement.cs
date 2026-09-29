namespace UiFramework.Controls;

[Element("Number")]
internal sealed class NumberElement : ViewElement, IPropertyObserver, IDisposable
{
    private readonly GUINumberInput _input;
    private readonly bool _integer;
    private Action<object?>? _changed;

    public NumberElement(ElementContext context)
        : this(context, IsInteger(context))
    {
    }

    private NumberElement(ElementContext context, bool integer)
        : base(new GUINumberInput(
            context.Rect(context.Parent, 1f, UiMetrics.ControlHeight),
            integer ? NumberType.Int : NumberType.Float,
            textAlignment: ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.CenterLeft)))
    {
        _input = (GUINumberInput)Control;
        _integer = integer;

        _input.ValueStep = context.Metric("Step", 1f);

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

    public void Dispose()
    {
        _input.OnValueChanged = null;
        _changed = null;
    }

    private static bool IsInteger(ElementContext context) => ViewMarkup.ToBool(context.Text("Integer"), false);
}
