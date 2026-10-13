using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using UiFramework.Templating;

namespace UiFramework.Controls;

[Element("TileGrid")]
internal sealed class TileGridElement : ViewElement, IPropertyObserver, IDisposable
{
    private const int Overscan = 2;
    private const int PoolLimit = 240;
    private const int MinCell = 12;

    private readonly GUIListBox _listBox;
    private readonly ViewContext _view;
    private readonly Dictionary<object, Cell> _live = new(ReferenceEqualityComparer.Instance);
    private readonly TemplatePool<Cell> _pool = new(PoolLimit, static cell => Drop(cell));
    private readonly List<object> _index = [];
    private readonly Dictionary<Type, DataTemplate> _templates = [];
    private readonly HashSet<object> _placed = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _stale = [];

    private readonly string? _templateKey;
    private int _columns;

    private IEnumerable? _items;
    private INotifyCollectionChanged? _observed;
    private bool _rebuildQueued;
    private GUIFrame? _sheet;
    private Point _sheetSize;
    private Action<object?>? _reportScroll;
    private float _reportedScroll = -1f;
    private int _signature;
    private (int Count, int Width, int Height, int Spacing, int Columns, float Scroll) _inputs;
    private bool _hasInputs;

    public TileGridElement(ElementContext context)
        : base(new GUIListBox(context.Rect(context.Parent, 1f, 1f), style: null!)
        {
            HoverCursor = CursorState.Default
        })
    {
        _listBox = (GUIListBox)Control;
        _view = context.View;

        _templateKey = context.Text("TileTemplate");
        _columns = int.TryParse(context.Text("Columns"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int columns)
            ? Math.Max(1, columns)
            : 3;

        _listBox.Spacing = UiMetrics.DipInt(context.Metric("Spacing", UiTokens.Dip("gap", 4f)));
        _listBox.Padding = context.Padding.ToVector4();

        _listBox.KeepSpaceForScrollBar = true;
        _listBox.DimensionsNeedRecalculation = true;

        _view.EveryFrame(Update);
    }

    [ElementProperty]
    public IEnumerable? Items
    {
        set
        {
            if (ReferenceEquals(value, _items)) { return; }

            if (_observed is not null) { _observed.CollectionChanged -= OnCollectionChanged; }

            _items = value;
            _observed = value as INotifyCollectionChanged;
            _observed?.CollectionChanged += OnCollectionChanged;

            Rebuild();
        }
    }

    [ElementProperty(Mode = BindingMode.TwoWay)]
    public float Scroll
    {
        get => _listBox.BarScroll;
        set => _listBox.BarScroll = Math.Clamp(value, 0f, 1f);
    }

    [ElementProperty]
    public int Columns
    {
        set => _columns = Math.Max(1, value);
    }

    [ElementProperty]
    public float Spacing
    {
        set => _listBox.Spacing = UiMetrics.DipInt(value);
    }

    [ElementProperty(Phase = ApplyPhase.Construct)]
    internal string? TileTemplate => Node?.Text("TileTemplate");

    void IPropertyObserver.Observe(string property, Action<object?> changed)
    {
        if (!string.Equals(property, nameof(Scroll), StringComparison.Ordinal)) { return; }

        _reportScroll = changed;
        _view.EveryFrame(ReportScroll);
    }

    public void Dispose()
    {
        if (_observed is not null) { _observed.CollectionChanged -= OnCollectionChanged; }

        _observed = null;
        _items = null;
        _reportScroll = null;

        ReleaseAll();

        _pool.Clear();

        if (_sheet is { } sheet)
        {
            sheet.RectTransform.Parent = null;
            _sheet = null;
        }
    }

    private void ReportScroll()
    {
        if (_reportScroll is not { } report) { return; }

        float scroll = _listBox.BarScroll;

        if (Math.Abs(scroll - _reportedScroll) < 0.0001f) { return; }

        _reportedScroll = scroll;
        report(scroll);
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
        _index.Clear();

        if (_items is not null)
        {
            foreach (object item in _items)
            {
                if (item is not null) { _index.Add(item); }
            }
        }

        _signature = 0;
        _hasInputs = false;

        if (_listBox.BarScroll > 0f) { _listBox.BarScroll = 0f; }

        Update();
    }

    private void Update()
    {
        if (!Control.Visible || IsPaused) { return; }

        int count = _index.Count;

        if (count == 0)
        {
            _hasInputs = false;

            if (_sheetSize == Point.Zero) { return; }

            ReleaseAll();

            _sheetSize = Point.Zero;

            if (_sheet is { } hidden) { hidden.Visible = false; }

            _listBox.UpdateScrollBarSize();
            _listBox.RecalculateChildren();
            return;
        }

        Rectangle content = _listBox.Content.Rect;
        int width = Math.Max(1, content.Width);
        int height = Math.Max(1, content.Height);

        if (width < MinCell || height < MinCell) { return; }

        int spacing = _listBox.Spacing;
        (int Count, int Width, int Height, int Spacing, int Columns, float Scroll) inputs =
            (count, width, height, spacing, _columns, _listBox.BarScroll);

        if (_hasInputs && inputs == _inputs) { return; }

        _inputs = inputs;
        _hasInputs = true;

        int cell = Math.Max(MinCell, (width - (_columns - 1) * spacing) / _columns);
        int stride = cell + spacing;
        int rows = (count + _columns - 1) / _columns;
        int sheetHeight = Math.Max(1, rows * stride - spacing);

        GUIFrame sheet = Sheet();
        sheet.Visible = true;

        Point sheetSize = new(width, sheetHeight);

        if (_sheetSize != sheetSize)
        {
            _sheetSize = sheetSize;
            sheet.RectTransform.NonScaledSize = sheetSize;
            _listBox.UpdateScrollBarSize();
        }

        if (sheetHeight <= height && _listBox.BarScroll > 0f) { _listBox.BarScroll = 0f; }

        float scrolled = _listBox.BarSize < 1f ? Math.Max(0f, _listBox.TotalSize - height) * _listBox.BarScroll : 0f;

        int firstRow = Math.Clamp((int)(scrolled / stride), 0, rows - 1);
        int lastRow = Math.Clamp((int)((scrolled + height) / stride) + 1, firstRow, rows - 1);
        int first = Math.Max(0, (firstRow - Overscan) * _columns);
        int last = Math.Min(count - 1, (lastRow + Overscan + 1) * _columns - 1);

        int signature = HashCode.Combine(count, width, height, spacing, cell, first, rows);

        if (signature == _signature) { return; }

        _signature = signature;

        Arrange(first, last, cell, stride);
    }

    private void Arrange(int first, int last, int cell, int stride)
    {
        _placed.Clear();
        _stale.Clear();

        RectTransform sheet = Sheet().RectTransform;
        Point size = new(cell, cell);

        for (int index = first; index <= last; index++)
        {
            object item = _index[index];
            if (!_placed.Add(item)) { continue; }

            if (Take(item) is not { } tile) { continue; }

            RectTransform rect = tile.Element.Control.RectTransform;
            rect.Parent = sheet;
            rect.SetPosition(Anchor.TopLeft, Pivot.TopLeft);
            rect.MaxSize = RectTransform.MaxPoint;
            rect.MinSize = Point.Zero;
            rect.IsFixedSize = true;
            rect.NonScaledSize = size;
            rect.AbsoluteOffset = new Point(index % _columns * stride, index / _columns * stride);
            tile.Element.Control.Visible = true;
            tile.Element.Control.AutoUpdate = _view.Interactive;

            if (tile.Element is ITileCell sized) { sized.SetCellSize(cell, cell); }
        }

        foreach (object item in _live.Keys)
        {
            if (!_placed.Contains(item)) { _stale.Add(item); }
        }

        for (int i = 0; i < _stale.Count; i++)
        {
            if (_live.Remove(_stale[i], out Cell? kept)) { Keep(kept); }
        }

        _listBox.RecalculateChildren();
    }

    private Cell? Take(object item)
    {
        if (_live.TryGetValue(item, out Cell? live))
        {
            live.Scope.Resume();
            return live;
        }

        if (TemplateFor(item) is not { } template)
        {
            _view.Diagnostics.Report($"no tile template for an item of type '{item.GetType().Name}'", Node);
            return null;
        }

        if (_pool.Take(template) is { } pooled)
        {
            Cell reused = Reuse(pooled, item);
            _live[item] = reused;

            return reused;
        }

        if (Build(item, template) is not { } built) { return null; }

        _live[item] = built;
        return built;
    }

    private DataTemplate? TemplateFor(object item)
    {
        Type type = item.GetType();

        if (_templates.TryGetValue(type, out DataTemplate? cached)) { return cached; }

        DataTemplate? template = DataTemplate.Select(item, _templateKey, _view, this);

        if (template is not null) { _templates[type] = template; }

        return template;
    }

    private Cell? Build(object item, DataTemplate template)
    {
        ViewScope scope = _view.BeginScope();
        ViewElement element;

        try
        {
            element = template.Build(_view, item, Sheet().RectTransform, this);
        }
        catch (Exception e)
        {
            scope.Dispose();
            _view.Diagnostics.Report($"a tile for '{item.GetType().Name}' could not be built ({e.Message})", Node);
            return null;
        }
        finally
        {
            _view.EndScope(scope);
        }

        element.Control.UserData = item;

        return new Cell(item, element, scope, template);
    }

    private static Cell Reuse(Cell cell, object item)
    {
        if (!ReferenceEquals(cell.Item, item)) { cell.Element.ResetAnimatedValues(); }

        cell.Element.DataContext = item;
        cell.Element.Control.UserData = item;
        cell.Scope.Retarget(item);
        cell.Scope.Resume();

        return cell with { Item = item };
    }

    private void Keep(Cell cell)
    {
        cell.Element.Control.Visible = false;
        cell.Element.Control.RectTransform.Parent = null;
        cell.Scope.Pause();

        _pool.Add(cell.Template, cell);
    }

    private static void Drop(Cell cell)
    {
        cell.Element.Control.Visible = false;
        cell.Element.Control.RectTransform.Parent = null;
        cell.Scope.Dispose();
    }

    private void ReleaseAll()
    {
        foreach (Cell cell in _live.Values) { Keep(cell); }

        _live.Clear();
    }

    private GUIFrame Sheet()
    {
        if (_sheet is { } sheet) { return sheet; }

        sheet = new GUIFrame(
            new RectTransform(new Point(1, 1), _listBox.Content.RectTransform, Anchor.TopLeft, Pivot.TopLeft, ScaleBasis.Normal, isFixedSize: true),
            style: null)
        {
            CanBeFocused = false
        };

        sheet.RectTransform.SetPosition(Anchor.TopLeft, Pivot.TopLeft);

        return _sheet = sheet;
    }

    private sealed record Cell(object Item, ViewElement Element, ViewScope Scope, DataTemplate Template);
}
