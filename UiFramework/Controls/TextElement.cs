using Microsoft.Xna.Framework;

namespace UiFramework.Controls;

[Element("Text")]
internal sealed class TextElement : ViewElement
{
    private readonly GUITextBlock _block;
    private readonly GUIFont _font;
    private readonly ViewContext _view;

    private float _scale;
    private string? _color;
    private RichString _applied = string.Empty;
    private bool _armed;

    public TextElement(ElementContext context)
        : this(context, CreateBlock(context))
    {
    }

    private TextElement(ElementContext context, GUITextBlock block) : base(block)
    {
        _block = block;
        _font = ViewMarkup.FontOf(context.Text("Font"));
        _scale = ViewMarkup.TextScaleOf(context.Text("FontSize"), _font);
        _view = context.View;

        _block.Padding = context.Padding.IsEmpty
            ? new Vector4(UiMetrics.Dip(UiTokens.Dip("pad", 6f)), 0f, 0f, 0f)
            : context.Padding.ToVector4();

        _block.CanBeFocused = false;

        _block.HoverColor = Microsoft.Xna.Framework.Color.Transparent;
        _block.SelectedColor = Microsoft.Xna.Framework.Color.Transparent;
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
    public bool Wrap
    {
        set
        {
            if (_block.Wrap == value) { return; }

            _block.Wrap = value;

            if (!value) { return; }

            _block.SetTextPos();
            ArmWrap();
        }
    }

    [ElementProperty]
    public bool Clip { set => _block.OverflowClip = value; }

    [ElementProperty]
    public new string Color
    {
        get => _color ?? "Bright";
        set
        {
            _color = value;
            _block.TextColor = ViewMarkup.ColorOf(value, UiMetrics.Text);
        }
    }

    [ElementProperty(Phase = ApplyPhase.Construct)]
    internal string? Font => Node?.Text("Font");

    private static GUITextBlock CreateBlock(ElementContext context)
        => new(
            context.Rect(context.Parent, 1f, UiTokens.Percent("control", 0.84f)),
            string.Empty,
            textColor: UiMetrics.Text,
            font: ViewMarkup.FontOf(context.Text("Font")),
            textAlignment: ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.CenterLeft));

    private void Apply(RichString value)
    {
        bool changed = value != _applied;

        _block.Text = value;
        _block.TextScale = _scale;

        _applied = value;

        if (changed) { RaiseEvent("TextChanged"); }
    }

    private void ArmWrap()
    {
        if (_armed) { return; }

        _armed = true;
        _block.RectTransform.SizeChanged += Rewrap;

        _view.EveryFrame(() =>
        {
            if (_block.Rect.Width <= 0) { return false; }

            _block.SetTextPos();

            return true;
        });
    }

    private void Rewrap()
    {
        if (!_block.Wrap || _block.Rect.Width <= 0) { return; }

        _block.SetTextPos();
    }
}
