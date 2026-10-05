namespace UiFramework.Controls;

// The grid a banded list lays its tiles out in, and the bounds the viewport puts on the detail rows. Worked out
// per frame, so it only keeps what a size, a row height and a list length fix for good.
internal sealed class ListBandGeometry
{
    private int _width = -1;
    private int _viewport = -1;
    private int _spacing = -1;
    private int _rowHeight = -1;
    private int _count = -1;

    internal int Columns { get; private set; } = 1;

    internal int CellWidth { get; private set; }

    internal int MinCell { get; private set; }

    internal int MaxRows { get; private set; }

    // The tile rows one side nominates for itself, and the detail rows left when only one side holds tiles.
    internal int NominalTileRows { get; private set; } = 1;

    internal int DetailRowsOneBand { get; private set; }

    internal bool Usable { get; private set; }

    internal bool Measure(int width, int viewport, int spacing, int rowHeight, int count, int detailRows)
    {
        if (_width == width && _viewport == viewport && _spacing == spacing
            && _rowHeight == rowHeight && _count == count)
        {
            return Usable;
        }

        _width = width;
        _viewport = viewport;
        _spacing = spacing;
        _rowHeight = rowHeight;
        _count = count;

        int tile = UiMetrics.DipInt(UiTokens.Dip("tile", 36f));
        int stride = Math.Max(1, rowHeight + spacing);

        Columns = Math.Max(1, (int)MathF.Round((width + spacing) / (float)Math.Max(1, tile + spacing)));
        CellWidth = Math.Max(1, (width - (Columns - 1) * spacing) / Columns);
        MinCell = Math.Max(12, tile * 3 / 5);
        NominalTileRows = GridRows((viewport - detailRows * rowHeight - (detailRows + 1) * spacing) / 2, spacing, CellWidth, int.MaxValue);
        MaxRows = Math.Max(detailRows, (viewport - spacing - MinCell) / stride);
        Usable = viewport >= 3 * MinCell;

        // The rows one band alone leaves for the detail rows: it sets the scroll range and with it which sides
        // have items at all, while what the rows are laid out as is worked out per frame from what each band shows.
        int items = Math.Max(0, count - detailRows);
        int used = Math.Max(1, Math.Min(NominalTileRows, (items + Columns - 1) / Columns));

        DetailRowsOneBand = Math.Clamp(detailRows + NominalTileRows + (NominalTileRows - used), detailRows, MaxRows);

        return Usable;
    }

    // The rows a band of this height holds at the cell width the grid has, capped by the rows the items fill:
    // fewer rows means taller cells, which keeps a band with little to show filling its own space.
    internal BandShape ShapeOf(int height, int used)
    {
        int rows = GridRows(height, _spacing, CellWidth, used);

        return new BandShape(height, rows, Math.Max(1, (height - (rows - 1) * _spacing) / rows), Usable ? Columns * rows : 0);
    }

    private static int GridRows(int band, int spacing, int cellWidth, int cap)
        => Math.Clamp((int)MathF.Round((band + spacing) / (float)Math.Max(1, cellWidth + spacing)), 1, Math.Max(1, cap));
}

// What one grid band is this frame: how tall it is, how many tile rows it shows, how tall a cell is and how many
// tiles it holds.
internal readonly record struct BandShape(int Height, int Rows, int CellHeight, int Capacity)
{
    internal static BandShape None { get; } = new(0, 1, 1, 0);
}
