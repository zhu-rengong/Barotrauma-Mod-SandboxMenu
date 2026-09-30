using System.Windows.Input;

namespace UiFramework.Controls;

[Element("Button")]
internal sealed class ButtonElement : ViewElement, ICommandElement, IDisposable
{
    private readonly GUIButton _button;
    private readonly ViewLoadContext _view;
    private readonly string _hintKey;
    private RichString _text = string.Empty;
    private RichString? _shortcut;
    private float _scale;
    private ICommand? _command;

    public ButtonElement(ElementContext context)
        : this(context, Create(context))
    {
    }

    private ButtonElement(ElementContext context, GUIButton button) : base(button)
    {
        _view = context.View;
        _button = button;

        // No skin means the host's plain button, which is drawn light: its key hint has to be darker to be read.
        _hintKey = context.Skin is "" ? "sandboxmenu.shortcut.hint.dark" : "sandboxmenu.shortcut.hint";
        _scale = ViewMarkup.TextScaleOf(context.Text("FontSize"), button.TextBlock.Font);

        button.TextBlock.Padding = new Vector4(UiMetrics.Dip(context.Metric("Indent", 0f)), 0f, UiMetrics.Dip(UiMetrics.Pad), 0f);
        button.TextBlock.AutoScaleHorizontal = ViewMarkup.ToBool(context.Text("Scale"), true);
        button.TextBlock.TextScale = _scale;
        button.TextBlock.TextAlignment = ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.Center);

        button.OnClicked = (_, _) =>
        {
            _view.Dispatch(() => _command?.Execute(null));
            return false;
        };
    }

    [ElementProperty(KeyText = true)]
    public RichString Text
    {
        set
        {
            _text = value;
            ApplyText();
        }
    }

    [ElementProperty]
    public RichString Literal
    {
        set
        {
            _text = value;
            ApplyText();
        }
    }

    [ElementProperty(KeyText = true)]
    public RichString? Shortcut
    {
        set
        {
            _shortcut = value;
            ApplyText();
        }
    }

    [ElementProperty]
    public ICommand? Command { set => SetCommand(value); }

    [ElementProperty]
    public ICommand? SecondaryCommand
    {
        set => _button.OnSecondaryClicked = (_, _) =>
        {
            _view.Dispatch(() => value?.Execute(null));
            return false;
        };
    }

    [ElementProperty]
    public float FontSize
    {
        set
        {
            _scale = UiMetrics.FontScale(value, _button.TextBlock.Font);
            _button.TextBlock.TextScale = _scale;
        }
    }

    [ElementProperty]
    public string TextAlign
    {
        set => _button.TextBlock.TextAlignment = ViewMarkup.AlignmentOf(value, Alignment.Center);
    }

    public void SetCommand(ICommand? command)
    {
        _command = command;
        RefreshCanExecute();
    }

    public void RefreshCanExecute() => _button.Enabled = _command?.CanExecute(null) ?? true;

    internal override void AddContent(ViewElement child) => throw new NotSupportedException("Button takes no content");

    public void Dispose()
    {
        _button.OnClicked = null;
        _button.OnSecondaryClicked = null;
        _command = null;
    }

    private static GUIButton Create(ElementContext context)
        => new(
            context.Rect(context.Parent, 1f, UiMetrics.ControlHeight),
            string.Empty,
            ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.Center),
            context.Skin);

    private void ApplyText()
    {
        _button.TextBlock.Text = ViewMarkup.WithShortcut(_text, _shortcut, _hintKey);
        _button.TextBlock.TextScale = _scale;
    }
}
