namespace UiFramework.Layout;

internal abstract class LayoutPanel : ViewElement
{
    private readonly ViewContext _view;
    private bool _arranged;
    private bool _dirty = true;
    private float _gap;

    protected LayoutPanel(ElementContext context, GUIComponent control) : base(control)
    {
        _view = context.View;

        control.RectTransform.SizeChanged += Invalidate;
        control.RectTransform.ScaleChanged += Invalidate;
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
        QueueArrange();
    }

    private void QueueArrange()
    {
        if (_arranged) { return; }

        _arranged = true;
        _view.Once(() =>
        {
            _arranged = false;

            if (_dirty)
            {
                _dirty = false;
                Arrange(new Rectangle(Point.Zero, Control.Rect.Size));
            }
        });
    }

    protected abstract void Arrange(Rectangle area);

    protected void Stack(Rectangle area, bool horizontal)
    {
        int extent = horizontal ? area.Width : area.Height;

        List<(ViewElement Child, int Size, Length Length)> measured = [];
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

            measured.Add((child, size, length));
        }

        int gap = UiMetrics.DipInt(_gap);
        int available = Math.Max(0, extent - fixedTotal - gap * Math.Max(0, Children.Count - 1));
        int cursor = horizontal ? area.X : area.Y;

        foreach ((ViewElement child, int size, Length length) in measured)
        {
            int main = length.Kind == LengthKind.Star
                ? length.Resolve(available, size, starTotal is 0f ? 0f : length.Value / starTotal)
                : size;

            Rectangle childArea = horizontal
                ? new Rectangle(cursor, area.Y, main, area.Height)
                : new Rectangle(area.X, cursor, area.Width, main);

            Layout.Place(child, childArea);
            cursor += main + gap;
        }
    }
}
