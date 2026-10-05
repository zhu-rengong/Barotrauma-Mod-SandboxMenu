namespace UiFramework.Controls;

// Worked out per frame, so it keeps only what a size, a row height and a list length fix for good.
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

        // The sum the frame works out where the top band is the only one on screen: it has to match, or the last items
        // stay behind in the bottom band instead of coming to rest in the rows.
        int endTop = NominalTileRows * (CellWidth + spacing) - spacing;

        DetailRowsOneBand = Math.Clamp((int)MathF.Floor((viewport - endTop) / (float)stride), detailRows, MaxRows);

        return Usable;
    }

    // The rows a band of this height holds, in whole cells: a cell is as tall as it is wide, so the gap across matches
    // the gap down whatever height the band comes out at.
    internal BandShape ShapeOf(int height, int used)
    {
        int rows = Math.Clamp((height + _spacing) / (CellWidth + _spacing), 1, used);

        return new BandShape(height, rows, CellWidth, Usable ? Columns * rows : 0);
    }

    private static int GridRows(int band, int spacing, int cellWidth, int cap)
        => Math.Clamp((int)MathF.Round((band + spacing) / (float)Math.Max(1, cellWidth + spacing)), 1, Math.Max(1, cap));
}

internal readonly record struct BandShape(int Height, int Rows, int CellHeight, int Capacity)
{
    internal static BandShape None { get; } = new(0, 1, 1, 0);
}
