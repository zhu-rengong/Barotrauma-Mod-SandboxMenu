using System.Windows.Input;

namespace UiFramework.Controls;

[Element("Button")]
internal sealed class ButtonElement : ViewElement, ICommandElement
{
    private readonly GUIButton _button;
    private readonly ViewLoadContext _view;
    private float _scale;
    private ICommand? _command;

    private Alignment _textAlignment;

    public ButtonElement(ElementContext context)
        : this(context, Create(context))
    {
    }

    private ButtonElement(ElementContext context, GUIButton button) : base(button)
    {
        _view = context.View;
        _button = button;
        _scale = ViewMarkup.TextScaleOf(context.Text("FontSize"), button.TextBlock.Font);

        button.TextBlock.Padding = new Vector4(UiMetrics.Dip(MarkupPlacement.Read(context.Node, "Indent", 0f)), 0f, UiMetrics.Dip(UiMetrics.Pad), 0f);
        button.TextBlock.AutoScaleHorizontal = ViewMarkup.ToBool(context.Text("Scale"), true);
        button.TextBlock.TextScale = _scale;

        _textAlignment = ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.Center);
        button.TextBlock.TextAlignment = _textAlignment;

        // A click runs inside the game's GUI walk, so the command is dispatched through the view, which knows how
        // a failure is reported.
        button.OnClicked = (_, _) =>
        {
            _view.Dispatch(() => _command?.Execute(null));
            return false;
        };
    }

    [ElementProperty(KeyText = true)]
    public RichString Text { set => ApplyText(value); }

    [ElementProperty]
    public RichString Literal { set => ApplyText(value); }

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
        set
        {
            _textAlignment = ViewMarkup.AlignmentOf(value, Alignment.Center);
            _button.TextBlock.TextAlignment = _textAlignment;
        }
    }

    public void SetCommand(ICommand? command)
    {
        _command = command;
        RefreshCanExecute();
    }

    public void RefreshCanExecute() => _button.Enabled = _command?.CanExecute(null) ?? true;

    internal override void AddContent(ViewElement child) => throw new NotSupportedException("Button takes no content");

    private static GUIButton Create(ElementContext context)
        => new(
            context.Rect(context.Parent, 1f, UiMetrics.ControlHeight),
            string.Empty,
            ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.Center),
            context.Skin);

    private void ApplyText(RichString value)
    {
        // The text block, not GUIButton.Text: that property is a LocalizedString, and a rich string round
        // tripped through it is re-wrapped as plain — its colour tags would never be parsed.
        _button.TextBlock.Text = value;
        _button.TextBlock.TextScale = _scale;
    }
}
