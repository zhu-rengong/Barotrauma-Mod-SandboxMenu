using Microsoft.Xna.Framework.Input;

namespace UiFramework.Controls;

[Element("TextBox")]
internal sealed class TextBoxElement : ViewElement, IPropertyObserver, IDisposable
{
    private readonly GUITextBox _box;
    private readonly ViewContext _view;
    private Action<object?>? _changed;
    private Action<object?>? _focusChanged;
    private GUITextBox.OnTextChangedHandler? _onTextChanged;
    private Action? _committed;
    private bool _commitWired;

    public TextBoxElement(ElementContext context)
        : base(new GUITextBox(
            context.Rect(context.Parent, 1f, UiTokens.Percent("control", 0.84f)),
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

    // Asked for when the box is done with an edit: the source is given the chance to hand back what it actually holds,
    // so text that was not taken (or was taken in another spelling) is replaced by the value that stands.
    [ElementProperty]
    public Action? Committed
    {
        set
        {
            _committed = value;

            if (_commitWired || value is null) { return; }

            _commitWired = true;
            _box.OnEnterPressed = OnBoxEnterPressed;
            _box.OnDeselected += OnBoxDeselected;
        }
    }

    private bool OnBoxEnterPressed(GUITextBox box, string text)
    {
        _committed?.Invoke();
        return true;
    }

    private void OnBoxDeselected(GUITextBox box, Keys key) => _committed?.Invoke();

    private void FocusBox()
    {
        if (ReferenceEquals(GUI.KeyboardDispatcher.Subscriber, _box)) { return; }

        _box.Select(ignoreSelectSound: true);
        _box.SelectAll();

        _focusChanged?.Invoke(false);
    }

    public override void AddContent(ViewElement child) => throw new NotSupportedException("TextBox takes no content");

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
        _committed = null;

        if (_commitWired)
        {
            _commitWired = false;
            _box.OnEnterPressed = null;
            _box.OnDeselected -= OnBoxDeselected;
        }

        if (_onTextChanged is { } handler)
        {
            _box.OnTextChanged -= handler;
            _onTextChanged = null;
        }

        _changed = null;
    }
}
