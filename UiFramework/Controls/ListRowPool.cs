using UiFramework.Layout;

namespace UiFramework.Controls;

// The rows and tiles a list builds from its templates, and the two ways they come back: a windowed list recycles
// whatever template it needs, a plain list keeps a row per item. Either way the components stay and the bindings
// are only re-pointed, which is what keeps scrolling cheap.
internal sealed class ListRowPool
{
    private const int Limit = 240;

    private readonly ViewContext _view;
    private readonly ListBoxElement _owner;
    private readonly GUIListBox _listBox;
    private readonly Dictionary<object, BuiltRow> _byItem = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<object> _order = new();
    private readonly List<BuiltRow> _recycled = [];
    private readonly Dictionary<Type, Templating.DataTemplate> _rowTemplates = [];
    private readonly Dictionary<Type, Templating.DataTemplate> _tileTemplates = [];

    private string? _rowTemplateKey;

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

        // A fresh row is laid out on the spot: waiting for its first update would show it for a frame with its
        // labels and controls still sitting at their default places.
        LayoutFlush.Apply(row.Control);

        row.Parent = _owner;
        row.Control.UserData = item;

        return new BuiltRow(item, row, row.Control as GUIButton, scope, template);
    }

    internal BuiltRow? Take(object item)
        => RowTemplate(item) is { } template
            ? TakeRecycled(template, item) ?? Build(item, template)
            : ReportMissingRowTemplate(item);

    // A pooled tile comes back detached, so it has to be hung on its band again before it is placed: a detached
    // control would stay in the host's update list (it is visible) and keep drawing itself at its old place.
    internal BuiltRow? TakeTile(GUIFrame band, object item)
    {
        if (TileTemplate(item) is not { } template)
        {
            _view.Diagnostics.Report($"no tile template for an item of type '{item.GetType().Name}'", _owner.Node);
            return null;
        }

        BuiltRow? tile = TakeRecycled(template, item) ?? Build(item, template, band.RectTransform);

        if (tile is not null) { tile.Element.Control.RectTransform.Parent = band.RectTransform; }

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

    // A recycled row keeps its components and only re-points its bindings, which is what keeps scrolling cheap.
    internal static BuiltRow Reuse(BuiltRow row, object item)
    {
        row.Element.DataContext = item;
        row.Scope.Retarget(item);
        row.Element.Control.UserData = item;
        row.Element.Control.Visible = true;
        row.Scope.Resume();

        return row with { Item = item };
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
        row = kept;
        return true;
    }

    internal void Keep(BuiltRow row)
    {
        row.Element.Control.RectTransform.Parent = null;
        row.Element.Control.Visible = false;
        row.Scope.Pause();

        if (_owner.Windowed)
        {
            _recycled.Add(row);

            while (_recycled.Count > Limit)
            {
                Drop(_recycled[0]);
                _recycled.RemoveAt(0);
            }

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

    internal void DropRecycled()
    {
        for (int i = 0; i < _recycled.Count; i++) { Drop(_recycled[i]); }

        _recycled.Clear();
    }

    internal void Clear()
    {
        foreach (BuiltRow row in _byItem.Values) { Drop(row); }

        _byItem.Clear();
        _order.Clear();
        DropRecycled();
    }

    private BuiltRow? ReportMissingRowTemplate(object item)
    {
        _view.Diagnostics.Report($"no template for an item of type '{item.GetType().Name}'", _owner.Node);
        return null;
    }

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
