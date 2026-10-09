using Microsoft.Xna.Framework;

namespace UiFramework.Controls;

[Element("Text")]
internal sealed class TextElement : ViewElement
{
    private readonly GUITextBlock _block;
    private readonly GUIFont _font;

    private float _scale;

    public TextElement(ElementContext context)
        : this(context, CreateBlock(context))
    {
    }

    private TextElement(ElementContext context, GUITextBlock block) : base(block)
    {
        _block = block;
        _font = ViewMarkup.FontOf(context.Text("Font"));
        _scale = ViewMarkup.TextScaleOf(context.Text("FontSize"), _font);

        _block.Padding = context.Padding.IsEmpty
            ? new Vector4(UiMetrics.Dip(UiTokens.Dip("pad", 6f)), 0f, 0f, 0f)
            : context.Padding.ToVector4();

        _block.CanBeFocused = false;

        _block.HoverColor = Microsoft.Xna.Framework.Color.Transparent;
        _block.SelectedColor = Microsoft.Xna.Framework.Color.Transparent;

        if (ViewMarkup.ToBool(context.Text("Wrap"), false))
        {
            int wrappedWidth = -1;

            context.View.EveryFrame(() =>
            {
                int width = _block.Rect.Width;
                if (width == wrappedWidth) { return; }

                wrappedWidth = width;
                _block.SetTextPos();
            });
        }
    }

    [ElementProperty(KeyText = true)]
    public RichString Text { set => Apply(value); }

    [ElementProperty]
    public RichString Literal { set => Apply(value); }

    [ElementProperty]
    public float FontSize
    {
        set
        {
            _scale = UiMetrics.FontScale(value, _font);
            _block.TextScale = _scale;
        }
    }

    [ElementProperty]
    public string TextAlign { set => _block.TextAlignment = ViewMarkup.AlignmentOf(value, Alignment.CenterLeft); }

    [ElementProperty]
    public bool Scale { set => _block.AutoScaleHorizontal = value; }

    [ElementProperty]
    public bool Wrap { set => _block.Wrap = value; }

    [ElementProperty]
    public string Color { set => _block.TextColor = ViewMarkup.ColorOf(value, UiMetrics.Text); }

    private static GUITextBlock CreateBlock(ElementContext context)
        => new(
            context.Rect(context.Parent, 1f, UiTokens.Percent("control", 0.84f)),
            string.Empty,
            textColor: UiMetrics.Text,
            font: ViewMarkup.FontOf(context.Text("Font")),
            textAlignment: ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.CenterLeft));

    private void Apply(RichString value)
    {
        _block.Text = value;
        _block.TextScale = _scale;
    }
}
