using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace UiFramework.Controls;

[Element("List")]
internal sealed class ListBoxElement : ViewElement, IPropertyObserver, IDisposable
{
    private readonly GUIListBox _listBox;
    private readonly ViewContext _view;
    private readonly ListRowPool _rowPool;
    private readonly VirtualListWindow _window;
    private readonly ListBandLayout _bands;
    private readonly ListDrag _drag;
    private readonly List<BuiltRow> _rows = [];

    private readonly List<object> _index = [];
    private IEnumerable? _items;
    private INotifyCollectionChanged? _observed;
    private bool _rebuildQueued;
    private bool _retriedEmpty;
    private bool _probed;
    private int _rowHeight;

    private Action<object?>? _reportScroll;
    private float _reportedScroll = -1f;

    private readonly int _detailRows;
    private readonly Insets _padding;

    public ListBoxElement(ElementContext context)
        : base(new GUIListBox(context.Rect(context.Parent, 1f, 1f), style: null!)
        {
            HoverCursor = CursorState.Default
        })
    {
        _listBox = (GUIListBox)Control;
        _view = context.View;

        string? rowKey = context.Text("ItemTemplate");
        string? tileKey = context.Text("TileTemplate");
        _rowPool = new ListRowPool(_view, this, _listBox, rowKey, tileKey);
        _window = new VirtualListWindow(this, _rowPool);
        _bands = new ListBandLayout(this, _rowPool);
        _drag = new ListDrag(this);

        _detailRows = int.TryParse(context.Text("Rows"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rows)
            ? Math.Max(0, rows)
            : 0;

        if (_detailRows > 0 && string.IsNullOrWhiteSpace(tileKey))
        {
            _detailRows = 0;
            context.View.Diagnostics.Report($"'{context.Node.Name}' asks for {rows} rows without a TileTemplate", context.Node);
        }

        Windowed = ViewMarkup.ToBool(context.Text("Virtual"), false) || _detailRows > 0;

        _listBox.Spacing = UiMetrics.DipInt(context.Metric("Spacing", UiTokens.Dip("gap", 4f)));

        _padding = context.Padding;

        if (_detailRows == 0) { _listBox.Padding = _padding.ToVector4(); }

        if (Windowed) { _view.EveryFrame(UpdateWindow); }

        if (ViewMarkup.ToBool(context.Text("Draggable"), false))
        {
            _view.EveryFrame(_drag.Update);
            _view.Overlays.Add(_drag.Draw);
        }

        if (ViewMarkup.ToBool(context.Text("BackgroundMenu"), false))
        {
            _view.EveryFrame(_drag.UpdateBackgroundClick);
        }
    }

    internal bool Windowed { get; private set; }

    internal GUIListBox ListBox => _listBox;

    internal ViewContext View => _view;

    internal List<BuiltRow> Rows => _rows;

    internal List<object> Index => _index;

    internal int RowHeight { get => _rowHeight; set => _rowHeight = value; }

    internal bool Probed { get => _probed; set => _probed = value; }

    internal int DetailRows => _detailRows;

    internal Insets Padding => _padding;

    [ElementProperty]
    public IEnumerable? Items
    {
        set
        {
            if (Windowed && _items is not null && ReferenceEquals(value, _items)) { return; }

            if (_observed is not null) { _observed.CollectionChanged -= OnCollectionChanged; }

            _items = value;
            _observed = value as INotifyCollectionChanged;
            _observed?.CollectionChanged += OnCollectionChanged;

            _drag.Retarget(DataContext);

            Rebuild();
        }
    }

    [ElementProperty]
    public string? ItemTemplate
    {
        set => _rowPool.RowTemplateKey = value;
    }

    [ElementProperty(Mode = BindingMode.TwoWay)]
    public float Scroll
    {
        get => _listBox.BarScroll;
        set => _listBox.BarScroll = Math.Clamp(value, 0f, 1f);
    }

    void IPropertyObserver.Observe(string property, Action<object?> changed)
    {
        if (!string.Equals(property, nameof(Scroll), StringComparison.Ordinal)) { return; }

        _reportScroll = changed;
        _view.EveryFrame(ReportScroll);
    }

    private void ReportScroll()
    {
        if (_reportScroll is not { } report) { return; }

        float scroll = _listBox.BarScroll;

        if (Math.Abs(scroll - _reportedScroll) < 0.0001f) { return; }

        _reportedScroll = scroll;
        report(scroll);
    }

    [ElementProperty]
    public float Spacing { set => _listBox.Spacing = UiMetrics.DipInt(value); }

    public override void AddContent(ViewElement child) => throw new NotSupportedException("List takes no content");

    public void Dispose()
    {
        if (_observed is not null) { _observed.CollectionChanged -= OnCollectionChanged; }

        _observed = null;
        _reportScroll = null;
        _drag.Reset();

        for (int i = 0; i < _rows.Count; i++) { _rowPool.Drop(_rows[i]); }

        _rows.Clear();
        _rowPool.Clear();

        _window.ReleaseSpacers();
        _bands.DropAll();
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
        if (_detailRows > 0) { _bands.Rebuild(); }
        else if (Windowed) { _window.Rebuild(); }
        else if (Extends()) { Append(); }
        else { RebuildAll(); }

        bool empty = _detailRows > 0 ? _bands.IsEmpty : _rows.Count == 0;

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

            if (_rowPool.Build(item) is { } built) { _rows.Add(built); }
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

                    _rowPool.Keep(candidate);
                }

                if (kept is null && _rowPool.TryTakeBack(item, out BuiltRow? pooled)) { kept = pooled; }

                if (kept is not null)
                {
                    kept.Element.Control.RectTransform.Parent = _listBox.Content.RectTransform;
                    _rows.Add(kept);
                    continue;
                }

                if (_rowPool.Build(item) is { } built) { _rows.Add(built); }
            }
        }
        finally
        {
            while (cursor < previous.Count) { _rowPool.Keep(previous[cursor++]); }
        }
    }

    internal void RefreshIndex()
    {
        _index.Clear();

        if (_items is null) { return; }

        foreach (object item in _items) { _index.Add(item); }
    }

    internal void AbandonWindow()
    {
        Windowed = false;
        _rowPool.DropRecycled();

        _view.Diagnostics.Report("the list cannot be windowed: not every row could be built", Node);
        RebuildAll();
    }

    private void UpdateWindow()
    {
        if (_drag.IsDragging || _rebuildQueued) { return; }

        if (_detailRows > 0) { _bands.Update(); return; }

        _window.Update();
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
}
