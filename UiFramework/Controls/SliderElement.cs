namespace UiFramework.Controls;

[Element("Slider")]
internal sealed class SliderElement : ViewElement, IPropertyObserver, IDisposable
{
    private const float HandleWidth = 0.08f;

    private readonly GUIScrollBar _bar;

    private Action<object?>? _changed;
    private float _value;
    private float _min;
    private float _max = 100f;
    private float _step;
    private bool _applying;

    public SliderElement(ElementContext context)
        : base(new GUIScrollBar(
            context.Rect(context.Parent, 1f, UiTokens.Percent("control", 0.84f)),
            HandleWidth,
            color: null,
            style: "GUISlider",
            isHorizontal: true))
    {
        _bar = (GUIScrollBar)Control;
        _bar.BarSize = HandleWidth;
        _bar.OnMoved = OnMoved;
        _bar.OnReleased = OnMoved;

        Sync();
    }

    [ElementProperty(Mode = BindingMode.TwoWay)]
    public float Value
    {
        set
        {
            _value = value;
            Push();
        }
    }

    [ElementProperty]
    public float Min
    {
        set
        {
            _min = value;
            Sync();
        }
    }

    [ElementProperty]
    public float Max
    {
        set
        {
            _max = value;
            Sync();
        }
    }

    [ElementProperty]
    public float Step
    {
        set
        {
            _step = Math.Max(0f, value);
            Sync();
        }
    }

    void IPropertyObserver.Observe(string property, Action<object?> changed)
    {
        if (string.Equals(property, nameof(Value), StringComparison.OrdinalIgnoreCase)) { _changed = changed; }
    }

    public void Dispose()
    {
        _bar.OnMoved = null;
        _bar.OnReleased = null;
        _changed = null;
    }

    private void Sync()
    {
        _bar.Range = new Vector2(_min, _max > _min ? _max : _min + 1f);
        _bar.StepValue = _step;
        Push();
    }

    private void Push()
    {
        if (_applying) { return; }

        _bar.BarScrollValue = MathHelper.Clamp(_value, _min, _max);
    }

    private bool OnMoved(GUIScrollBar bar, float scroll)
    {
        _applying = true;
        bar.BarScroll = scroll;

        _value = MathHelper.Clamp(bar.BarScrollValue, _min, _max);

        _applying = false;

        _changed?.Invoke(_value);

        return true;
    }
}
