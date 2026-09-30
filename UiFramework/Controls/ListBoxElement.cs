using System.Collections;
using System.Collections.Specialized;
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
        _listBox.Spacing = UiMetrics.DipInt(context.Metric("Spacing", UiMetrics.Gap));

        _listBox.Padding = new Vector4(UiMetrics.Dip(context.Metric("Padding", 0f)));

        _virtual = ViewMarkup.ToBool(context.Text("Virtual"), false);

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

        _rows.Clear();
        _spare.Clear();
        _spareOrder.Clear();
        _recycled.Clear();

        ReleaseSpacers();
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
        if (_virtual) { RebuildWindow(); }
        else if (Extends()) { Append(); }
        else { RebuildAll(); }

        if (_rows.Count == 0 && HasItems() && !_retriedEmpty)
        {
            _retriedEmpty = true;
            _view.Once(Rebuild);
        }
        else if (_rows.Count > 0)
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

    private BuiltRow? Build(object item, Templating.DataTemplate template)
    {
        OwnershipScope scope = _view.BeginScope();
        ViewElement row;

        try
        {
            row = template.Build(_view, item, _listBox.Content.RectTransform, this);
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
