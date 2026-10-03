namespace UiFramework.Controls;

[Element("TextBox")]
internal sealed class TextBoxElement : ViewElement, IPropertyObserver, IDisposable
{
    private readonly GUITextBox _box;
    private readonly ViewContext _view;
    private Action<object?>? _changed;
    private Action<object?>? _focusChanged;
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
        _view = context.View;
        _box = (GUITextBox)Control;
        _box.TextBlock.TextScale = UiMetrics.TextScale;

        if (ViewMarkup.ToBool(context.Text("Focus"), false)) { context.View.FocusOnOpen(FocusBox); }

        _box.OverflowClip = true;
    }

    [ElementProperty(Mode = BindingMode.TwoWay)]
    public string? Text { set => _box.Text = value ?? string.Empty; }

    // Setting this asks for the keyboard, taken once the click that asked for it is over (or the box would lose it
    // again in the same frame); the request is then answered with false so a re-pointed binding cannot take the caret
    // away from someone who is typing.
    [ElementProperty(Mode = BindingMode.TwoWay)]
    public bool Focus
    {
        set
        {
            if (value) { _view.FocusAfterClick(FocusBox); }
        }
    }

    private void FocusBox()
    {
        if (ReferenceEquals(GUI.KeyboardDispatcher.Subscriber, _box)) { return; }

        _box.Select(ignoreSelectSound: true);
        _box.SelectAll();

        _focusChanged?.Invoke(false);
    }

    internal override void AddContent(ViewElement child) => throw new NotSupportedException("TextBox takes no content");

    void IPropertyObserver.Observe(string property, Action<object?> changed)
    {
        if (string.Equals(property, nameof(Focus), StringComparison.OrdinalIgnoreCase))
        {
            _focusChanged = changed;
            return;
        }

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
        _focusChanged = null;

        if (_onTextChanged is not { } handler) { return; }

        _box.OnTextChanged -= handler;
        _onTextChanged = null;
        _changed = null;
    }
}
