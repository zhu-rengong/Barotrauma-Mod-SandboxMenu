namespace UiFramework.Controls;

internal readonly record struct BandArrangement(
    BandMetrics Metrics,
    int Viewport,
    int Window,
    int Shown,
    bool Tiles,
    bool TopTiles,
    bool BottomTiles,
    BandShape TopShape,
    BandShape BottomShape,
    int Pad,
    int Tail)
{
    internal int Signature => HashCode.Combine(
        HashCode.Combine(Window, Metrics.Count, Metrics.Room, Metrics.Width, Metrics.Spacing),
        HashCode.Combine(Metrics.RowHeight, Pad, Tail, Shown, TopTiles ? 1 : 0),
        HashCode.Combine(BottomTiles ? 1 : 0, TopShape.GetHashCode(), BottomShape.GetHashCode()));
}

internal sealed class ListBandLayout(ListBoxElement list, ListRowPool rowPool)
{
    private readonly GUIListBox _listBox = list.ListBox;
    private readonly ListBandGeometry _geometry = new();
    private readonly List<BuiltRow?> _slots = [];
    private readonly List<BuiltRow> _topTiles = [];
    private readonly List<BuiltRow> _bottomTiles = [];
    private readonly List<RectTransform> _order = [];

    private GUIFrame? _pad;
    private GUIFrame? _top;
    private GUIFrame? _bottom;
    private GUIFrame? _tail;

    private ShapeSignature<int> _signature;
    private ShapeSignature<(BandMetrics Metrics, float BarScroll, float BarSize, float TotalSize)> _inputs;

    private int _above;
    private int _below;

    internal bool IsEmpty => _slots.Count == 0 || _slots[0] is null;

    internal void Rebuild()
    {
        if (_slots.Count > 0 && _slots[0] is { } probe) { list.RowHeight = probe.Element.Control.Rect.Height; }

        rowPool.Release(_slots);
        rowPool.Release(_topTiles);
        rowPool.Release(_bottomTiles);

        list.Rows.Clear();
        _slots.Clear();
        _topTiles.Clear();
        _bottomTiles.Clear();

        list.RefreshIndex();

        list.Probed = false;

        if (list.Index.Count == 0) { return; }

        Apply(force: true);
    }

    internal void Update() => Apply(force: false);

    internal void DropAll()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] is { } slot) { rowPool.Drop(slot); }
        }

        for (int i = 0; i < _topTiles.Count; i++) { rowPool.Drop(_topTiles[i]); }
        for (int i = 0; i < _bottomTiles.Count; i++) { rowPool.Drop(_bottomTiles[i]); }

        _slots.Clear();
        _topTiles.Clear();
        _bottomTiles.Clear();

        DetachFrames();
    }

    private void Apply(bool force)
    {
        if (list.Index.Count == 0)
        {
            DetachFrames();

            _signature.Reset();
            _inputs.Reset();
            return;
        }

        int spacing = _listBox.Spacing;
        Insets padding = list.ContentPadding;

        int viewport = _listBox.Content.Rect.Height;
        int room = Math.Max(0, viewport - padding.Vertical);
        int width = Math.Max(1, _listBox.Content.Rect.Width - padding.Horizontal);

        if (width <= 0 || room <= 0)
        {
            _signature.Reset();
            return;
        }

        if (!SettleRowHeight())
        {
            _signature.Reset();
            return;
        }

        BandMetrics metrics = new(width, room, spacing, list.RowHeight, list.Index.Count, list.DetailRows);
        (BandMetrics Metrics, float BarScroll, float BarSize, float TotalSize) inputs =
            (metrics, _listBox.BarScroll, _listBox.BarSize, _listBox.TotalSize);

        if (!force && _inputs.Matches(inputs)) { return; }

        _inputs.Set(inputs);

        BandArrangement band = Arrange(metrics, viewport);

        if (!force && _signature.Matches(band.Signature)) { return; }

        _signature.Set(band.Signature);

        Commit(band);
    }

    private bool SettleRowHeight()
    {
        if (_slots.Count == 0 || _slots[0] is null)
        {
            if (!ProbeSlot()) { return false; }
        }

        int height = _slots[0] is { } first ? first.Element.Control.Rect.Height : list.RowHeight;

        if (height <= 0) { return false; }

        list.RowHeight = height;
        return true;
    }

    private BandArrangement Arrange(BandMetrics metrics, int viewport)
    {
        bool tiles = _geometry.Measure(metrics);
        int rows = tiles ? _geometry.DetailRowsOneBand : list.DetailRows;
        int stride = Math.Max(1, metrics.RowHeight + metrics.Spacing);
        int steps = Math.Max(0, metrics.Count - rows);
        float scrolled = _listBox.BarSize < 1f ? Math.Max(0f, _listBox.TotalSize - viewport) * _listBox.BarScroll : 0f;
        int scroll = (int)scrolled;
        int window = steps == 0 ? 0 : Math.Clamp(scroll / stride, 0, steps);

        bool topTiles = tiles && window > 0;
        bool bottomTiles = tiles && metrics.Count - window > rows;

        int columns = _geometry.Columns;
        int nominal = _geometry.NominalTileRows;
        int aboveItems = Math.Min(columns * nominal, window);
        int belowItems = Math.Max(0, metrics.Count - window - _geometry.MaxRows);
        int usedTop = topTiles ? Math.Clamp((aboveItems + columns - 1) / columns, 1, nominal) : 0;
        int usedBottom = bottomTiles ? Math.Clamp((belowItems + columns - 1) / columns, 1, nominal) : 0;
        int pitch = _geometry.CellWidth + metrics.Spacing;
        int wantTop = usedTop > 0 ? usedTop * pitch - metrics.Spacing : 0;
        int wantBottom = usedBottom > 0 ? usedBottom * pitch - metrics.Spacing : 0;
        int minTop = usedTop > 0 ? usedTop * _geometry.MinCell + (usedTop - 1) * metrics.Spacing : 0;
        int minBottom = usedBottom > 0 ? usedBottom * _geometry.MinCell + (usedBottom - 1) * metrics.Spacing : 0;
        int bands = (topTiles ? 1 : 0) + (bottomTiles ? 1 : 0);
        int gaps = bands == 2 ? 2 * metrics.Spacing : metrics.Spacing;

        int shown = bands == 0
            ? Math.Min(metrics.Count - window, _geometry.MaxRows)
            : Math.Clamp((int)MathF.Floor((metrics.Room - gaps - wantTop - wantBottom + metrics.Spacing) / (float)stride), list.DetailRows, _geometry.MaxRows);

        shown = Math.Min(shown, metrics.Count - window);

        int leftover = metrics.Room - gaps - (shown * stride - metrics.Spacing);
        int weight = Math.Max(1, usedTop + usedBottom);

        int topHeight = topTiles ? Math.Clamp(leftover * usedTop / weight, minTop, wantTop) : 0;
        int bottomHeight = bottomTiles ? Math.Clamp(leftover - topHeight, minBottom, wantBottom) : 0;

        if (topTiles && topHeight < wantTop && bottomTiles && bottomHeight >= wantBottom)
        {
            topHeight = Math.Clamp(leftover - bottomHeight, minTop, wantTop);
        }

        if (bottomTiles && bottomHeight < minBottom)
        {
            bottomHeight = minBottom;
            topHeight = leftover - bottomHeight;
        }

        if (topTiles && topHeight < minTop)
        {
            topHeight = minTop;
            bottomHeight = bottomTiles ? leftover - topHeight : 0;
        }

        BandShape topShape = topTiles ? _geometry.ShapeOf(topHeight, usedTop) : BandShape.None;
        BandShape bottomShape = bottomTiles ? _geometry.ShapeOf(bottomHeight, usedBottom) : BandShape.None;
        int children = 2 + shown + bands;

        int content = viewport + steps * stride;
        int budget = Math.Max(0, content - metrics.Spacing * children - topShape.Height - bottomShape.Height - shown * metrics.RowHeight);
        int pad = Math.Min(steps == 0 ? 0 : Math.Max(0, scroll - metrics.Spacing), budget);

        return new BandArrangement(
            metrics,
            viewport,
            window,
            shown,
            tiles,
            topTiles,
            bottomTiles,
            topShape,
            bottomShape,
            pad,
            budget - pad);
    }

    private void Commit(BandArrangement band)
    {
        RefreshDetailRows(band.Shown, band.Window);

        if (band.Tiles) { RefreshTiles(band); }

        PlaceBands(band);

        rowPool.FlushLayout();
        _listBox.ScrollBarNeedsRecalculation = true;
    }

    private void RefreshDetailRows(int shown, int window)
    {
        EnsureDetailRows(shown);

        RectTransform content = _listBox.Content.RectTransform;
        int contentWidth = content.Rect.Width;
        int width = Math.Max(1, contentWidth - list.ContentPadding.Horizontal);

        for (int i = 0; i < _slots.Count; i++)
        {
            object item = list.Index[window + i];

            if (_slots[i] is not { } row || !ReferenceEquals(row.Item, item))
            {
                if (_slots[i] is { } stale) { rowPool.Keep(stale); }

                _slots[i] = rowPool.Take(item);
            }

            if (_slots[i] is not { } live) { continue; }

            live.Element.Control.Visible = true;
            Inset(live.Element.Control, width, contentWidth);
        }
    }

    private static void Inset(GUIComponent row, int width, int contentWidth)
    {
        if (contentWidth <= 0) { return; }

        RectTransform rect = row.RectTransform;

        rect.SetPosition(Anchor.TopCenter, Pivot.TopCenter);
        rect.RelativeSize = new Vector2(width / (float)contentWidth, rect.RelativeSize.Y);
    }

    private void RefreshTiles(BandArrangement band)
    {
        if (band.TopTiles)
        {
            _top ??= CreateFrame();
            _above = Math.Min(band.TopShape.Capacity, band.Window);
            FillBand(_topTiles, _top, _above, band.Window - _above, band.TopShape.Capacity);
        }
        else
        {
            _above = 0;
        }

        if (band.BottomTiles)
        {
            _bottom ??= CreateFrame();
            _below = Math.Clamp(band.Metrics.Count - band.Window - band.Shown, 0, band.BottomShape.Capacity);
            FillBand(_bottomTiles, _bottom, _below, band.Window + band.Shown, band.BottomShape.Capacity);
        }
        else
        {
            _below = 0;
        }
    }

    private void FillBand(List<BuiltRow> tiles, GUIFrame band, int shown, int start, int capacity)
    {
        if (shown <= 0 || start >= list.Index.Count)
        {
            for (int i = 0; i < tiles.Count; i++) { tiles[i].Element.Control.Visible = false; }

            return;
        }

        rowPool.EnsureTiles(tiles, band, list.Index[start], capacity);

        int count = Math.Min(shown, Math.Min(capacity, tiles.Count));
        bool interactive = list.View.Interactive;

        for (int i = 0; i < count; i++)
        {
            object item = list.Index[start + i];
            BuiltRow tile = tiles[i];

            if (ReferenceEquals(tile.Item, item))
            {
                tile.Element.Control.Visible = true;
                tile.Element.Control.AutoUpdate = interactive;
                continue;
            }

            Templating.DataTemplate? template = rowPool.TileTemplate(item);

            if (template is not null && !ReferenceEquals(tile.Template, template))
            {
                rowPool.Keep(tile);

                if (rowPool.TakeTile(band, item) is { } swapped) { tiles[i] = swapped; continue; }

                tiles.RemoveAt(i);
                count = i;
                break;
            }

            tiles[i] = ListRowPool.Reuse(tile, item);
            tiles[i].Element.Control.AutoUpdate = interactive;
        }

        for (int i = count; i < tiles.Count; i++) { tiles[i].Element.Control.Visible = false; }
    }

    private void EnsureDetailRows(int shown)
    {
        while (_slots.Count < shown) { _slots.Add(null); }

        while (_slots.Count > shown)
        {
            if (_slots[^1] is { } extra) { rowPool.Keep(extra); }

            _slots.RemoveAt(_slots.Count - 1);
        }
    }

    private void PlaceBands(BandArrangement band)
    {
        GUIFrame padFrame = _pad ??= CreateFrame();
        GUIFrame tailFrame = _tail ??= CreateFrame();

        int inset = list.ContentPadding.Top;
        int offset = inset > 0 ? inset + _listBox.Spacing : 0;
        int width = band.Metrics.Width;

        SizeFrame(padFrame, width, band.Pad + offset);
        SizeFrame(tailFrame, width, Math.Max(0, band.Tail - offset));

        padFrame.Visible = true;
        tailFrame.Visible = true;

        if (_top is { } top)
        {
            SizeFrame(top, width, band.TopShape.Height);
            top.Visible = band.TopTiles;
        }

        if (_bottom is { } bottom)
        {
            SizeFrame(bottom, width, band.BottomShape.Height);
            bottom.Visible = band.BottomTiles;
        }

        _order.Clear();
        _order.Add(padFrame.RectTransform);

        if (band.TopTiles && _top is { } topBand) { _order.Add(topBand.RectTransform); }

        for (int i = 0; i < band.Shown; i++)
        {
            if (_slots[i] is not { } row || !row.Element.Control.Visible) { continue; }

            _order.Add(row.Element.Control.RectTransform);
        }

        if (band.BottomTiles && _bottom is { } bottomBand) { _order.Add(bottomBand.RectTransform); }

        _order.Add(tailFrame.RectTransform);

        if (!MatchesOrder()) { PlaceAll(); }

        if (band.TopTiles) { PlaceTiles(_topTiles, _top, _above, seamAtBottom: true, band.TopShape); }
        if (band.BottomTiles) { PlaceTiles(_bottomTiles, _bottom, _below, seamAtBottom: false, band.BottomShape); }

        _listBox.RecalculateChildren();
    }

    private bool MatchesOrder()
    {
        int index = 0;

        foreach (RectTransform child in _listBox.Content.RectTransform.Children)
        {
            if (index >= _order.Count) { return true; }
            if (!ReferenceEquals(child, _order[index])) { return false; }

            index++;
        }

        return index >= _order.Count;
    }

    private void PlaceAll()
    {
        RectTransform content = _listBox.Content.RectTransform;

        for (int i = 0; i < _order.Count; i++)
        {
            RectTransform transform = _order[i];

            transform.Parent = content;

            if (content.GetChildIndex(transform) != i) { transform.RepositionChildInHierarchy(i); }
        }
    }

    private void PlaceTiles(IReadOnlyList<BuiltRow> tiles, GUIFrame? band, int shown, bool seamAtBottom, BandShape shape)
    {
        if (band is null || shown <= 0 || tiles.Count == 0) { return; }

        int bandWidth = Math.Max(1, band.RectTransform.NonScaledSize.X);
        int bandHeight = Math.Max(1, band.RectTransform.NonScaledSize.Y);
        int columns = _geometry.Columns;
        int count = Math.Min(shown, tiles.Count);
        int left = list.ContentPadding.Left;
        int pitch = _geometry.CellWidth + _listBox.Spacing;
        int stride = shape.CellHeight + _listBox.Spacing;

        for (int i = 0; i < count; i++)
        {
            BuiltRow tile = seamAtBottom ? tiles[count - 1 - i] : tiles[i];
            int flowRow = i / columns;
            int within = i % columns;
            int column = (flowRow & 1) == 0 ? within : columns - 1 - within;
            int row = seamAtBottom ? shape.Rows - 1 - flowRow : flowRow;
            RectTransform transform = tile.Element.Control.RectTransform;

            transform.SetPosition(Anchor.TopLeft, Pivot.TopLeft);
            transform.RelativeSize = new Vector2(_geometry.CellWidth / (float)bandWidth, shape.CellHeight / (float)bandHeight);
            transform.AbsoluteOffset = new Point(left + column * pitch, row * stride);
        }
    }

    private GUIFrame CreateFrame()
        => new(
            new RectTransform(new Point(1, 1), _listBox.Content.RectTransform, Anchor.TopLeft, Pivot.TopLeft, ScaleBasis.Normal, isFixedSize: true),
            style: null)
        {
            CanBeFocused = false
        };

    private static void SizeFrame(GUIFrame frame, int width, int height)
    {
        Point size = new(Math.Max(1, width), Math.Max(0, height));

        if (frame.RectTransform.NonScaledSize != size) { frame.RectTransform.Resize(size, true); }
    }

    private void DetachFrames()
    {
        Detach(_pad);
        Detach(_top);
        Detach(_bottom);
        Detach(_tail);
    }

    private static void Detach(GUIFrame? frame)
    {
        if (frame is not { } value) { return; }

        value.Visible = false;
        value.RectTransform.Parent = null;
    }

    private bool ProbeSlot()
    {
        if (list.Probed || list.Index.Count == 0) { return false; }

        list.Probed = true;

        if (rowPool.Build(list.Index[0]) is not { } row) { return false; }

        EnsureDetailRows(list.DetailRows);

        _slots[0] = row;
        list.RowHeight = row.Element.Control.Rect.Height;

        return true;
    }
}
