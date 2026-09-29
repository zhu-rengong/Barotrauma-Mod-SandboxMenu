namespace UiFramework.Controls;

[Element("Field")]
internal sealed class FieldElement : ViewElement
{
    private readonly GUITextBlock _label;
    private readonly GUILayoutGroup _controls;

    public FieldElement(ElementContext context)
        : base(new GUILayoutGroup(context.Rect(context.Parent, 1f, UiMetrics.RowHeight), isHorizontal: true, Anchor.CenterLeft)
        {
            CanBeFocused = false,
            HoverCursor = CursorState.Default
        })
    {
        float labelWidth = context.Size("LabelWidth", UiMetrics.LabelWidth);
        float controlWidth = context.Size("ControlWidth", 1f - labelWidth);

        _label = new GUITextBlock(
            new RectTransform(new Vector2(labelWidth, UiMetrics.ControlHeight), Control.RectTransform),
            string.Empty,
            textColor: UiMetrics.Text,
            textAlignment: Alignment.CenterLeft)
        {
            Padding = new Vector4(UiMetrics.Dip(UiMetrics.Pad), 0f, 0f, 0f),
            AutoScaleHorizontal = true,
            CanBeFocused = false
        };

        _controls = new GUILayoutGroup(
            new RectTransform(new Vector2(controlWidth, 1f), Control.RectTransform),
            isHorizontal: true,
            childAnchor: Anchor.CenterLeft)
        {
            CanBeFocused = false
        };
    }

    internal override void AddContent(ViewElement child)
    {
    }

    [ElementProperty(KeyText = true)]
    public RichString LabelText
    {
        set
        {
            _label.Text = value;
            _label.TextScale = UiMetrics.TextScale;
        }
    }

    [ElementProperty]
    public bool LabelActive
    {
        set
        {
            Color color = value ? UiMetrics.Text : UiMetrics.TextDim;
            _label.TextColor = color;
            _label.HoverColor = color;
        }
    }

    [ElementProperty]
    public float ControlWidth
    {
        set => _controls.RectTransform.RelativeSize = new Vector2(value, 1f);
    }

    internal override RectTransform ContentParent => _controls.RectTransform;
}
