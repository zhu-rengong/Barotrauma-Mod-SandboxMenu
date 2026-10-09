using System.Windows.Input;

namespace UiFramework.Controls;

[Element("Button")]
internal sealed class ButtonElement : ViewElement, ICommandElement, IDisposable
{
    private readonly GUIButton _button;
    private readonly ViewContext _view;
    private readonly string? _hintKey;
    private RichString _text = string.Empty;
    private RichString? _shortcut;
    private RichString? _toolTip;
    private float _scale;
    private bool _scalable;
    private float _indent;
    private Alignment _alignment = Alignment.Center;
    private ICommand? _command;

    public ButtonElement(ElementContext context)
        : this(context, Create(context))
    {
    }

    private ButtonElement(ElementContext context, GUIButton button) : base(button)
    {
        _view = context.View;
        _button = button;

        _hintKey = context.Skin is "" ? ViewMarkup.DarkHintKey : ViewMarkup.HintKey;
        _scale = ViewMarkup.TextScaleOf(context.Text("FontSize"), button.TextBlock.Font);

        _indent = context.Metric("Indent", 0f);
        _alignment = ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.Center);

        button.TextBlock.Padding = new Vector4(UiMetrics.Dip(_indent), 0f, UiMetrics.Dip(UiTokens.Dip("pad", 6f)), 0f);
        button.TextBlock.TextScale = _scale;
        SetScale(ViewMarkup.ToBool(context.Text("Scale"), true));

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
    public bool CloseView
    {
        set
        {
            if (value && Context is { } view) { SetCommand(view.CloseCommand); }
        }
    }

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
    public RichString? ToolTip
    {
        set
        {
            _toolTip = value;
            ApplyToolTip();
        }
    }

    [ElementProperty]
    public bool Scale { set => SetScale(value); }

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
        set
        {
            _alignment = ViewMarkup.AlignmentOf(value, Alignment.Center);
            ApplyAlignment();
        }
    }

    public void SetCommand(ICommand? command)
    {
        _command = command;
        RefreshCanExecute();
    }

    public void RefreshCanExecute() => _button.Enabled = _command?.CanExecute(null) ?? true;

    public override void AddContent(ViewElement child) => throw new NotSupportedException("Button takes no content");

    public void Dispose()
    {
        _button.OnClicked = null;
        _button.OnSecondaryClicked = null;
        _command = null;
    }

    private static GUIButton Create(ElementContext context)
        => new(
            context.Rect(context.Parent, 1f, UiTokens.Percent("control", 0.84f)),
            string.Empty,
            ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.Center),
            context.Skin);

    private void ApplyText()
    {
        _button.TextBlock.Text = ViewMarkup.WithShortcut(_text, _shortcut, _hintKey);
        _button.TextBlock.TextScale = _scale;
        ApplyToolTip();
    }

    private void SetScale(bool scalable)
    {
        _scalable = scalable;
        _button.TextBlock.AutoScaleHorizontal = scalable;
        _button.TextBlock.OverflowClip = !scalable;
        ApplyAlignment();
        ApplyToolTip();
    }

    private void ApplyAlignment()
    {
        float pad = UiTokens.Dip("pad", 6f);
        float indent = _indent > 0f ? _indent : 0f;

        _button.TextBlock.TextAlignment = _alignment;
        _button.TextBlock.Padding = new Vector4(UiMetrics.Dip(indent + pad), 0f, UiMetrics.Dip(pad), 0f);
    }

    private void ApplyToolTip()
    {
        if (_toolTip is null && _scalable) { return; }

        _button.ToolTip = _toolTip ?? _text;
    }
}
