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

        _block.Padding = new Vector4(UiMetrics.Dip(MarkupPlacement.Read(context.Node, "Padding", UiMetrics.Pad)), 0f, 0f, 0f);
        _block.CanBeFocused = false;

        // A wrapped label can only wrap against a width, and it gets one only once the layout has run: the engine
        // wraps when the text changes, not when the rectangle does, so the width is watched until it settles (and
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
            context.Rect(context.Parent, 1f, UiMetrics.ControlHeight),
            string.Empty,
            textColor: UiMetrics.Text,
            font: ViewMarkup.FontOf(context.Text("Font")),
            // Not "Align": that one is the rectangle's anchor, and a row of a list is placed by the list adding
            // an offset to it — an anchor other than the top left corner moves the row off the slot the list
            textAlignment: ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.CenterLeft));

    private void Apply(RichString value)
    {
        _block.Text = value;
        _block.TextScale = _scale;
    }
}
