namespace UiFramework.Layout;

internal abstract class LayoutPanel : ViewElement
{
    private readonly ViewContext _view;
    private readonly DeferredCall _arrange;
    private readonly RectTransform _rect;
    private readonly List<(ViewElement Child, int Size, Length Length)> _measured = [];
    private bool _dirty = true;
    private float _gap;

    protected LayoutPanel(ElementContext context, GUIComponent control) : base(control)
    {
        _view = context.View;
        _rect = control.RectTransform;
        _arrange = new DeferredCall(ArrangeNow);

        _rect.SizeChanged += Invalidate;
        _rect.ScaleChanged += Invalidate;
    }

    protected List<ViewElement> Children { get; } = [];

    protected void SetGap(float gapDip) => _gap = gapDip;

    public override void AddContent(ViewElement child)
    {
        Children.Add(child);
        Invalidate();
    }

    public override void ChildVisibilityChanged(ViewElement child) => Invalidate();

    public override void ChildLayoutChanged(ViewElement child) => Invalidate();

    protected void Report(string message, MarkupNode? node) => _view.Diagnostics.Report(message, node);

    protected void Invalidate()
    {
        _dirty = true;
        _arrange.Queue(_view);
    }

    private void ArrangeNow()
    {
        if (!_dirty) { return; }

        _dirty = false;
        Arrange(new Rectangle(Point.Zero, Control.Rect.Size));
    }

    protected override void OnRelease()
    {
        _rect.SizeChanged -= Invalidate;
        _rect.ScaleChanged -= Invalidate;
    }

    protected abstract void Arrange(Rectangle area);

    protected void Stack(Rectangle area, bool horizontal)
    {
        int extent = horizontal ? area.Width : area.Height;

        _measured.Clear();

        float starTotal = 0f;
        int fixedTotal = 0;

        foreach (ViewElement child in Children)
        {
            Length length = horizontal ? child.Width : child.Height;
            int size = length.Kind switch
            {
                LengthKind.Auto => horizontal ? Layout.Measure(child).X : Layout.Measure(child).Y,
                LengthKind.Dip => length.Resolve(0, 0),
                LengthKind.Percent => length.Resolve(extent, 0),
                _ => 0
            };

            if (length.Kind == LengthKind.Star) { starTotal += length.Value; }
            else { fixedTotal += size; }

            _measured.Add((child, size, length));
        }

        int gap = UiMetrics.DipInt(_gap);
        int available = StackShares.RemainingPixels(extent, fixedTotal, gap, _measured.Count);
        int cursor = horizontal ? area.X : area.Y;

        for (int i = 0; i < _measured.Count; i++)
        {
            (ViewElement child, int size, Length length) = _measured[i];

            int main = length.Kind == LengthKind.Star
                ? StackShares.PixelsOf(available, StackShares.Fraction(length.Value, starTotal))
                : size;

            Rectangle childArea = horizontal
                ? new Rectangle(cursor, area.Y, main, area.Height)
                : new Rectangle(area.X, cursor, area.Width, main);

            Layout.Place(child, childArea);
            cursor += main + gap;
        }
    }
}
