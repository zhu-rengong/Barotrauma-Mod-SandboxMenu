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
    private readonly struct Cell(int row, int column, int rowSpan, int columnSpan, bool placed)
    {
        internal int Row { get; } = row;

        internal int Column { get; } = column;

        internal int RowSpan { get; } = rowSpan;

        internal int ColumnSpan { get; } = columnSpan;

        internal bool Placed { get; } = placed;
    }

    private Length[] _rows;
    private Length[] _columns;

    private int[] _rowSizes = [];
    private int[] _columnSizes = [];
    private int[] _rowPrefix = [];
    private int[] _columnPrefix = [];
    private int[] _trackMax = [];
    private Point[] _measures = [];
    private Cell[] _cells = [];

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
        int count = Children.Count;

        Prepare(count, _rows.Length, _columns.Length);

        bool measure = AnyAuto(_rows) || AnyAuto(_columns);

        for (int i = 0; i < count; i++)
        {
            ViewElement child = Children[i];
            _cells[i] = CellOf(child);

            if (measure) { _measures[i] = Layout.Measure(child); }
        }

        Solve(_rows, area.Height, true);
        Solve(_columns, area.Width, false);

        int column = 0;
        int row = 0;

        for (int i = 0; i < count; i++)
        {
            Cell cell = _cells[i];
            int childRow = cell.Row;
            int childColumn = cell.Column;

            if (!cell.Placed)
            {
                childRow = Math.Min(row, _rows.Length - 1);
                childColumn = Math.Min(column, _columns.Length - 1);
            }

            Rectangle cellArea = new(
                area.X + Offset(_columnPrefix, childColumn),
                area.Y + Offset(_rowPrefix, childRow),
                Span(_columnPrefix, childColumn, cell.ColumnSpan),
                Span(_rowPrefix, childRow, cell.RowSpan));

            Layout.Place(Children[i], cellArea);

            if (!cell.Placed)
            {
                column += cell.ColumnSpan;
                if (column >= _columns.Length) { column = 0; row++; }
            }
        }
    }

    private static Cell CellOf(ViewElement child)
    {
        object? row = child.Attached("Grid.Row");
        object? column = child.Attached("Grid.Column");

        return new Cell(
            Math.Max(0, AsInt(row, 0)),
            Math.Max(0, AsInt(column, 0)),
            Math.Max(1, AsInt(child.Attached("Grid.RowSpan"), 1)),
            Math.Max(1, AsInt(child.Attached("Grid.ColumnSpan"), 1)),
            row is not null || column is not null);
    }

    private static int AsInt(object? value, int fallback)
        => value is int number ? number
            : value is string text && int.TryParse(text, out int parsed) ? parsed
            : fallback;

    private static bool AnyAuto(Length[] lengths)
    {
        for (int i = 0; i < lengths.Length; i++)
        {
            if (lengths[i].Kind == LengthKind.Auto) { return true; }
        }

        return false;
    }

    private static int Offset(int[] prefix, int index)
        => prefix[Math.Clamp(index, 0, prefix.Length - 1)];

    private static int Span(int[] prefix, int index, int span)
    {
        int from = Math.Clamp(index, 0, prefix.Length - 1);
        int to = Math.Clamp(index + span, from, prefix.Length - 1);

        return prefix[to] - prefix[from];
    }

    private void Prepare(int count, int rows, int columns)
    {
        if (_measures.Length < count)
        {
            _measures = new Point[count];
            _cells = new Cell[count];
        }

        if (_rowSizes.Length != rows)
        {
            _rowSizes = new int[rows];
            _rowPrefix = new int[rows + 1];
        }

        if (_columnSizes.Length != columns)
        {
            _columnSizes = new int[columns];
            _columnPrefix = new int[columns + 1];
        }

        int tracks = Math.Max(rows, columns);

        if (_trackMax.Length < tracks) { _trackMax = new int[tracks]; }
    }

    private void Solve(Length[] lengths, int available, bool rows)
    {
        int count = lengths.Length;

        if (count == 0) { return; }

        int[] sizes = rows ? _rowSizes : _columnSizes;
        int[] prefix = rows ? _rowPrefix : _columnPrefix;

        if (AnyAuto(lengths))
        {
            Array.Clear(_trackMax, 0, count);

            for (int i = 0; i < Children.Count; i++)
            {
                Cell cell = _cells[i];
                int track = rows ? cell.Row : cell.Column;

                if ((uint)track >= (uint)count) { continue; }

                int measured = rows ? _measures[i].Y : _measures[i].X;

                if (measured > _trackMax[track]) { _trackMax[track] = measured; }
            }
        }

        float starTotal = 0f;
        int fixedTotal = 0;

        for (int i = 0; i < count; i++)
        {
            Length length = lengths[i];
            int size = length.Kind switch
            {
                LengthKind.Auto => _trackMax[i],
                LengthKind.Dip => length.Resolve(0, 0),
                LengthKind.Percent => length.Resolve(available, 0),
                _ => 0
            };

            sizes[i] = size;

            if (length.Kind == LengthKind.Star) { starTotal += length.Value; }
            else { fixedTotal += size; }
        }

        int left = Math.Max(0, available - fixedTotal);

        for (int i = 0; i < count; i++)
        {
            if (lengths[i].Kind == LengthKind.Star)
            {
                sizes[i] = (int)MathF.Round(starTotal is 0f ? 0f : lengths[i].Value / starTotal * left);
            }
        }

        prefix[0] = 0;

        for (int i = 0; i < count; i++) { prefix[i + 1] = prefix[i] + sizes[i]; }
    }

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
