namespace UiFramework.Layout;

internal abstract class LayoutPanel : ViewElement
{
    private readonly ViewLoadContext _view;
    private bool _arranged;
    private bool _dirty = true;
    private float _gap;

    protected LayoutPanel(ElementContext context, GUIComponent control) : base(control)
    {
        _view = context.View;
    }

    protected List<ViewElement> Children { get; } = [];

    protected void SetGap(float dip) => _gap = dip;

    internal override void AddContent(ViewElement child)
    {
        Children.Add(child);
        Invalidate();
    }

    protected static Length WidthOf(ViewElement child, Length fallback)
        => Length.Parse(child.Node?.Text("Width"), fallback);

    protected static Length HeightOf(ViewElement child, Length fallback)
        => Length.Parse(child.Node?.Text("Height"), fallback);

    protected static string? Attached(ViewElement child, string property)
        => child.Node?.Text(property);

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

    protected static int SizeOf(Length length, int available, int measured, float starShare)
        => length.Resolve(available, measured, starShare);

    protected void Stack(Rectangle area, bool horizontal)
    {
        int cross = horizontal ? area.Height : area.Width;

        List<(ViewElement Child, int Size)> measured = [];
        float starTotal = 0f;
        int fixedTotal = 0;

        foreach (ViewElement child in Children)
        {
            Length length = horizontal ? WidthOf(child, Length.Fill) : HeightOf(child, Length.Fill);
            int size = length.Kind switch
            {
                LengthKind.Auto => (horizontal ? Layout.Measure(child).X : Layout.Measure(child).Y),
                LengthKind.Dip => length.Resolve(0, 0),
                _ => 0
            };

            if (length.Kind == LengthKind.Star) { starTotal += length.Value; }
            else { fixedTotal += size; }

            measured.Add((child, size));
        }

        int gap = UiMetrics.DipInt(_gap);
        int available = Math.Max(0, (horizontal ? area.Width : area.Height) - fixedTotal - gap * Math.Max(0, Children.Count - 1));
        int cursor = horizontal ? area.X : area.Y;

        foreach ((ViewElement child, int size) in measured)
        {
            Length length = horizontal ? WidthOf(child, Length.Fill) : HeightOf(child, Length.Fill);
            int main = length.Kind == LengthKind.Star
                ? SizeOf(length, available, size, starTotal is 0f ? 0f : length.Value / starTotal)
                : size;

            Rectangle childArea = horizontal
                ? new Rectangle(cursor, area.Y, main, area.Height)
                : new Rectangle(area.X, cursor, area.Width, main);

            Layout.Place(child, childArea);
            cursor += main + gap;
        }
    }
}
