using UiFramework.Layout;

namespace UiFramework.Controls;

[Element("Stack")]
internal sealed class StackElement : ViewElement
{
    private readonly GUILayoutGroup _group;
    private readonly RectTransform _rect;
    private readonly ViewContext _view;
    private readonly List<ViewElement> _children = [];
    private bool _horizontal;
    private bool _sharesPending;

    public StackElement(ElementContext context)
        : base(Create(context))
    {
        _group = (GUILayoutGroup)Control;
        _rect = Control.RectTransform;
        _view = context.View;
        _horizontal = _group.IsHorizontal;

        _rect.SizeChanged += ApplyShares;
        _rect.ScaleChanged += ApplyShares;
    }

    protected override void OnRelease()
    {
        _rect.SizeChanged -= ApplyShares;
        _rect.ScaleChanged -= ApplyShares;
    }

    [ElementProperty]
    public string Orientation
    {
        set
        {
            bool horizontal = string.Equals(value ?? "Vertical", "Horizontal", StringComparison.OrdinalIgnoreCase);

            if (_horizontal == horizontal) { return; }

            _horizontal = horizontal;
            _group.IsHorizontal = horizontal;

            ApplyShares();
        }
    }

    [ElementProperty]
    public Anchor ChildAnchor
    {
        set
        {
            _group.ChildAnchor = value;
            ApplyShares();
        }
    }

    [ElementProperty]
    public bool Stretch
    {
        set
        {
            _group.Stretch = value;
            ApplyShares();
        }
    }

    [ElementProperty]
    public float Spacing
    {
        set
        {
            _group.AbsoluteSpacing = UiMetrics.DipInt(value);
            ApplyShares();
        }
    }

    [ElementProperty]
    public float RelativeSpacing
    {
        set
        {
            _group.RelativeSpacing = value;
            ApplyShares();
        }
    }

    public override void AddContent(ViewElement child)
    {
        _children.Add(child);

        ApplyShares();
    }

    public override void ChildVisibilityChanged(ViewElement child) => ApplyShares();

    public override void ChildLayoutChanged(ViewElement child) => ApplyShares();

    private void ApplyShares()
    {
        if (_view.BatchingLayout)
        {
            if (!_sharesPending)
            {
                _sharesPending = true;
                _view.QueueShares(this);
            }

            return;
        }

        ArrangeShares();
    }

    internal void FlushShares()
    {
        if (!_sharesPending) { return; }

        _sharesPending = false;
        ArrangeShares();
    }

    private void ArrangeShares()
    {
        float shares = 0f;
        float taken = 0f;
        int shown = 0;
        bool changed = false;

        for (int i = 0; i < _children.Count; i++)
        {
            ViewElement child = _children[i];
            bool visible = child.Control.Visible;
            bool ignored = !visible;

            if (child.Control.IgnoreLayoutGroups != ignored)
            {
                child.Control.IgnoreLayoutGroups = ignored;
                changed = true;
            }

            if (!visible) { continue; }

            shown++;

            if (Share(child) is { } weight) { shares += weight; }
            else { taken += MainAxis(child) + MarginOf(child); }
        }

        if (shares > 0f)
        {
            float available = Math.Max(0f, 1f - taken - Gaps(shown));

            for (int i = 0; i < _children.Count; i++)
            {
                ViewElement child = _children[i];

                if (!child.Control.Visible || Share(child) is not { } weight) { continue; }

                if (SetMainAxis(child, Math.Max(0f, available * weight / shares - MarginOf(child)))) { changed = true; }
            }
        }

        if (!changed) { return; }

        _group.NeedsToRecalculate = true;

        if (_horizontal ? Control.Rect.Width > 0 : Control.Rect.Height > 0) { LayoutFlush.Apply(Control); }
    }

    private float? Share(ViewElement child)
    {
        Length length = _horizontal ? child.Width : child.Height;

        return length.Kind == LengthKind.Star ? length.Value : null;
    }

    private float MarginOf(ViewElement child)
    {
        Insets margin = child.Margin;

        if (margin.IsEmpty) { return 0f; }

        int extent = _horizontal ? Control.Rect.Width : Control.Rect.Height;
        if (extent <= 0) { return 0f; }

        return (_horizontal ? margin.Horizontal : margin.Vertical) / (float)extent;
    }

    private float MainAxis(ViewElement child)
    {
        Vector2 size = child.Control.RectTransform.RelativeSize;

        return _horizontal ? size.X : size.Y;
    }

    private bool SetMainAxis(ViewElement child, float value)
    {
        Vector2 size = child.Control.RectTransform.RelativeSize;

        if (MathF.Abs((_horizontal ? size.X : size.Y) - value) < 0.0001f) { return false; }

        child.Control.RectTransform.RelativeSize = _horizontal ? new(value, size.Y) : new(size.X, value);

        return true;
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
