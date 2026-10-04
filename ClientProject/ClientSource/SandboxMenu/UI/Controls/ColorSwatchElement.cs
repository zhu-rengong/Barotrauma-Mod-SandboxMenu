using System.Windows.Input;

namespace SandboxMenu.UI.Controls;

[Element("ColorSwatch")]
internal sealed class ColorSwatchElement : ViewElement, IDisposable
{
    private readonly SwatchButton _button;
    private readonly ViewContext _view;

    private ICommand? _command;

    public ColorSwatchElement(ElementContext context)
        : base(new SwatchButton(context.Rect(context.Parent, 1f, UiMetrics.ControlHeight), string.Empty))
    {
        _view = context.View;
        _button = (SwatchButton)Control;
        _button.Padding = context.Metric("Padding", UiMetrics.Pad);

        _button.OnClicked = (_, _) =>
        {
            _view.Dispatch(() => _command?.Execute(null));
            return false;
        };
    }

    [ElementProperty]
    public Color Color
    {
        set
        {
            _button.Swatch = value;
            Labels.Apply(_button.TextBlock, SwatchButton.Contrast(value));
        }
    }

    [ElementProperty]
    public RichString Label
    {
        set
        {
            _button.TextBlock.Text = value;
            _button.TextBlock.TextScale = UiMetrics.TextScale;
        }
    }

    [ElementProperty]
    public ICommand? Command { set => _command = value; }

    public void Dispose()
    {
        _button.OnClicked = null;
        _command = null;
    }
}
