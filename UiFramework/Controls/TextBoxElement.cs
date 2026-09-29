namespace UiFramework.Controls;

[Element("TextBox")]
internal sealed class TextBoxElement : ViewElement, IPropertyObserver, IDisposable
{
    private readonly GUITextBox _box;
    private Action<object?>? _changed;
    private GUITextBox.OnTextChangedHandler? _onTextChanged;

    public TextBoxElement(ElementContext context)
        : base(new GUITextBox(
            context.Rect(context.Parent, 1f, UiMetrics.ControlHeight),
            string.Empty,
            textColor: UiMetrics.Text,
            font: ViewMarkup.FontOf(context.Text("Font")),
            textAlignment: context.Text("TextAlign") is { } align ? ViewMarkup.AlignmentOf(align, Alignment.Left) : Alignment.Left,
            createClearButton: ViewMarkup.ToBool(context.Text("Clear"), false)))
    {
        _box = (GUITextBox)Control;
        _box.TextBlock.TextScale = UiMetrics.TextScale;

        if (ViewMarkup.ToBool(context.Text("Focus"), false)) { context.View.FocusOnOpen(Focus); }

        _box.OverflowClip = true;
    }

    [ElementProperty(Mode = BindingMode.TwoWay)]
    public string? Text { set => _box.Text = value ?? string.Empty; }

    internal void Focus()
    {
        _box.Select(ignoreSelectSound: true);
        _box.SelectAll();
    }

    internal override void AddContent(ViewElement child) => throw new NotSupportedException("TextBox takes no content");

    void IPropertyObserver.Observe(string property, Action<object?> changed)
    {
        if (!string.Equals(property, nameof(Text), StringComparison.OrdinalIgnoreCase)) { return; }

        _changed = changed;

        _onTextChanged = (box, text) =>
        {
            _changed?.Invoke(text);
            return true;
        };

        _box.OnTextChanged += _onTextChanged;
    }

    public void Dispose()
    {
        if (_onTextChanged is not { } handler) { return; }

        _box.OnTextChanged -= handler;
        _onTextChanged = null;
        _changed = null;
    }
}
