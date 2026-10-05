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

        // No skin means the host's plain button, which is drawn light: its key hint has to be darker to be read.
        // Both templates are the mod's, handed over at startup.
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

    // Closes the view this button sits in: what every window's close button declares instead of being wired up by
    // name once the tree is built.
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

    // A button that may not shrink its text is the one whose text may not fit: what does not fit is clipped, and the
    // whole of it is shown on hover instead.
    private void SetScale(bool scalable)
    {
        _scalable = scalable;
        _button.TextBlock.AutoScaleHorizontal = scalable;
        _button.TextBlock.OverflowClip = !scalable;
        ApplyAlignment();
        ApplyToolTip();
    }

    // A button that may not shrink its text keeps it against its left edge: the host centres such a text on the whole
    // rect, so it would run over the button on both sides before a clip could take it.
    private void ApplyAlignment()
    {
        GUITextBlock block = _button.TextBlock;
        block.TextAlignment = _scalable ? _alignment : Alignment.Left;

        if (_scalable) { return; }

        float pad = UiTokens.Dip("pad", 6f);
        block.Padding = new Vector4(UiMetrics.Dip(_indent > 0f ? _indent : pad), 0f, UiMetrics.Dip(pad), 0f);
    }

    private void ApplyToolTip()
    {
        if (_toolTip is null && _scalable) { return; }

        _button.ToolTip = _toolTip ?? _text;
    }
}
