namespace UiFramework.Controls;

[Element("Panel")]
internal sealed class PanelElement : ViewElement
{
    private readonly GUIFrame _panel;
    private readonly Insets _padding;
    private GUIFrame? _content;

    public PanelElement(ElementContext context)
        : base(new GUIFrame(context.Rect(context.Parent, 1f, 1f), context.Skin)
        {
            HoverCursor = CursorState.Default
        })
    {
        _panel = (GUIFrame)Control;
        _padding = context.Padding;

        if (_padding.IsEmpty) { return; }

        _content = new GUIFrame(new RectTransform(Vector2.One, _panel.RectTransform, Anchor.Center), style: null)
        {
            CanBeFocused = false
        };

        Fit();

        _panel.RectTransform.SizeChanged += Fit;
    }

    public override RectTransform ContentParent => _content?.RectTransform ?? Control.RectTransform;

    public override void AddContent(ViewElement child)
    {
    }

    protected override void OnRelease()
    {
        _panel.RectTransform.SizeChanged -= Fit;
        _content = null;
    }

    private void Fit()
    {
        if (_content is not { } content) { return; }

        Rectangle rect = _panel.Rect;
        if (rect.Width <= 0 || rect.Height <= 0) { return; }

        Point size = new(
            Math.Max(1, rect.Width - _padding.Horizontal),
            Math.Max(1, rect.Height - _padding.Vertical));

        content.RectTransform.MinSize = size;
        content.RectTransform.MaxSize = size;
        content.RectTransform.NonScaledSize = size;

        content.RectTransform.AbsoluteOffset = new Point(
            (_padding.Left - _padding.Right) / 2,
            (_padding.Top - _padding.Bottom) / 2);
    }
}
