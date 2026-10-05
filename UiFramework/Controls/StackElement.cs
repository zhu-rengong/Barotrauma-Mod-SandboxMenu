using UiFramework.Layout;

namespace UiFramework.Controls;

[Element("Stack")]
internal sealed class StackElement : ViewElement
{
    private readonly GUILayoutGroup _group;
    private readonly bool _horizontal;
    private readonly List<ViewElement> _children = [];

    public StackElement(ElementContext context)
        : base(Create(context))
    {
        _group = (GUILayoutGroup)Control;
        _horizontal = _group.IsHorizontal;

        Control.RectTransform.SizeChanged += ApplyShares;
        Control.RectTransform.ScaleChanged += ApplyShares;
    }

    public override void AddContent(ViewElement child)
    {
        _children.Add(child);

        ApplyShares();
    }

    public override void ChildVisibilityChanged(ViewElement child) => ApplyShares();

    // Children sized as a share ("1*", "2*") split what the rest of the axis leaves, so a row fills its width again
    // once one of them is hidden; the host layout group keeps a place for every child it was handed, so a hidden
    // child is taken out of the layout as well.
    private void ApplyShares()
    {
        float shares = 0f;
        float taken = 0f;
        int shown = 0;

        foreach (ViewElement child in _children)
        {
            bool visible = child.Control.Visible;

            child.Control.IgnoreLayoutGroups = !visible;

            if (!visible) { continue; }

            shown++;

            if (Share(child) is { } weight) { shares += weight; }
            else { taken += MainAxis(child) + Margin(child); }
        }

        if (shares > 0f)
        {
            float available = Math.Max(0f, 1f - taken - Gaps(shown));

            foreach (ViewElement child in _children)
            {
                if (!child.Control.Visible || Share(child) is not { } weight) { continue; }

                SetMainAxis(child, Math.Max(0f, available * weight / shares - Margin(child)));
            }
        }

        if (_horizontal ? Control.Rect.Width > 0 : Control.Rect.Height > 0) { LayoutFlush.Apply(Control); }
        else { _group.NeedsToRecalculate = true; }
    }

    private float? Share(ViewElement child)
        => Length.TryWeight(child.Node?.Text(_horizontal ? "Width" : "Height"), out float weight) ? weight : null;

    // The room a margin keeps outside the child, as a fraction of the stack's own extent: the host lays every child out
    // in the share it is handed, so the margin has to come off that share. The child sits centred in what is left.
    private float Margin(ViewElement child)
    {
        if (child.Node?.Text("Margin") is not { Length: > 0 } text) { return 0f; }

        int extent = _horizontal ? Control.Rect.Width : Control.Rect.Height;
        if (extent <= 0) { return 0f; }

        Insets margin = Insets.Parse(text);

        return (_horizontal ? margin.Horizontal : margin.Vertical) / (float)extent;
    }

    private float MainAxis(ViewElement child)
    {
        Vector2 size = child.Control.RectTransform.RelativeSize;

        return _horizontal ? size.X : size.Y;
    }

    private void SetMainAxis(ViewElement child, float value)
    {
        Vector2 size = child.Control.RectTransform.RelativeSize;

        child.Control.RectTransform.RelativeSize = _horizontal ? new(value, size.Y) : new(size.X, value);
    }

    private float Gaps(int shown)
    {
        if (shown < 2) { return 0f; }

        int count = shown - 1;
        int extent = _horizontal ? Control.Rect.Width : Control.Rect.Height;
        float gaps = _group.RelativeSpacing * count;

        if (_group.AbsoluteSpacing > 0 && extent > 0) { gaps += _group.AbsoluteSpacing * count / (float)extent; }

        return gaps;
    }

    private static GUILayoutGroup Create(ElementContext context)
    {
        bool horizontal = string.Equals(context.Text("Orientation") ?? "Vertical", "Horizontal", StringComparison.OrdinalIgnoreCase);

        return new GUILayoutGroup(
            context.Rect(context.Parent, 1f, 1f),
            horizontal,
            ViewMarkup.AnchorOf(context.Text("ChildAnchor"), horizontal ? Anchor.CenterLeft : Anchor.TopLeft))
        {
            Stretch = ViewMarkup.ToBool(context.Text("Stretch"), false),
            AbsoluteSpacing = UiMetrics.DipInt(context.Metric("Spacing", 0f)),
            RelativeSpacing = context.Size("RelativeSpacing", 0f)
        };
    }
}
