using UiFramework.Layout;

namespace UiFramework.Controls;

internal sealed class ListRowPool
{
    private const int Limit = 240;

    private readonly ViewContext _view;
    private readonly ListBoxElement _owner;
    private readonly GUIListBox _listBox;
    private readonly Dictionary<object, BuiltRow> _byItem = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, BuiltRow> _visible = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<object> _order = new();
    private readonly Dictionary<Type, Templating.DataTemplate> _rowTemplates = [];
    private readonly Dictionary<Type, Templating.DataTemplate> _tileTemplates = [];

    private readonly TemplatePool<BuiltRow> _recycled = new(Limit, static row =>
    {
        row.Element.Control.Visible = false;
        row.Element.Control.RectTransform.Parent = null;
        row.Scope.Dispose();
    });

    private string? _rowTemplateKey;
    private bool _layoutDirty;

    internal ListRowPool(ViewContext view, ListBoxElement owner, GUIListBox listBox, string? rowTemplateKey, string? tileTemplateKey)
    {
        _view = view;
        _owner = owner;
        _listBox = listBox;
        _rowTemplateKey = rowTemplateKey;
        TileTemplateKey = tileTemplateKey;
    }

    internal string? RowTemplateKey
    {
        get => _rowTemplateKey;
        set
        {
            _rowTemplateKey = value;
            _rowTemplates.Clear();
        }
    }

    private string? TileTemplateKey { get; }

    internal BuiltRow? Find(object? item)
        => item is not null && _visible.TryGetValue(item, out BuiltRow? row) ? row : null;

    internal BuiltRow? Build(object item)
        => RowTemplate(item) is { } template ? Build(item, template) : ReportMissingRowTemplate(item);

    internal BuiltRow? Build(object item, Templating.DataTemplate template, RectTransform? parent = null)
    {
        ViewScope scope = _view.BeginScope();
        ViewElement row;

        try
        {
            row = template.Build(_view, item, parent ?? _listBox.Content.RectTransform, _owner);
        }
        catch (Exception e)
        {
            scope.Dispose();
            _view.Diagnostics.Report($"row for '{item.GetType().Name}' could not be built ({e.Message})", _owner.Node);
            return null;
        }
        finally
        {
            _view.EndScope(scope);
        }

        row.Control.RectTransform.SetPosition(Anchor.TopLeft, Pivot.TopLeft);

        _layoutDirty = true;

        row.Parent = _owner;
        row.Control.UserData = item;

        BuiltRow built = new(item, row, row.Control as GUIButton, scope, template);

        _visible.TryAdd(item, built);

        return built;
    }

    internal void FlushLayout()
    {
        if (!_layoutDirty) { return; }

        _layoutDirty = false;
        LayoutFlush.Apply(_listBox.Content);
    }

    internal BuiltRow? Take(object item)
    {
        if (RowTemplate(item) is not { } template) { return ReportMissingRowTemplate(item); }

        BuiltRow? row = TakeRecycled(template, item) ?? Build(item, template);

        if (row is not null)
        {
            _visible.TryAdd(item, row);
        }

        return row;
    }

    internal BuiltRow? TakeTile(GUIFrame band, object item)
    {
        if (TileTemplate(item) is not { } template)
        {
            _view.Diagnostics.Report($"no tile template for an item of type '{item.GetType().Name}'", _owner.Node);
            return null;
        }

        BuiltRow? tile = TakeRecycled(template, item) ?? Build(item, template, band.RectTransform);

        if (tile is not null)
        {
            tile.Element.Control.RectTransform.Parent = band.RectTransform;
            _visible.TryAdd(item, tile);
        }

        return tile;
    }

    internal void EnsureTiles(List<BuiltRow> tiles, GUIFrame band, object seed, int capacity)
    {
        while (tiles.Count > capacity)
        {
            Drop(tiles[^1]);
            tiles.RemoveAt(tiles.Count - 1);
        }

        while (tiles.Count < capacity && TakeTile(band, seed) is { } tile) { tiles.Add(tile); }
    }

    internal Templating.DataTemplate? TileTemplate(object item)
    {
        Type type = item.GetType();

        if (_tileTemplates.TryGetValue(type, out Templating.DataTemplate? cached)) { return cached; }

        Templating.DataTemplate? template = Templating.DataTemplate.Select(item, TileTemplateKey, _view, _owner);

        if (template is not null) { _tileTemplates[type] = template; }

        return template;
    }

    internal BuiltRow Reuse(BuiltRow row, object item)
    {
        if (!ReferenceEquals(row.Item, item)) { row.Element.ResetAnimatedValues(); }

        row.Element.DataContext = item;
        row.Scope.Retarget(item);
        row.Element.Control.UserData = item;
        row.Element.Control.Visible = true;
        row.Scope.Resume();

        Forget(row);

        BuiltRow reused = row with { Item = item };

        _visible.TryAdd(item, reused);

        return reused;
    }

    internal bool TryTakeBack(object item, out BuiltRow? row)
    {
        if (!_byItem.Remove(item, out BuiltRow? kept))
        {
            row = null;
            return false;
        }

        kept.Element.Control.Visible = true;
        kept.Scope.Resume();

        _visible[item] = kept;

        row = kept;
        return true;
    }

    internal void Keep(BuiltRow row)
    {
        row.Element.Control.RectTransform.Parent = null;
        row.Element.Control.Visible = false;
        row.Scope.Pause();

        Forget(row);

        if (_owner.Windowed)
        {
            _recycled.Add(row.Template, row);

            return;
        }

        if (_byItem.ContainsKey(row.Item)) { Drop(row); return; }

        _byItem[row.Item] = row;
        _order.Enqueue(row.Item);

        while (_order.Count > Limit)
        {
            if (_byItem.Remove(_order.Dequeue(), out BuiltRow? oldest)) { Drop(oldest); }
        }
    }

    internal void Drop(BuiltRow row)
    {
        Forget(row);

        row.Element.Control.Visible = false;
        row.Element.Control.RectTransform.Parent = null;
        row.Scope.Dispose();
    }

    internal void Release(IEnumerable<BuiltRow?> rows)
    {
        foreach (BuiltRow? row in rows)
        {
            if (row is { } value) { Keep(value); }
        }
    }

    internal void DropRecycled() => _recycled.Clear();

    internal void Clear()
    {
        foreach (BuiltRow row in _byItem.Values) { Drop(row); }

        _byItem.Clear();
        _visible.Clear();
        _order.Clear();
        DropRecycled();
    }

    private void Forget(BuiltRow row)
    {
        if (_visible.TryGetValue(row.Item, out BuiltRow? mapped) && ReferenceEquals(mapped, row)) { _visible.Remove(row.Item); }
    }

    private BuiltRow? ReportMissingRowTemplate(object item)
    {
        _view.Diagnostics.Report($"no template for an item of type '{item.GetType().Name}'", _owner.Node);
        return null;
    }

    private BuiltRow? TakeRecycled(Templating.DataTemplate template, object item)
        => _recycled.Take(template) is { } row ? Reuse(row, item) : null;

    private Templating.DataTemplate? RowTemplate(object item)
    {
        Type type = item.GetType();

        if (_rowTemplates.TryGetValue(type, out Templating.DataTemplate? cached)) { return cached; }

        Templating.DataTemplate? template = Templating.DataTemplate.Select(item, _rowTemplateKey, _view, _owner);

        if (template is not null) { _rowTemplates[type] = template; }

        return template;
    }
}

internal sealed record BuiltRow(object Item, ViewElement Element, GUIButton? Button, ViewScope Scope, Templating.DataTemplate Template);
