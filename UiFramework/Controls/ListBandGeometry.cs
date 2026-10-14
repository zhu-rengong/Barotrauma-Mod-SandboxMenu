namespace UiFramework.Controls;

internal readonly record struct BandMetrics(
    int Width,
    int Room,
    int Spacing,
    int RowHeight,
    int Count,
    int DetailRows);

internal sealed class ListBandGeometry
{
    private ShapeSignature<BandMetrics> _measured;

    private int _spacing = -1;

    internal int Columns { get; private set; } = 1;

    internal int CellWidth { get; private set; }

    internal int MinCell { get; private set; }

    internal int MaxRows { get; private set; }

    internal int NominalTileRows { get; private set; } = 1;

    internal int DetailRowsOneBand { get; private set; }

    internal bool Usable { get; private set; }

    internal bool Measure(BandMetrics metrics)
    {
        if (_measured.Matches(metrics)) { return Usable; }

        _measured.Set(metrics);
        _spacing = metrics.Spacing;

        int tile = UiMetrics.DipInt(UiTokens.Dip("tile", 36f));
        int stride = Math.Max(1, metrics.RowHeight + metrics.Spacing);

        Columns = Math.Max(1, (int)MathF.Round((metrics.Width + metrics.Spacing) / (float)Math.Max(1, tile + metrics.Spacing)));
        CellWidth = Math.Max(1, (metrics.Width - (Columns - 1) * metrics.Spacing) / Columns);
        MinCell = Math.Max(12, tile * 3 / 5);
        NominalTileRows = GridRows(metrics.Room - metrics.DetailRows * metrics.RowHeight - (metrics.DetailRows + 1) * metrics.Spacing, int.MaxValue);
        MaxRows = Math.Max(metrics.DetailRows, (metrics.Room - metrics.Spacing - MinCell) / stride);
        Usable = metrics.Room >= 3 * MinCell;

        int endTop = NominalTileRows * (CellWidth + metrics.Spacing) - metrics.Spacing;

        DetailRowsOneBand = Math.Clamp((int)MathF.Floor((metrics.Room - endTop) / (float)stride), metrics.DetailRows, MaxRows);

        return Usable;
    }

    internal BandShape ShapeOf(int height, int used)
    {
        int rows = Math.Clamp((height + _spacing) / (CellWidth + _spacing), 1, used);

        return new BandShape(height, rows, CellWidth, Usable ? Columns * rows : 0);
    }

    private int GridRows(int band, int cap)
        => Math.Clamp((int)MathF.Round((band / 2 + _spacing) / (float)Math.Max(1, CellWidth + _spacing)), 1, Math.Max(1, cap));
}

internal readonly record struct BandShape(int Height, int Rows, int CellHeight, int Capacity)
{
    internal static BandShape None { get; } = new(0, 1, 1, 0);
}
