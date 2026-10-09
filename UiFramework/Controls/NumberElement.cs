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
            context.Rect(context.Parent, 1f, UiTokens.Percent("control", 0.84f)),
            integer ? NumberType.Int : NumberType.Float,
            textAlignment: ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.CenterLeft)))
    {
        _input = (GUINumberInput)Control;
        _integer = integer;

        if (context.Node.Value("Step")?.Extension is null) { _input.ValueStep = context.Metric("Step", 1f); }

        _input.OnValueChanged = _ => _changed?.Invoke(_integer ? _input.IntValue : _input.FloatValue);
    }

    [ElementProperty(Mode = BindingMode.TwoWay)]
    public float Value
    {
        set
        {
            if (_integer) { _input.IntValue = ToInt(value); }
            else { _input.FloatValue = value; }
        }
    }

    [ElementProperty]
    public float Step
    {
        set
        {
            if (Math.Abs(_input.ValueStep - value) < 0.0001f) { return; }

            _input.ValueStep = value;
        }
    }

    [ElementProperty]
    public int Decimals
    {
        set
        {
            if (_input.DecimalsToDisplay == value) { return; }

            _input.DecimalsToDisplay = value;
        }
    }

    [ElementProperty]
    public float Min
    {
        set
        {
            if (_integer)
            {
                int minimum = ToInt(value);
                if (_input.MinValueInt == minimum) { return; }

                _input.MinValueInt = minimum;
                return;
            }

            if (_input.MinValueFloat == value) { return; }

            _input.MinValueFloat = value;
        }
    }

    [ElementProperty]
    public float Max
    {
        set
        {
            if (_integer)
            {
                int maximum = ToInt(value);
                if (_input.MaxValueInt == maximum) { return; }

                _input.MaxValueInt = maximum;
                return;
            }

            if (_input.MaxValueFloat == value) { return; }

            _input.MaxValueFloat = value;
        }
    }

    public override void AddContent(ViewElement child) => throw new NotSupportedException("Number takes no content");

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

    private static int ToInt(float value)
        => value >= int.MaxValue ? int.MaxValue
            : value <= int.MinValue ? int.MinValue
            : (int)MathF.Round(value);
}
