namespace UiFramework.Layout;

[Element("Flow")]
internal sealed class FlowElement : LayoutPanel
{
    private bool _horizontal;

    public FlowElement(ElementContext context)
        : base(context, new GUIFrame(context.Rect(context.Parent, 1f, 1f), context.Skin) { HoverCursor = CursorState.Default })
    {
        _horizontal = string.Equals(context.Text("Orientation") ?? "Vertical", "Horizontal", StringComparison.OrdinalIgnoreCase);
        SetGap(context.Metric("Gap", 0f));
    }

    [ElementProperty]
    public string Orientation
    {
        set
        {
            _horizontal = string.Equals(value ?? "Vertical", "Horizontal", StringComparison.OrdinalIgnoreCase);
            Invalidate();
        }
    }

    [ElementProperty]
    public float Gap
    {
        set
        {
            SetGap(value);
            Invalidate();
        }
    }

    protected override void Arrange(Rectangle area) => Stack(area, _horizontal);
}

[Element("Grid")]
internal sealed class GridElement : LayoutPanel
{
    private Length[] _rows;
    private Length[] _columns;

    public GridElement(ElementContext context)
        : base(context, new GUIFrame(context.Rect(context.Parent, 1f, 1f), context.Skin) { HoverCursor = CursorState.Default })
    {
        _rows = ParseSequence(context.Text("Rows"), [Length.Fill], context.Node);
        _columns = ParseSequence(context.Text("Columns"), [Length.Fill], context.Node);
    }

    [ElementProperty]
    public string Rows
    {
        set
        {
            _rows = ParseSequence(value, _rows, Node);
            Invalidate();
        }
    }

    [ElementProperty]
    public string Columns
    {
        set
        {
            _columns = ParseSequence(value, _columns, Node);
            Invalidate();
        }
    }

    protected override void Arrange(Rectangle area)
    {
        int[] rows = Solve(_rows, area.Height, child => Layout.Measure(child).Y, true);
        int[] columns = Solve(_columns, area.Width, child => Layout.Measure(child).X, false);

        int column = 0;
        int row = 0;

        foreach (ViewElement child in Children)
        {
            bool explicitCell = TryCell(child, out int childRow, out int childColumn, out int rowSpan, out int columnSpan);

            if (!explicitCell)
            {
                childRow = Math.Min(row, _rows.Length - 1);
                childColumn = Math.Min(column, _columns.Length - 1);
            }

            Rectangle cell = new(
                area.X + Offset(columns, childColumn),
                area.Y + Offset(rows, childRow),
                Sum(columns, childColumn, columnSpan),
                Sum(rows, childRow, rowSpan));

            Layout.Place(child, cell);

            if (!explicitCell)
            {
                column += columnSpan;
                if (column >= _columns.Length) { column = 0; row++; }
            }
        }
    }

    private static bool TryCell(ViewElement child, out int row, out int column, out int rowSpan, out int columnSpan)
    {
        bool placed = child.Attached("Grid.Row") is not null || child.Attached("Grid.Column") is not null;

        row = Math.Max(0, child.AttachedInt("Grid.Row", 0));
        column = Math.Max(0, child.AttachedInt("Grid.Column", 0));
        rowSpan = Math.Max(1, child.AttachedInt("Grid.RowSpan", 1));
        columnSpan = Math.Max(1, child.AttachedInt("Grid.ColumnSpan", 1));

        return placed;
    }

    private static int Offset(int[] sizes, int index)
    {
        int offset = 0;
        for (int i = 0; i < index && i < sizes.Length; i++) { offset += sizes[i]; }
        return offset;
    }

    private static int Sum(int[] sizes, int index, int span)
    {
        int sum = 0;
        for (int i = index; i < index + span && i < sizes.Length; i++) { sum += sizes[i]; }
        return sum;
    }

    private int[] Solve(Length[] lengths, int available, Func<ViewElement, int> measure, bool rows)
    {
        int[] sizes = new int[lengths.Length];
        float starTotal = 0f;
        int fixedTotal = 0;

        for (int i = 0; i < lengths.Length; i++)
        {
            Length length = lengths[i];
            int measured = length.Kind == LengthKind.Auto ? MeasureTrack(i, measure, rows) : 0;

            sizes[i] = length.Kind switch
            {
                LengthKind.Auto => measured,
                LengthKind.Dip => length.Resolve(0, 0),
                LengthKind.Percent => length.Resolve(available, 0),
                _ => 0
            };

            if (length.Kind == LengthKind.Star) { starTotal += length.Value; }
            else { fixedTotal += sizes[i]; }
        }

        int left = Math.Max(0, available - fixedTotal);

        for (int i = 0; i < lengths.Length; i++)
        {
            if (lengths[i].Kind == LengthKind.Star)
            {
                sizes[i] = (int)MathF.Round(starTotal is 0f ? 0f : lengths[i].Value / starTotal * left);
            }
        }

        return sizes;
    }

    private int MeasureTrack(int index, Func<ViewElement, int> measure, bool rows)
    {
        int largest = 0;

        foreach (ViewElement child in Children)
        {
            if (TrackOf(child, rows) == index) { largest = Math.Max(largest, measure(child)); }
        }

        return largest;
    }

    private static int TrackOf(ViewElement child, bool rows)
        => Math.Max(0, child.AttachedInt(rows ? "Grid.Row" : "Grid.Column", 0));

    private Length[] ParseSequence(string? text, Length[] fallback, MarkupNode? node)
    {
        if (string.IsNullOrWhiteSpace(text)) { return fallback; }

        Length[] lengths =
        [
            .. text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => Length.Parse(part, Length.Fill, message => Report(message, node)))
        ];

        return lengths.Length == 0 ? fallback : lengths;
    }
}
