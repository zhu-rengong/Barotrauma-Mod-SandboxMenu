namespace UiFramework.Controls;

// A banded list splits its viewport into three pinned parts — a grid of tiles above, the detail rows in the
// middle, a grid below, each showing the items right outside the detail window — all inside the same host list,
// so the wheel, the scroll bar, the clipping and the input clamping stay the host's own. Spacer heights put the
// parts where they belong, and keep one stride of the scroll bar equal to one item of the list.
internal sealed class ListBandLayout(ListBoxElement list, ListRowPool rowPool)
{
    private readonly GUIListBox _listBox = list.ListBox;
    private readonly ListBandGeometry _geometry = new();
    private readonly List<BuiltRow?> _slots = [];
    private readonly List<BuiltRow> _topTiles = [];
    private readonly List<BuiltRow> _bottomTiles = [];

    private GUIFrame? _pad;
    private GUIFrame? _top;
    private GUIFrame? _bottom;
    private GUIFrame? _tail;

    // The whole content has to keep the length the scroll bar maps onto the items, so every band calculation below
    // counts the same children every frame; the signature says when the result stopped changing.
    private int _signature;

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
        int count = list.Index.Count;

        if (count == 0)
        {
            DetachFrames();

            _signature = 0;
            return;
        }

        int spacing = _listBox.Spacing;
        int viewport = _listBox.Content.Rect.Height;
        int width = _listBox.Content.Rect.Width;

        if (width <= 0 || viewport <= 0)
        {
            _signature = 0;
            return;
        }

        if (_slots.Count == 0 || _slots[0] is null)
        {
            if (!ProbeSlot())
            {
                _signature = 0;
                return;
            }
        }

        int height = _slots[0] is { } first ? first.Element.Control.Rect.Height : list.RowHeight;

        if (height <= 0)
        {
            _signature = 0;
            return;
        }

        list.RowHeight = height;

        bool tiles = _geometry.Measure(width, viewport, spacing, height, count, list.DetailRows);
        int rows = tiles ? _geometry.DetailRowsOneBand : list.DetailRows;
        int stride = Math.Max(1, height + spacing);
        int steps = Math.Max(0, count - rows);
        float scrolled = _listBox.BarSize < 1f ? Math.Max(0f, _listBox.TotalSize - viewport) * _listBox.BarScroll : 0f;
        int scroll = (int)scrolled;
        int window = steps == 0 ? 0 : Math.Clamp(scroll / stride, 0, steps);

        // A side without items hands its grid space to the detail rows, and which sides have items is what the window
        // position alone decides: one position is one layout, so the rows never flap between two counts in place.
        bool topOn = window > 0;
        bool bottomOn = count - window > rows;
        bool topTiles = tiles && topOn;
        bool bottomTiles = tiles && bottomOn;

        // Each side keeps only the tile rows it really fills: the top band sees at most min(the tiles it holds, the
        // items above the window), the bottom band is credited with the fewest items it can be left with. The rows
        // neither side can use go to the detail rows, so a grid that shows one row gives away one row, no more.
        int columns = _geometry.Columns;
        int nominal = _geometry.NominalTileRows;
        int cells = columns * nominal;
        int aboveItems = Math.Min(cells, window);
        int belowItems = Math.Max(0, count - window - _geometry.MaxRows);
        int usedTop = topTiles ? Math.Clamp((aboveItems + columns - 1) / columns, 1, nominal) : 0;
        int usedBottom = bottomTiles ? Math.Clamp((belowItems + columns - 1) / columns, 1, nominal) : 0;
        int pitch = _geometry.CellWidth + spacing;
        int wantTop = usedTop > 0 ? usedTop * pitch - spacing : 0;
        int wantBottom = usedBottom > 0 ? usedBottom * pitch - spacing : 0;
        int minTop = usedTop > 0 ? usedTop * _geometry.MinCell + (usedTop - 1) * spacing : 0;
        int minBottom = usedBottom > 0 ? usedBottom * _geometry.MinCell + (usedBottom - 1) * spacing : 0;
        int bands = (topTiles ? 1 : 0) + (bottomTiles ? 1 : 0);
        int gaps = bands == 2 ? 2 * spacing : spacing;

        // The detail rows take what is left once each band has the height its own items call for, rounded off: a band
        // that fills one row hands over one row, and a band that fills two keeps two, so the grids stay as full as
        // they were and only the rounding half row is made up out of the bands.
        int shown = bands == 0
            ? Math.Min(count - window, _geometry.MaxRows)
            : Math.Clamp((int)MathF.Round((viewport - gaps - wantTop - wantBottom + spacing) / (float)stride), list.DetailRows, _geometry.MaxRows);

        shown = Math.Min(shown, count - window);

        int leftover = viewport - gaps - (shown * stride - spacing);
        int weight = Math.Max(1, usedTop + usedBottom);
        int topHeight = topTiles ? Math.Max(minTop, leftover * usedTop / weight) : 0;
        int bottomHeight = bottomTiles ? leftover - topHeight : 0;

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
        int pad;
        int tail = 0;

        // A list the viewport can hold whole needs no pinning: its rows start at the top of the list, like any other
        // list, and the tail takes the rest of the viewport so the bar stays where it is.
        pad = steps == 0 ? 0 : Math.Max(0, scroll - spacing);

        // One stride of the content is one item of the list. The tail is what keeps the content as long as the scroll
        // bar expects, and the pad is held to what the tail can take: a pad that asks for more would push the host's
        // own length up, and the scroll position is read back from that length. Where it is held back, the pinned
        // parts move along with the scroll for the last few pixels of the range instead.
        int content = viewport + steps * stride;
        int budget = Math.Max(0, content - spacing * children - topShape.Height - bottomShape.Height - shown * height);

        pad = Math.Min(pad, budget);
        tail = budget - pad;

        int signature = HashCode.Combine(
            HashCode.Combine(window, count, viewport, width, spacing),
            HashCode.Combine(height, pad, tail, shown, topTiles ? 1 : 0),
            HashCode.Combine(bottomTiles ? 1 : 0, topShape.GetHashCode(), bottomShape.GetHashCode()));

        if (!force && signature == _signature) { return; }

        _signature = signature;

        RefreshDetailRows(shown, window);
        if (tiles) { RefreshTiles(count, window, shown, topTiles, bottomTiles, topShape, bottomShape); }

        Arrange(pad, tail, shown, width, topTiles, bottomTiles, topShape, bottomShape);

        _listBox.ScrollBarNeedsRecalculation = true;
    }

    private void RefreshDetailRows(int shown, int window)
    {
        EnsureDetailRows(shown);

        for (int i = 0; i < _slots.Count; i++)
        {
            object item = list.Index[window + i];

            if (_slots[i] is not { } row || !ReferenceEquals(row.Item, item))
            {
                if (_slots[i] is { } stale) { rowPool.Keep(stale); }

                _slots[i] = rowPool.Take(item);
                continue;
            }

            row.Element.Control.Visible = true;
        }
    }

    private void RefreshTiles(int count, int window, int shown, bool topTiles, bool bottomTiles, BandShape topShape, BandShape bottomShape)
    {
        if (topTiles)
        {
            _top ??= CreateFrame();
            _above = Math.Min(topShape.Capacity, window);
            FillBand(_topTiles, _top, _above, window - _above, topShape.Capacity);
        }
        else
        {
            _above = 0;
        }

        if (bottomTiles)
        {
            _bottom ??= CreateFrame();
            _below = Math.Clamp(count - window - shown, 0, bottomShape.Capacity);
            FillBand(_bottomTiles, _bottom, _below, window + shown, bottomShape.Capacity);
        }
        else
        {
            _below = 0;
        }
    }

    private void FillBand(List<BuiltRow> tiles, GUIFrame band, int shown, int start, int capacity)
    {
        // A band whose side has nothing left to show keeps its tiles hidden: the items run out exactly there now that
        // the detail rows take as much of the list as they can.
        if (shown <= 0 || start >= list.Index.Count)
        {
            for (int i = 0; i < tiles.Count; i++) { tiles[i].Element.Control.Visible = false; }

            return;
        }

        rowPool.EnsureTiles(tiles, band, list.Index[start], capacity);

        int count = Math.Min(shown, Math.Min(capacity, tiles.Count));

        for (int i = 0; i < count; i++)
        {
            object item = list.Index[start + i];
            BuiltRow tile = tiles[i];

            if (ReferenceEquals(tile.Item, item))
            {
                tile.Element.Control.Visible = true;
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

    // The banded parts are the only children of the content and they keep their slots, so nothing is created or
    // detached while scrolling: rows and tiles that were pooled elsewhere are the only parts that come and go, and
    // every part is moved to its slot in the order the host lays children out in before it positions them.
    private void Arrange(int pad, int tail, int shown, int width, bool topTiles, bool bottomTiles, BandShape topShape, BandShape bottomShape)
    {
        GUIFrame padFrame = _pad ??= CreateFrame();
        GUIFrame tailFrame = _tail ??= CreateFrame();

        SizeFrame(padFrame, width, pad);
        SizeFrame(tailFrame, width, tail);

        padFrame.Visible = true;
        tailFrame.Visible = true;

        if (_top is { } top)
        {
            SizeFrame(top, width, topShape.Height);
            top.Visible = topTiles;
        }

        if (_bottom is { } bottom)
        {
            SizeFrame(bottom, width, bottomShape.Height);
            bottom.Visible = bottomTiles;
        }

        int slot = 0;

        PlacePart(padFrame.RectTransform, slot++);

        if (topTiles && _top is { } firstBand) { PlacePart(firstBand.RectTransform, slot++); }

        for (int i = 0; i < shown; i++)
        {
            if (_slots[i] is not { } row || !row.Element.Control.Visible) { continue; }

            PlacePart(row.Element.Control.RectTransform, slot++);
        }

        if (bottomTiles && _bottom is { } secondBand) { PlacePart(secondBand.RectTransform, slot++); }

        PlacePart(tailFrame.RectTransform, slot);

        if (topTiles) { PlaceTiles(_topTiles, _top, _above, seamAtBottom: true, topShape.Rows, topShape.CellHeight); }
        if (bottomTiles) { PlaceTiles(_bottomTiles, _bottom, _below, seamAtBottom: false, bottomShape.Rows, bottomShape.CellHeight); }

        _listBox.RecalculateChildren();
    }

    private void PlacePart(RectTransform transform, int slot)
    {
        RectTransform content = _listBox.Content.RectTransform;

        transform.Parent = content;

        // Moving a part re-scales its whole subtree, so it is only moved when it is not already in its slot.
        if (content.GetChildIndex(transform) != slot) { transform.RepositionChildInHierarchy(slot); }
    }

    // The tiles run from the seam outwards: the item next to the detail window sits at the left end of the seam row
    // and the flow carries on rightwards, then from the right end of the row beyond it and back, so the items of a
    // band stay in sequence and next to each other on screen.
    private void PlaceTiles(List<BuiltRow> tiles, GUIFrame? band, int shown, bool seamAtBottom, int tileRows, int cellHeight)
    {
        if (band is null || shown <= 0 || tiles.Count == 0) { return; }

        int bandWidth = Math.Max(1, band.RectTransform.NonScaledSize.X);
        int bandHeight = Math.Max(1, band.RectTransform.NonScaledSize.Y);
        int columns = _geometry.Columns;
        int count = Math.Min(shown, tiles.Count);

        for (int i = 0; i < count; i++)
        {
            // The top band fills from the seam outwards too, and the tiles it holds run oldest first, so the item
            // next to the detail window is the last one of the list and takes the first place of the flow.
            BuiltRow tile = seamAtBottom ? tiles[count - 1 - i] : tiles[i];
            int flowRow = i / columns;
            int within = i % columns;
            int column = (flowRow & 1) == 0 ? within : columns - 1 - within;
            int row = seamAtBottom ? tileRows - 1 - flowRow : flowRow;
            RectTransform transform = tile.Element.Control.RectTransform;

            transform.SetPosition(Anchor.TopLeft, Pivot.TopLeft);
            transform.RelativeSize = new Vector2(_geometry.CellWidth / (float)bandWidth, cellHeight / (float)bandHeight);
            transform.AbsoluteOffset = new Point(
                column * (_geometry.CellWidth + _listBox.Spacing),
                row * (cellHeight + _listBox.Spacing));
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

    // A detached control that is still visible stays in the host's update list, so hiding is what takes it out.
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
