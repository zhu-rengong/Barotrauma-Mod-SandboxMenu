namespace UiFramework.Controls;

[Element("Tick")]
internal sealed class TickElement : ViewElement, IPropertyObserver
{
    private readonly GUITickBox _tick;
    private Action<object?>? _changed;

    public TickElement(ElementContext context)
        : base(new GUITickBox(
            context.Rect(context.Parent, 1f, UiMetrics.ControlHeight),
            string.Empty,
            ViewMarkup.FontOf(context.Text("Font"))))
    {
        _tick = (GUITickBox)Control;
        _tick.TextBlock.TextScale = UiMetrics.TextScale;
        _tick.TextBlock.Padding = new Vector4(UiMetrics.Dip(4f), 0f, 0f, 0f);

        _tick.OnSelected = tickBox =>
        {
            _changed?.Invoke(tickBox.Selected);
            return true;
        };
    }

    [ElementProperty(KeyText = true)]
    public RichString Text { set => _tick.TextBlock.Text = value; }

    [ElementProperty(Mode = BindingMode.TwoWay)]
    public bool Selected { set => _tick.Selected = value; }

    internal override void AddContent(ViewElement child) => throw new NotSupportedException("Tick takes no content");

    void IPropertyObserver.Observe(string property, Action<object?> changed)
    {
        // The box reports one thing only. A registration for another property must not take its place, or the
        // tick would travel to the wrong path and be dropped there.
        if (string.Equals(property, nameof(Selected), StringComparison.OrdinalIgnoreCase)) { _changed = changed; }
    }
}
