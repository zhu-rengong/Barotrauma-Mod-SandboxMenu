using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UiFramework.Controls;

[Element("List")]
internal sealed class ListBoxElement : ViewElement, IDisposable
{
    private const float DragThreshold = 6f;

    private const int SpareLimit = 240;

    private const int WindowOverscan = 4;

    private readonly GUIListBox _listBox;
    private readonly ViewLoadContext _view;
    private readonly List<BuiltRow> _rows = [];

    private readonly Dictionary<object, BuiltRow> _spare = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<object> _spareOrder = new();
    private readonly List<object> _index = [];
    private readonly Dictionary<Type, Templating.DataTemplate> _templates = [];
    private readonly List<BuiltRow> _recycled = [];

    private IEnumerable? _items;
    private INotifyCollectionChanged? _observed;
    private string? _templateKey;
    private bool _rebuildQueued;
    private bool _retriedEmpty;
    private bool _virtual;
    private bool _probed;
    private int _rowHeight;
    private int _windowFirst = -1;
    private int _windowHeight;
    private int _windowSpacing = -1;
    private float _lastScrolled;
    private GUIFrame? _spacerTop;
    private GUIFrame? _spacerBottom;

    private readonly int _bands;
    private readonly string? _tileKey;
    private readonly List<BuiltRow?> _slots = [];
    private readonly List<BuiltRow> _topTiles = [];
    private readonly List<BuiltRow> _bottomTiles = [];
    private readonly Dictionary<Type, Templating.DataTemplate> _tileTemplates = [];

    private GUIFrame? _bandPad;
    private GUIFrame? _bandTop;
    private GUIFrame? _bandBottom;
    private GUIFrame? _bandTail;

    // The bands are placed by spacer heights the host lays out in order, so the whole content has to keep the length
    // the scroll bar maps onto the items: every band calculation below counts the same children every frame.
    private int _bandSignature;
    private int _geometryWidth = -1;
    private int _geometryViewport = -1;
    private int _geometryRowHeight = -1;
    private int _geometrySpacing = -1;
    private int _geometryCount = -1;

    private int _columns = 1;
    private int _cellWidth;
    private int _nominal = 1;
    private int _minCell;
    private int _maxRows;
    private int _rowsOne;
    private bool _usable;
    private int _above;
    private int _below;

    private IItemDropTarget? _dropTarget;
    private IListBackground? _background;

    private object? _candidate;
    private object? _dragged;
    private Point _grabOffset;
    private bool _dragging;
    private object? _highlight;
    private DropMode _highlightMode;
    private float _proximity;
    private Rectangle? _dropRect;

    private LabelSkin? _draggedSkin;
    private LabelSkin? _highlightSkin;

    public ListBoxElement(ElementContext context)
        : base(new GUIListBox(context.Rect(context.Parent, 1f, 1f), style: null!)
        {
            HoverCursor = CursorState.Default
        })
    {
        _listBox = (GUIListBox)Control;
        _view = context.View;
        _templateKey = context.Text("ItemTemplate");
        _tileKey = context.Text("TileTemplate");
        _bands = int.TryParse(context.Text("Rows"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rows)
            ? Math.Max(0, rows)
            : 0;

        // Without a tile template the bands would fall back to the row template by type and the two would share
        // their recycled rows: the list shows the detail rows alone instead.
        if (_bands > 0 && string.IsNullOrWhiteSpace(_tileKey))
        {
            _bands = 0;
            context.View.Diagnostics.Report($"'{context.Node.Name}' asks for {rows} rows without a TileTemplate", context.Node);
        }

        _virtual = ViewMarkup.ToBool(context.Text("Virtual"), false) || _bands > 0;

        _listBox.Spacing = UiMetrics.DipInt(context.Metric("Spacing", UiMetrics.Gap));

        _listBox.Padding = new Vector4(UiMetrics.Dip(context.Metric("Padding", 0f)));

        if (_virtual) { _view.EveryFrame(UpdateWindow); }

        if (ViewMarkup.ToBool(context.Text("Draggable"), false))
        {
            _view.EveryFrame(UpdateDrag);
            _view.Overlays.Add(DrawDropIndicator);
        }

        if (ViewMarkup.ToBool(context.Text("BackgroundMenu"), false))
        {
            _view.EveryFrame(UpdateBackgroundClick);
        }
    }

    [ElementProperty]
    public IEnumerable? Items
    {
        set
        {
            if (_virtual && _items is not null && ReferenceEquals(value, _items)) { return; }

            if (_observed is not null) { _observed.CollectionChanged -= OnCollectionChanged; }

            _items = value;
            _observed = value as INotifyCollectionChanged;
            _observed?.CollectionChanged += OnCollectionChanged;

            _dropTarget = DataContext as IItemDropTarget;
            _background = DataContext as IListBackground;

            Rebuild();
        }
    }

    [ElementProperty]
    public string? ItemTemplate
    {
        set
        {
            _templateKey = value;
            _templates.Clear();
        }
    }

    [ElementProperty]
    public float Spacing { set => _listBox.Spacing = UiMetrics.DipInt(value); }

    internal override void AddContent(ViewElement child) => throw new NotSupportedException("List takes no content");

    public void Dispose()
    {
        if (_observed is not null) { _observed.CollectionChanged -= OnCollectionChanged; }

        _observed = null;
        ResetDrag();

        for (int i = 0; i < _rows.Count; i++) { Drop(_rows[i]); }

        foreach (BuiltRow row in _spare.Values) { Drop(row); }

        for (int i = 0; i < _recycled.Count; i++) { Drop(_recycled[i]); }

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] is { } slot) { Drop(slot); }
        }

        for (int i = 0; i < _topTiles.Count; i++) { Drop(_topTiles[i]); }

        for (int i = 0; i < _bottomTiles.Count; i++) { Drop(_bottomTiles[i]); }

        _rows.Clear();
        _slots.Clear();
        _topTiles.Clear();
        _bottomTiles.Clear();
        _spare.Clear();
        _spareOrder.Clear();
        _recycled.Clear();

        ReleaseSpacers();
        ReleaseBands();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_rebuildQueued) { return; }

        _rebuildQueued = true;
        _view.Once(() =>
        {
            if (!_rebuildQueued) { return; }

            _rebuildQueued = false;
            Rebuild();
        });
    }

    private void Rebuild()
    {
        if (_bands > 0) { RebuildBands(); }
        else if (_virtual) { RebuildWindow(); }
        else if (Extends()) { Append(); }
        else { RebuildAll(); }

        bool empty = _bands > 0 ? _slots.Count == 0 || _slots[0] is null : _rows.Count == 0;

        if (empty && HasItems() && !_retriedEmpty)
        {
            _retriedEmpty = true;
            _view.Once(Rebuild);
        }
        else if (!empty)
        {
            _retriedEmpty = false;
        }

        InvalidateLayout();

        // The host list puts its children in place on its next update; asking for it here means the rebuilt rows are
        // already where they belong when the frame is drawn.
        _listBox.RecalculateChildren();
    }

    private bool Extends()
    {
        if (_items is null) { return false; }

        int index = 0;

        foreach (object item in _items)
        {
            if (index == _rows.Count) { return true; }

            if (!ReferenceEquals(_rows[index].Item, item)) { return false; }

            index++;
        }

        return index >= _rows.Count;
    }

    private void Append()
    {
        int skip = _rows.Count;
        int index = 0;

        foreach (object item in _items ?? Array.Empty<object>())
        {
            index++;

            if (index <= skip) { continue; }

            if (Build(item) is { } built) { _rows.Add(built); }
        }
    }

    private void RebuildAll()
    {
        List<BuiltRow> previous = [.. _rows];

        for (int i = 0; i < previous.Count; i++) { previous[i].Element.Control.RectTransform.Parent = null; }

        _rows.Clear();
        _listBox.Content.ClearChildren();

        int cursor = 0;

        try
        {
            foreach (object item in _items ?? Array.Empty<object>())
            {
                BuiltRow? kept = null;

                while (cursor < previous.Count)
                {
                    BuiltRow candidate = previous[cursor++];

                    if (ReferenceEquals(candidate.Item, item)) { kept = candidate; break; }

                    Keep(candidate);
                }

                if (kept is null && _spare.Remove(item, out BuiltRow? pooled))
                {
                    pooled.Element.Control.Visible = true;
                    pooled.Scope.Resume();
                    kept = pooled;
                }

                if (kept is not null)
                {
                    kept.Element.Control.RectTransform.Parent = _listBox.Content.RectTransform;
                    _rows.Add(kept);
                    continue;
                }

                if (Build(item) is { } built) { _rows.Add(built); }
            }
        }
        finally
        {
            while (cursor < previous.Count) { Keep(previous[cursor++]); }
        }
    }

    // A windowed list keeps only the rows around the viewport and stands in for the rest with two spacers, so the
    // host list box lays out tens of children instead of thousands. The spacer heights are picked so the host's own
    // total size, and with it the scrollbar range, matches the unwindowed list exactly.
    private void RebuildWindow()
    {
        if (_rows.Count > 0) { _rowHeight = _rows[0].Element.Control.Rect.Height; }

        ReleaseWindow();
        RefreshIndex();

        _probed = false;

        if (_index.Count == 0) { return; }

        ApplyWindow(force: true);
    }

    private void RefreshIndex()
    {
        _index.Clear();

        if (_items is null) { return; }

        foreach (object item in _items) { _index.Add(item); }
    }

    private void UpdateWindow()
    {
        if (_dragging || _rebuildQueued) { return; }

        if (_bands > 0) { ApplyBands(force: false); return; }

        ApplyWindow(force: false);
    }

    private void ApplyWindow(bool force)
    {
        int count = _index.Count;

        if (count == 0)
        {
            ReleaseWindow();
            return;
        }

        if (_rowHeight <= 0 && _rows.Count == 0 && !Probe()) { return; }

        int height = _rows.Count > 0 ? _rows[0].Element.Control.Rect.Height : _rowHeight;
        int viewport = _listBox.Content.Rect.Height;
        int spacing = _listBox.Spacing;

        if (height <= 0 || viewport <= 0) { return; }

        _rowHeight = height;

        int stride = height + spacing;
        float scrolled = _listBox.BarSize < 1f ? Math.Max(0f, _listBox.TotalSize - viewport) * _listBox.BarScroll : 0f;

        // While the wheel is spinning the list is scrolled faster than rows could be shown, so the margin only has
        // to cover the viewport: a smaller window means fewer rows to shuffle and reprocess on each frame.
        int margin = Math.Abs(scrolled - _lastScrolled) > WindowOverscan * stride ? 1 : WindowOverscan;

        _lastScrolled = scrolled;

        int first = Math.Clamp((int)(scrolled / stride), 0, count - 1);
        int last = Math.Clamp((int)((scrolled + viewport) / stride) + 1, first, count - 1);
        int targetFirst = Math.Max(0, first - margin);
        int targetLast = Math.Min(count - 1, last + margin);

        if (!force && height == _windowHeight && spacing == _windowSpacing
            && _windowFirst >= 0 && _windowFirst <= targetFirst && _windowFirst + _rows.Count - 1 >= targetLast)
        {
            return;
        }

        BuildWindow(targetFirst, targetLast, height, spacing);
    }

    private bool Probe()
    {
        if (_probed || _index.Count == 0) { return false; }

        _probed = true;

        if (Build(_index[0]) is not { } row) { return false; }

        _rows.Add(row);
        _windowFirst = 0;
        _rowHeight = row.Element.Control.Rect.Height;

        return true;
    }

    private void BuildWindow(int first, int last, int height, int spacing)
    {
        List<BuiltRow?> previous = [.. _rows];
        List<BuiltRow> next = new(last - first + 1);
        int failed = 0;

        for (int index = first; index <= last; index++)
        {
            object item = _index[index];
            BuiltRow? row = null;
            int position = index - _windowFirst;

            if (_windowFirst >= 0 && position >= 0 && position < previous.Count
                && previous[position] is { } candidate && ReferenceEquals(candidate.Item, item))
            {
                previous[position] = null;
                row = candidate;
            }

            row ??= Take(item);

            if (row is null) { failed++; continue; }

            next.Add(row);
        }

        if (failed > 0)
        {
            Release(next);
            Release(previous);
            ReleaseSpacers();

            _rows.Clear();
            _windowFirst = -1;

            if (next.Count == 0) { return; }

            _virtual = false;

            for (int i = 0; i < _recycled.Count; i++) { Drop(_recycled[i]); }

            _recycled.Clear();

            _view.Diagnostics.Report("the list cannot be windowed: not every row could be built", Node);
            RebuildAll();
            return;
        }

        Release(previous);

        _rows.Clear();
        _rows.AddRange(next);
        _windowFirst = first;
        _windowHeight = height;
        _windowSpacing = spacing;
        _rowHeight = height;

        ArrangeChildren();
    }

    private void ArrangeChildren()
    {
        ReleaseSpacers();

        for (int i = 0; i < _rows.Count; i++) { _rows[i].Element.Control.RectTransform.Parent = null; }

        int spacing = _listBox.Spacing;
        int stride = _rowHeight + spacing;
        int width = Math.Max(1, _listBox.Content.Rect.Width);

        if (_windowFirst > 0) { _spacerTop = CreateSpacer(width, _windowFirst * stride - spacing); }

        for (int i = 0; i < _rows.Count; i++) { _rows[i].Element.Control.RectTransform.Parent = _listBox.Content.RectTransform; }

        int after = _index.Count - _windowFirst - _rows.Count;

        if (after > 0) { _spacerBottom = CreateSpacer(width, after * stride - spacing); }
    }

    private GUIFrame CreateSpacer(int width, int height)
    {
        GUIFrame spacer = new(
            new RectTransform(new Point(width, height), _listBox.Content.RectTransform, Anchor.TopLeft, Pivot.TopLeft, ScaleBasis.Normal, isFixedSize: true),
            style: null)
        {
            CanBeFocused = false
        };

        spacer.RectTransform.SetPosition(Anchor.TopLeft, Pivot.TopLeft);

        return spacer;
    }

    private void ReleaseSpacers()
    {
        if (_spacerTop is { } top)
        {
            top.RectTransform.Parent = null;
            _spacerTop = null;
        }

        if (_spacerBottom is { } bottom)
        {
            bottom.RectTransform.Parent = null;
            _spacerBottom = null;
        }
    }

    // A banded list splits its viewport into three pinned parts: a grid of tiles above, the detail rows in the middle
    // and a grid below, each showing the items right outside the detail window. All three live in the same host list,
    // so the wheel, the scroll bar, the clipping and the input clamping stay the host's own; spacer heights are what
    // puts the parts where they belong. The tail spacer keeps the content as long as the scroll bar expects for every
    // scroll position, so one stride of the bar is one item of the list.
    private void RebuildBands()
    {
        if (_slots.Count > 0 && _slots[0] is { } probe) { _rowHeight = probe.Element.Control.Rect.Height; }

        Release(_slots);
        Release(_topTiles);
        Release(_bottomTiles);

        _rows.Clear();
        _slots.Clear();
        _topTiles.Clear();
        _bottomTiles.Clear();

        ReleaseSpacers();
        RefreshIndex();

        _probed = false;

        if (_index.Count == 0) { return; }

        ApplyBands(force: true);
    }

    private void ApplyBands(bool force)
    {
        int count = _index.Count;

        if (count == 0)
        {
            ReleaseSpacers();
            ReleaseBands();

            _bandSignature = 0;
            return;
        }

        int spacing = _listBox.Spacing;
        int viewport = _listBox.Content.Rect.Height;
        int width = _listBox.Content.Rect.Width;

        if (width <= 0 || viewport <= 0)
        {
            _bandSignature = 0;
            return;
        }

        if (_slots.Count == 0 || _slots[0] is null)
        {
            if (!ProbeSlot())
            {
                _bandSignature = 0;
                return;
            }
        }

        int height = _slots[0] is { } first ? first.Element.Control.Rect.Height : _rowHeight;

        if (height <= 0)
        {
            _bandSignature = 0;
            return;
        }

        _rowHeight = height;

        bool tiles = UpdateGeometry(width, viewport, spacing, height, count);
        int rows = tiles ? _rowsOne : _bands;
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
        int cells = _columns * _nominal;
        int aboveItems = Math.Min(cells, window);
        int belowItems = Math.Max(0, count - window - _maxRows);
        int usedTop = topTiles ? Math.Clamp((aboveItems + _columns - 1) / _columns, 1, _nominal) : 0;
        int usedBottom = bottomTiles ? Math.Clamp((belowItems + _columns - 1) / _columns, 1, _nominal) : 0;
        int pitch = _cellWidth + spacing;
        int wantTop = usedTop > 0 ? usedTop * pitch - spacing : 0;
        int wantBottom = usedBottom > 0 ? usedBottom * pitch - spacing : 0;
        int minTop = usedTop > 0 ? usedTop * _minCell + (usedTop - 1) * spacing : 0;
        int minBottom = usedBottom > 0 ? usedBottom * _minCell + (usedBottom - 1) * spacing : 0;
        int bands = (topTiles ? 1 : 0) + (bottomTiles ? 1 : 0);
        int gaps = bands == 2 ? 2 * spacing : spacing;

        // The detail rows take what is left once each band has the height its own items call for, rounded off: a band
        // that fills one row hands over one row, and a band that fills two keeps two, so the grids stay as full as
        // they were and only the rounding half row is made up out of the bands.
        int shown = bands == 0
            ? Math.Min(count - window, _maxRows)
            : Math.Clamp((int)MathF.Round((viewport - gaps - wantTop - wantBottom + spacing) / (float)stride), _bands, _maxRows);

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

        BandShape topShape = topTiles ? GridOf(topHeight, usedTop) : BandShape.None;
        BandShape bottomShape = bottomTiles ? GridOf(bottomHeight, usedBottom) : BandShape.None;
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

        if (!force && signature == _bandSignature) { return; }

        _bandSignature = signature;

        RefreshSlots(shown, window);
        if (tiles) { RefreshTiles(count, window, shown, topTiles, bottomTiles, topShape, bottomShape); }

        ArrangeBands(pad, tail, shown, width, topTiles, bottomTiles, topShape, bottomShape);

        _listBox.ScrollBarNeedsRecalculation = true;
    }

    // The layout is worked out per frame, so this only keeps what a size, a row height and a list length fix for good:
    // the columns a grid has, the rows a band is meant to hold, and the bounds the viewport puts on the detail rows.
    private bool UpdateGeometry(int width, int viewport, int spacing, int height, int count)
    {
        if (_geometryWidth == width && _geometryViewport == viewport && _geometrySpacing == spacing
            && _geometryRowHeight == height && _geometryCount == count)
        {
            return _usable;
        }

        _geometryWidth = width;
        _geometryViewport = viewport;
        _geometrySpacing = spacing;
        _geometryRowHeight = height;
        _geometryCount = count;

        int tile = UiMetrics.DipInt(UiMetrics.TileSize);
        int stride = Math.Max(1, height + spacing);

        _columns = Math.Max(1, (int)MathF.Round((width + spacing) / (float)Math.Max(1, tile + spacing)));
        _cellWidth = Math.Max(1, (width - (_columns - 1) * spacing) / _columns);
        _minCell = Math.Max(12, tile * 3 / 5);
        _nominal = GridRows((viewport - _bands * height - (_bands + 1) * spacing) / 2, spacing, _cellWidth, int.MaxValue);
        _maxRows = Math.Max(_bands, (viewport - spacing - _minCell) / stride);
        _usable = viewport >= 3 * _minCell;

        // The rows one band alone leaves for the detail rows: it sets the scroll range and with it which sides have
        // items at all. What the rows are laid out as is worked out per frame from what each band really shows.
        int items = Math.Max(0, count - _bands);
        int used = Math.Max(1, Math.Min(_nominal, (items + _columns - 1) / _columns));

        _rowsOne = Math.Clamp(_bands + _nominal + (_nominal - used), _bands, _maxRows);

        return _usable;
    }

    // The rows a band of this height holds at the cell width the grid has, capped by the rows the items actually fill:
    // fewer rows means taller cells, which is what keeps a band with little to show filling its own space.
    private static int GridRows(int band, int spacing, int cellWidth, int cap)
        => Math.Clamp((int)MathF.Round((band + spacing) / (float)Math.Max(1, cellWidth + spacing)), 1, Math.Max(1, cap));

    private BandShape GridOf(int height, int used)
    {
        int rows = GridRows(height, _geometrySpacing, _cellWidth, used);

        return new BandShape(height, rows, Math.Max(1, (height - (rows - 1) * _geometrySpacing) / rows), _usable ? _columns * rows : 0);
    }

    private void RefreshSlots(int shown, int window)
    {
        EnsureSlots(shown);

        for (int i = 0; i < _slots.Count; i++)
        {
            object item = _index[window + i];

            if (_slots[i] is not { } row || !ReferenceEquals(row.Item, item))
            {
                if (_slots[i] is { } stale) { Keep(stale); }

                _slots[i] = Take(item);
                continue;
            }

            row.Element.Control.Visible = true;
        }
    }

    private void RefreshTiles(int count, int window, int shown, bool topTiles, bool bottomTiles, BandShape topShape, BandShape bottomShape)
    {
        if (topTiles)
        {
            _bandTop ??= CreateFrame();
            _above = Math.Min(topShape.Capacity, window);
            FillBand(_topTiles, _bandTop, _above, window - _above, topShape.Capacity);
        }
        else
        {
            _above = 0;
        }

        if (bottomTiles)
        {
            _bandBottom ??= CreateFrame();
            _below = Math.Clamp(count - window - shown, 0, bottomShape.Capacity);
            FillBand(_bottomTiles, _bandBottom, _below, window + shown, bottomShape.Capacity);
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
        if (shown <= 0 || start >= _index.Count)
        {
            for (int i = 0; i < tiles.Count; i++) { tiles[i].Element.Control.Visible = false; }

            return;
        }

        EnsureTiles(tiles, band, _index[start], capacity);

        int count = Math.Min(shown, Math.Min(capacity, tiles.Count));

        for (int i = 0; i < count; i++)
        {
            object item = _index[start + i];
            BuiltRow tile = tiles[i];

            if (ReferenceEquals(tile.Item, item))
            {
                tile.Element.Control.Visible = true;
                continue;
            }

            Templating.DataTemplate? template = TileTemplate(item);

            if (template is not null && !ReferenceEquals(tile.Template, template))
            {
                Keep(tile);

                if (TakeTile(band, item) is { } swapped) { tiles[i] = swapped; continue; }

                tiles.RemoveAt(i);
                count = i;
                break;
            }

            tiles[i] = Reuse(tile, item);
        }

        for (int i = count; i < tiles.Count; i++) { tiles[i].Element.Control.Visible = false; }
    }

    private void EnsureSlots(int shown)
    {
        while (_slots.Count < shown) { _slots.Add(null); }

        while (_slots.Count > shown)
        {
            if (_slots[^1] is { } extra) { Keep(extra); }

            _slots.RemoveAt(_slots.Count - 1);
        }
    }

    private void EnsureTiles(List<BuiltRow> tiles, GUIFrame band, object seed, int capacity)
    {
        while (tiles.Count > capacity)
        {
            Drop(tiles[^1]);
            tiles.RemoveAt(tiles.Count - 1);
        }

        while (tiles.Count < capacity && TakeTile(band, seed) is { } tile) { tiles.Add(tile); }
    }

    // The banded parts are the only children of the content and they keep their slots, so nothing is created or
    // detached while scrolling: rows and tiles that were pooled elsewhere are the only parts that come and go, and
    // every part is moved to its slot in the order the host lays children out in before it positions them.
    private void ArrangeBands(int pad, int tail, int shown, int width, bool topTiles, bool bottomTiles, BandShape topShape, BandShape bottomShape)
    {
        GUIFrame padFrame = _bandPad ??= CreateFrame();
        GUIFrame tailFrame = _bandTail ??= CreateFrame();

        SizeFrame(padFrame, width, pad);
        SizeFrame(tailFrame, width, tail);

        padFrame.Visible = true;
        tailFrame.Visible = true;

        if (_bandTop is { } top)
        {
            SizeFrame(top, width, topShape.Height);
            top.Visible = topTiles;
        }

        if (_bandBottom is { } bottom)
        {
            SizeFrame(bottom, width, bottomShape.Height);
            bottom.Visible = bottomTiles;
        }

        int slot = 0;

        PlacePart(padFrame.RectTransform, slot++);

        if (topTiles && _bandTop is { } firstBand) { PlacePart(firstBand.RectTransform, slot++); }

        for (int i = 0; i < shown; i++)
        {
            if (_slots[i] is not { } row || !row.Element.Control.Visible) { continue; }

            PlacePart(row.Element.Control.RectTransform, slot++);
        }

        if (bottomTiles && _bandBottom is { } secondBand) { PlacePart(secondBand.RectTransform, slot++); }

        PlacePart(tailFrame.RectTransform, slot);

        if (topTiles) { PlaceTiles(_topTiles, _bandTop, _above, seamAtBottom: true, topShape.Rows, topShape.CellHeight); }
        if (bottomTiles) { PlaceTiles(_bottomTiles, _bandBottom, _below, seamAtBottom: false, bottomShape.Rows, bottomShape.CellHeight); }

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
        int columns = Math.Max(1, _columns);
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
            transform.RelativeSize = new Vector2(_cellWidth / (float)bandWidth, cellHeight / (float)bandHeight);
            transform.AbsoluteOffset = new Point(column * (_cellWidth + _geometrySpacing), row * (cellHeight + _geometrySpacing));
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

    private void ReleaseBands()
    {
        Detach(_bandPad);
        Detach(_bandTop);
        Detach(_bandBottom);
        Detach(_bandTail);
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
        if (_probed || _index.Count == 0) { return false; }

        _probed = true;

        if (Build(_index[0]) is not { } row) { return false; }

        EnsureSlots(_bands);

        _slots[0] = row;
        _rowHeight = row.Element.Control.Rect.Height;

        return true;
    }

    private Templating.DataTemplate? TileTemplate(object item)
    {
        Type type = item.GetType();

        if (_tileTemplates.TryGetValue(type, out Templating.DataTemplate? cached)) { return cached; }

        Templating.DataTemplate? template = Templating.DataTemplate.Select(item, _tileKey, _view, this);
        if (template is not null) { _tileTemplates[type] = template; }

        return template;
    }

    // A pooled tile comes back detached, so it has to be hung on its band again before it is placed: a detached
    // control would stay in the host's update list (it is visible) and keep drawing itself at its old place.
    private BuiltRow? TakeTile(GUIFrame band, object item)
    {
        if (TileTemplate(item) is not { } template)
        {
            _view.Diagnostics.Report($"no tile template for an item of type '{item.GetType().Name}'", Node);
            return null;
        }

        BuiltRow? tile = TakeRecycled(template, item) ?? Build(item, template, band.RectTransform);
        if (tile is not null) { tile.Element.Control.RectTransform.Parent = band.RectTransform; }

        return tile;
    }

    private void ReleaseWindow()
    {
        Release(_rows);

        _rows.Clear();
        _windowFirst = -1;

        ReleaseSpacers();
    }

    private void Release(IEnumerable<BuiltRow?> rows)
    {
        foreach (BuiltRow? row in rows)
        {
            if (row is { } value) { Keep(value); }
        }
    }

    private BuiltRow? Build(object item)
        => Template(item) is { } template ? Build(item, template) : ReportMissingTemplate(item);

    private BuiltRow? ReportMissingTemplate(object item)
    {
        _view.Diagnostics.Report($"no template for an item of type '{item.GetType().Name}'", Node);
        return null;
    }

    private BuiltRow? Build(object item, Templating.DataTemplate template, RectTransform? parent = null)
    {
        OwnershipScope scope = _view.BeginScope();
        ViewElement row;

        try
        {
            row = template.Build(_view, item, parent ?? _listBox.Content.RectTransform, this);
        }
        catch (Exception e)
        {
            scope.Dispose();
            _view.Diagnostics.Report($"row for '{item.GetType().Name}' could not be built ({e.Message})", Node);
            return null;
        }
        finally
        {
            _view.EndScope(scope);
        }

        row.Control.RectTransform.SetPosition(Anchor.TopLeft, Pivot.TopLeft);

        // A fresh row is laid out on the spot: waiting for its first update would show it for a frame with its
        // labels and controls still sitting at their default places.
        row.Control.ForceLayoutRecalculation();

        row.Parent = this;
        row.Control.UserData = item;

        return new BuiltRow(item, row, row.Control as GUIButton, scope, template);
    }

    private Templating.DataTemplate? Template(object item)
    {
        Type type = item.GetType();

        if (_templates.TryGetValue(type, out Templating.DataTemplate? cached)) { return cached; }

        Templating.DataTemplate? template = Templating.DataTemplate.Select(item, _templateKey, _view, this);
        if (template is not null) { _templates[type] = template; }

        return template;
    }

    private BuiltRow? Take(object item)
        => Template(item) is { } template
            ? TakeRecycled(template, item) ?? Build(item, template)
            : ReportMissingTemplate(item);

    private BuiltRow? TakeRecycled(Templating.DataTemplate template, object item)
    {
        for (int i = _recycled.Count - 1; i >= 0; i--)
        {
            BuiltRow row = _recycled[i];
            if (!ReferenceEquals(row.Template, template)) { continue; }

            _recycled.RemoveAt(i);
            return Reuse(row, item);
        }

        return null;
    }

    // A recycled row keeps its components and only gets the new item: re-pointing the bindings is what makes the
    // list cheap to scroll, as building a row for every item the wheel rolls past is the expensive part.
    private static BuiltRow Reuse(BuiltRow row, object item)
    {
        row.Element.DataContext = item;
        row.Scope.Retarget(item);
        row.Element.Control.UserData = item;
        row.Element.Control.Visible = true;
        row.Scope.Resume();

        return row with { Item = item };
    }

    private void Pool(BuiltRow row)
    {
        _recycled.Add(row);

        while (_recycled.Count > SpareLimit)
        {
            Drop(_recycled[0]);
            _recycled.RemoveAt(0);
        }
    }

    private static void Drop(BuiltRow row)
    {
        row.Element.Control.Visible = false;
        row.Element.Control.RectTransform.Parent = null;
        row.Scope.Dispose();
    }

    private void Keep(BuiltRow row)
    {
        row.Element.Control.RectTransform.Parent = null;
        row.Element.Control.Visible = false;
        row.Scope.Pause();

        if (_virtual)
        {
            Pool(row);
            return;
        }

        if (_spare.ContainsKey(row.Item))
        {
            Drop(row);
            return;
        }

        _spare[row.Item] = row;
        _spareOrder.Enqueue(row.Item);

        while (_spareOrder.Count > SpareLimit)
        {
            if (_spare.Remove(_spareOrder.Dequeue(), out BuiltRow? oldest)) { Drop(oldest); }
        }
    }

    private bool HasItems()
    {
        if (_items is null) { return false; }

        IEnumerator enumerator = _items.GetEnumerator();

        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    private void InvalidateLayout()
    {
        _listBox.ChildrenNeedRecalculation = true;
        _listBox.ScrollBarNeedsRecalculation = true;
        _listBox.DimensionsNeedRecalculation = true;
    }

    private GUIButton? RowOf(object? item)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (ReferenceEquals(_rows[i].Element.DataContext, item)) { return _rows[i].Button; }
        }

        return null;
    }

    private object? ItemAt(Vector2 mouse)
    {
        Point point = mouse.ToPoint();
        Rectangle viewport = _listBox.Rect;

        if (!viewport.Contains(point)) { return null; }

        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_rows[i].Item, _dragged)) { continue; }

            GUIComponent component = _rows[i].Element.Control;
            if (!component.Visible) { continue; }
            if (!viewport.Intersects(component.Rect)) { continue; }
            if (component.Rect.Contains(point)) { return _rows[i].Item; }
        }

        return null;
    }

    private void UpdateBackgroundClick()
    {
        if (_view.IsInputBlocked() || !PlayerInput.SecondaryMouseButtonClicked()) { return; }

        Vector2 mouse = PlayerInput.MousePosition;

        // The menu belongs to the list: without this the click would be read anywhere on the screen, as "not on an
        // item" is also true for every point outside the list.
        if (!_listBox.Rect.Contains(mouse.ToPoint())) { return; }
        if (ItemAt(mouse) is not null) { return; }

        _background?.ShowBackgroundMenu(mouse);
    }

    private void UpdateDrag()
    {
        if (_dropTarget is null || _view.IsInputBlocked()) { return; }

        Vector2 mouse = PlayerInput.MousePosition;

        if (_dragging) { ContinueDrag(mouse); return; }

        if (!_listBox.Rect.Contains(mouse.ToPoint()))
        {
            _candidate = null;
            return;
        }

        if (PlayerInput.PrimaryMouseButtonDown())
        {
            _candidate = ItemAt(mouse);
            _grabOffset = mouse.ToPoint() - (RowOf(_candidate)?.Rect.Location ?? Point.Zero);
        }

        if (!PlayerInput.PrimaryMouseButtonHeld())
        {
            _candidate = null;
            return;
        }

        if (_candidate is null || !_dropTarget.CanDrag(_candidate) || RowOf(_candidate) is not { } row) { return; }

        Point delta = mouse.ToPoint() - row.Rect.Location - _grabOffset;
        int threshold = UiMetrics.DipInt(DragThreshold);
        if (Math.Abs(delta.X) < threshold && Math.Abs(delta.Y) < threshold) { return; }

        _dragging = true;
        _dragged = _candidate;
        _draggedSkin = LabelSkin.Of(row.TextBlock);
        row.TextBlock.OverrideTextColor(UiMetrics.Text * 0.45f);
        _listBox.DraggedElement = row;
    }

    private void ContinueDrag(Vector2 mouse)
    {
        if (!PlayerInput.PrimaryMouseButtonHeld())
        {
            CompleteDrag(mouse);
            return;
        }

        if (_dragged is null || RowOf(_dragged) is not { } row)
        {
            ResetDrag();
            return;
        }

        row.RectTransform.AbsoluteOffset = mouse.ToPoint() - _listBox.Content.Rect.Location - _grabOffset;

        object? target = ItemAt(mouse);
        if (target is null || ReferenceEquals(target, _dragged))
        {
            SetHighlight(null, DropMode.After, 0f, Rectangle.Empty);
            return;
        }

        Rectangle rect = RowOf(target)?.Rect ?? Rectangle.Empty;
        DropMode mode = ResolveDropMode(target, mouse);
        float distance = rect.Height > 0 ? Math.Abs(mouse.Y - (rect.Top + rect.Height / 2f)) / (rect.Height * 1.5f) : 1f;

        SetHighlight(target, mode, Math.Clamp(1f - distance, 0f, 1f), rect);
    }

    private void CompleteDrag(Vector2 mouse)
    {
        object? source = _dragged;
        object? target = ItemAt(mouse);
        DropMode mode = target is null ? DropMode.After : ResolveDropMode(target, mouse);

        ResetDrag();

        if (source is null || target is null)
        {
            if (source is not null) { _dropTarget?.Drop(source, null, DropMode.After); }
            return;
        }

        if (ReferenceEquals(source, target)) { return; }
        _dropTarget?.Drop(source, target, mode);
    }

    private DropMode ResolveDropMode(object target, Vector2 mouse)
    {
        Rectangle rect = RowOf(target)?.Rect ?? Rectangle.Empty;
        float relativeY = rect.Height > 0 ? (mouse.Y - rect.Top) / rect.Height : 0.5f;

        if (_dropTarget?.CanNest(target) == true && relativeY is > 0.25f and < 0.75f) { return DropMode.Nest; }
        return relativeY < 0.5f ? DropMode.Before : DropMode.After;
    }

    private void SetHighlight(object? item, DropMode mode, float proximity, Rectangle rect)
    {
        if (ReferenceEquals(_highlight, item) && _highlightMode == mode && Math.Abs(_proximity - proximity) < 0.01f && _dropRect == rect)
        {
            return;
        }

        if (!ReferenceEquals(_highlight, item) || _highlightMode != mode)
        {
            RestoreHighlight();
            _highlight = item;
            _highlightMode = mode;
        }

        _proximity = proximity;
        _dropRect = item is null ? null : rect;

        if (RowOf(item)?.TextBlock is not { } label) { return; }

        _highlightSkin ??= LabelSkin.Of(label);

        label.OverrideTextColor(Color.Lerp(UiMetrics.Accent * 0.7f, Color.White, proximity));
        label.TextScale = UiMetrics.TextScale * (1f + 0.09f * proximity);
    }

    private void RestoreHighlight()
    {
        Restore(_highlightSkin, RowOf(_highlight));
        _highlightSkin = null;

        _highlight = null;
        _highlightMode = DropMode.After;
        _proximity = 0f;
        _dropRect = null;
    }

    private static void Restore(LabelSkin? skin, GUIButton? row)
    {
        if (skin is not { } saved || row?.TextBlock is not { } label) { return; }

        saved.ApplyTo(label);
    }

    private void ResetDrag()
    {
        RestoreHighlight();
        Restore(_draggedSkin, RowOf(_dragged));
        _draggedSkin = null;

        _dragging = false;
        _dragged = null;
        _candidate = null;
        _listBox.DraggedElement = null;

        _listBox.ChildrenNeedRecalculation = true;
    }

    private void DrawDropIndicator(SpriteBatch spriteBatch)
    {
        if (!_dragging || _dropRect is not { Width: > 0 } rect) { return; }

        int thickness = UiMetrics.DipInt(2f);
        int half = Math.Max(1, rect.Height / 2);
        int indent = UiMetrics.DipInt(UiMetrics.TreeIndentStep);

        Rectangle slot = _highlightMode switch
        {
            DropMode.Before => new Rectangle(rect.X, rect.Y, rect.Width, half),
            DropMode.After => new Rectangle(rect.X, rect.Y + half, rect.Width, Math.Max(1, rect.Height - half)),
            _ => new Rectangle(rect.X + indent, rect.Y, Math.Max(1, rect.Width - indent), rect.Height)
        };

        GUI.DrawRectangle(spriteBatch, slot, Color.White * (0.35f + 0.3f * _proximity), false, 0f, thickness);
    }

    // What one grid band is this frame: how tall it is, how many tile rows it shows, how tall a cell is and how many
    // tiles it holds. The detail rows get as many whole rows as the viewport has left over, and each band gets its
    // share of the rest by how many rows it actually fills, so a band with little to show never keeps empty rows.
    private readonly record struct BandShape(int Height, int Rows, int CellHeight, int Capacity)
    {
        internal static BandShape None { get; } = new(0, 1, 1, 0);
    }

    private sealed record BuiltRow(object Item, ViewElement Element, GUIButton? Button, OwnershipScope Scope, Templating.DataTemplate Template);

    private readonly record struct LabelSkin(
        Color Text,
        Color Hover,
        Color Pressed,
        Color Selected,
        Color HoverSelected,
        float Scale)
    {
        internal static LabelSkin Of(GUITextBlock label) => new(
            label.TextColor,
            label.HoverTextColor,
            label.PressedTextColor,
            label.SelectedTextColor,
            label.HoverSelectedTextColor,
            label.TextScale);

        internal void ApplyTo(GUITextBlock label)
        {
            label.TextColor = Text;
            label.HoverTextColor = Hover;
            label.PressedTextColor = Pressed;
            label.SelectedTextColor = Selected;
            label.HoverSelectedTextColor = HoverSelected;
            label.TextScale = Scale;
        }
    }
}
