using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace UiFramework.Controls;

// Dragging a row out of the list and the drop indicator that shows where it would land, plus the menu a right
// click on the empty part of the list opens.
internal sealed class ListDrag(ListBoxElement list)
{
    private const float Threshold = 6f;

    private readonly GUIListBox _listBox = list.ListBox;
    private readonly ViewContext _view = list.View;

    private IItemDropTarget? _target;
    private IListBackground? _background;

    private object? _candidate;
    private object? _dragged;
    private Point _grabOffset;
    private object? _highlight;
    private DropMode _highlightMode;
    private float _proximity;
    private Rectangle? _dropRect;

    private LabelSkin? _draggedSkin;
    private LabelSkin? _highlightSkin;

    internal bool IsDragging { get; private set; }

    internal void Retarget(object? dataContext)
    {
        _target = dataContext as IItemDropTarget;
        _background = dataContext as IListBackground;
    }

    // A list inside a hidden function area keeps the rect it had and its drag code runs off the view's own frame
    // actions rather than the host's update list, so the element chain is what knows the area went away.
    private bool IsShown()
    {
        for (ViewElement? element = list; element is not null; element = element.Parent)
        {
            if (!element.Control.Visible) { return false; }
        }

        return true;
    }

    internal void Update()
    {
        if (_view.IsInputBlocked()) { return; }

        // An in-flight drag is let go even if its list was just switched away, so it always tidies up after itself.
        if (IsDragging) { ContinueDrag(PlayerInput.MousePosition); return; }

        if (!IsShown() || _target is null) { return; }

        Vector2 mouse = PlayerInput.MousePosition;

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

        if (_candidate is null || !_target.CanDrag(_candidate) || RowOf(_candidate) is not { } row) { return; }

        Point delta = mouse.ToPoint() - row.Rect.Location - _grabOffset;
        int threshold = UiMetrics.DipInt(Threshold);
        if (Math.Abs(delta.X) < threshold && Math.Abs(delta.Y) < threshold) { return; }

        IsDragging = true;
        _dragged = _candidate;
        _draggedSkin = LabelSkin.Of(row.TextBlock);
        row.TextBlock.OverrideTextColor(UiMetrics.Text * 0.45f);
        _listBox.DraggedElement = row;
    }

    internal void UpdateBackgroundClick()
    {
        if (!IsShown() || _view.IsInputBlocked() || !PlayerInput.SecondaryMouseButtonClicked()) { return; }

        Vector2 mouse = PlayerInput.MousePosition;

        // The menu belongs to the list: without this the click would be read anywhere on the screen, as "not on an
        // item" is also true for every point outside the list.
        if (!_listBox.Rect.Contains(mouse.ToPoint())) { return; }
        if (ItemAt(mouse) is not null) { return; }

        _background?.ShowBackgroundMenu(mouse);
    }

    internal void Reset()
    {
        RestoreHighlight();
        Restore(_draggedSkin, RowOf(_dragged));
        _draggedSkin = null;

        IsDragging = false;
        _dragged = null;
        _candidate = null;
        _listBox.DraggedElement = null;

        _listBox.ChildrenNeedRecalculation = true;
    }

    internal void Draw(SpriteBatch spriteBatch)
    {
        if (!IsDragging || _dropRect is not { Width: > 0 } rect) { return; }

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

    private void ContinueDrag(Vector2 mouse)
    {
        if (!PlayerInput.PrimaryMouseButtonHeld())
        {
            CompleteDrag(mouse);
            return;
        }

        if (_dragged is null || RowOf(_dragged) is not { } row)
        {
            Reset();
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

        Reset();

        if (source is null || target is null)
        {
            if (source is not null) { _target?.Drop(source, null, DropMode.After); }
            return;
        }

        if (ReferenceEquals(source, target)) { return; }
        _target?.Drop(source, target, mode);
    }

    private DropMode ResolveDropMode(object target, Vector2 mouse)
    {
        Rectangle rect = RowOf(target)?.Rect ?? Rectangle.Empty;
        float relativeY = rect.Height > 0 ? (mouse.Y - rect.Top) / rect.Height : 0.5f;

        if (_target?.CanNest(target) == true && relativeY is > 0.25f and < 0.75f) { return DropMode.Nest; }
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

    private GUIButton? RowOf(object? item)
    {
        for (int i = 0; i < list.Rows.Count; i++)
        {
            if (ReferenceEquals(list.Rows[i].Element.DataContext, item)) { return list.Rows[i].Button; }
        }

        return null;
    }

    private object? ItemAt(Vector2 mouse)
    {
        Point point = mouse.ToPoint();
        Rectangle viewport = _listBox.Rect;

        if (!viewport.Contains(point)) { return null; }

        for (int i = list.Rows.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(list.Rows[i].Item, _dragged)) { continue; }

            GUIComponent component = list.Rows[i].Element.Control;
            if (!component.Visible) { continue; }
            if (!viewport.Intersects(component.Rect)) { continue; }
            if (component.Rect.Contains(point)) { return list.Rows[i].Item; }
        }

        return null;
    }

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
