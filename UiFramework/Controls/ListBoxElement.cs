using System.Collections;
using System.Collections.Specialized;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UiFramework.Controls;

[Element("List")]
internal sealed class ListBoxElement : ViewElement, IDisposable
{
    private const float DragThreshold = 6f;

    // The entries of the filter that was just switched off, so it is a couple of turns of one filter's worth.
    private const int SpareLimit = 240;

    private readonly GUIListBox _listBox;
    private readonly ViewLoadContext _view;
    private readonly List<BuiltRow> _rows = [];

    // Rows that are no longer listed, held for an item the list may hold again: building a row's controls is the
    // whole cost of a list change, and a row kept here is that cost already paid.
    private readonly Dictionary<object, BuiltRow> _spare = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<object> _spareOrder = new();

    private IEnumerable? _items;
    private INotifyCollectionChanged? _observed;
    private string? _templateKey;
    private bool _rebuildQueued;
    private bool _retriedEmpty;

    // The scope is per row: that is what lets a dropped row give its own bindings back while the rows that stayed
    // keep theirs, a row that is still listed not being rebuilt at all.
    private sealed record BuiltRow(object Item, ViewElement Element, GUIButton? Button, OwnershipScope Scope);
    private IItemDropTarget? _dropTarget;
    private IListBackground? _background;

    public ListBoxElement(ElementContext context)
        : base(new GUIListBox(context.Rect(context.Parent, 1f, 1f), style: null!)
        {
            HoverCursor = CursorState.Default
        })
    {
        _listBox = (GUIListBox)Control;
        _view = context.View;
        _templateKey = context.Text("ItemTemplate");
        _listBox.Spacing = UiMetrics.DipInt(MarkupPlacement.Read(context.Node, "Spacing", UiMetrics.Gap));

        // The engine keeps a list's frame at its full size and insets the scrollable content by its padding,
        // which is how the rows get room inside the frame instead of the frame growing around them.
        _listBox.Padding = new Vector4(UiMetrics.Dip(MarkupPlacement.Read(context.Node, "Padding", 0f)));

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
            if (_observed is not null) { _observed.CollectionChanged -= OnCollectionChanged; }

            _items = value;
            _observed = value as INotifyCollectionChanged;
            _observed?.CollectionChanged += OnCollectionChanged;

            // The view model the rows are presented for is known by the time properties are applied.
            _dropTarget = DataContext as IItemDropTarget;
            _background = DataContext as IListBackground;

            Rebuild();
        }
    }

    [ElementProperty]
    public string? ItemTemplate { set => _templateKey = value; }

    [ElementProperty]
    public float Spacing { set => _listBox.Spacing = UiMetrics.DipInt(value); }

    internal override void AddContent(ViewElement child) => throw new NotSupportedException("List takes no content");

    public void Dispose()
    {
        if (_observed is not null) { _observed.CollectionChanged -= OnCollectionChanged; }

        _observed = null;

        for (int i = 0; i < _rows.Count; i++) { _rows[i].Scope.Dispose(); }

        foreach (BuiltRow row in _spare.Values) { row.Scope.Dispose(); }

        _rows.Clear();
        _spare.Clear();
        _spareOrder.Clear();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // A collection change is often the consequence of a binding writing back: rebuilding right here would tear
        // down the control that is mid-update, so it waits for the frame's deferred work.
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
        // A list that fills itself up is only ever handed more items than it had, and that change has to cost
        // nothing: assigning Parent recalculates every child the parent already has, so taking the rows out and
        // putting them back costs the length of the list on every frame it grows.
        if (Extends()) { Append(); }
        else { RebuildAll(); }

        // Rows produced for items that exist mean the templates were not reachable yet (a load-time ordering miss):
        // one deferred retry, after which a persistent miss keeps reporting through the diagnostics instead.
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
        // Every row is taken out of the list first: a row that is kept has to go back in the order the list now
        // asks for, and the engine appends a transform to whichever content it is handed.
        List<BuiltRow> previous = [.. _rows];

        for (int i = 0; i < previous.Count; i++) { previous[i].Element.Control.RectTransform.Parent = null; }

        _rows.Clear();
        _listBox.Content.ClearChildren();

        int cursor = 0;

        try
        {
            foreach (object item in _items ?? Array.Empty<object>())
            {
                // Narrowing a list hands the entries that stayed back in the order they were already in, so the row
                // at the cursor is the one to show; an entry the list already built a row for and stopped showing
                // is answered with that row, from the pool. Only a row the list never built costs anything here.
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

    private BuiltRow? Build(object item)
    {
        if (Templating.DataTemplate.Select(item, _templateKey, _view, this) is not { } template)
        {
            _view.Diagnostics.Report($"no template for an item of type '{item.GetType().Name}'", Node);
            return null;
        }

        // Closed even if the row could not be built, or the scope would stay open and take every element built
        // after it as well: the bands below the list in the window.
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

        // The list places a row by adding an offset to its anchor, so the anchor has to stay in the corner the
        // offsets are counted from: a row anchored elsewhere would sit off the slot it was given.
        row.Control.RectTransform.SetPosition(Anchor.TopLeft, Pivot.TopLeft);

        row.Parent = this;
        row.Control.UserData = item;

        return new BuiltRow(item, row, row.Control as GUIButton, scope);
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

        // Two rows for one item cannot both be held, or the list would lose track of the other one.
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

    private object? _candidate;
    private object? _dragged;
    private Point _grabOffset;
    private bool _dragging;
    private object? _highlight;
    private DropMode _highlightMode;
    private float _proximity;
    private Rectangle? _dropRect;

    // The colours a row is given for its own reasons (the template's, the dim of a reference row) read back whole
    // before the drag overrides them and put back afterwards: OverrideTextColor only covers three of the six text
    // colour states, and hover is not among the ones it derives from the others — a label dimmed for a drag kept
    // drawing dim on hover until the list happened to rebuild the row.
    private LabelSkin? _draggedSkin;
    private LabelSkin? _highlightSkin;

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
            // The dragged row follows the cursor, so it would always be in the way: the row the player is
            // pointing at is the one underneath it.
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
        if (ItemAt(mouse) is not null) { return; }

        _background?.ShowBackgroundMenu(mouse);
    }

    private void UpdateDrag()
    {
        // A popup sits on top of the list: dragging behind it would move invisible rows.
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

        // The list only repositions when asked to, so it is asked to put the row back where it belongs.
        _listBox.ChildrenNeedRecalculation = true;
    }

    // Outlined the way the game's own lists outline the slot a dragged row would land in.
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
}
