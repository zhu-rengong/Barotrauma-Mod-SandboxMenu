using System.Collections;
using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Controls;

[Element("Dropdown")]
public sealed class DropdownElement : ViewElement, IPropertyObserver
{
    private const string ListStyleName = "GUIDropDown.GUIListBox";

    private const string ItemStyleName = "ListBoxElement";

    private const float IconSize = 0.6f;

    private const float IconOffset = 5f;

    private readonly GUIButton _button;
    private readonly ViewContext _view;
    private readonly List<RichString> _labels = [];
    private readonly GUIComponentStyle? _listStyle;
    private readonly float _padLeft;
    private readonly float _padRight;

    private GUIListBox? _list;
    private GUIImage? _icon;
    private Action<object?>? _changed;
    private bool _open;
    private bool _syncing;
    private int _selected;
    private int _maxRows = 10;

    public DropdownElement(ElementContext context)
        : base(new GUIButton(
            context.Rect(context.Parent, 1f, Theme.ControlHeight),
            string.Empty,
            ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.CenterLeft),
            context.Skin))
    {
        _button = (GUIButton)Control;
        _view = context.View;
        _listStyle = context.Style(ListStyleName);

        _button.TextBlock.OverflowClip = true;
        _button.TextBlock.TextScale = UiMetrics.TextScale;
        _button.OnClicked = (_, _) =>
        {
            Toggle();
            return false;
        };

        AttachSkinIcon();

        _padLeft = _button.TextBlock.Padding.X;
        _padRight = _button.TextBlock.Padding.Z;

        ApplyAlignment();

        _button.RectTransform.SizeChanged += ApplyAlignment;
        _view.EveryFrame(UpdateDrop);
    }

    [ElementProperty]
    public IEnumerable? Items
    {
        set
        {
            _labels.Clear();

            foreach (object item in value ?? Array.Empty<object>()) { _labels.Add(ViewMarkup.ToRichText(item)); }

            _selected = _labels.Count == 0 ? 0 : Math.Clamp(_selected, 0, _labels.Count - 1);

            ApplyLabel();

            if (_open) { FillList(); }
        }
    }

    [ElementProperty]
    public int MaxRows
    {
        set
        {
            _maxRows = Math.Max(2, value);

            if (_open) { FillList(); }
        }
    }

    [ElementProperty(Mode = BindingMode.TwoWay)]
    public int SelectedIndex
    {
        set
        {
            if (value < 0 || value >= _labels.Count || value == _selected) { return; }

            _selected = value;

            ApplyLabel();
            SyncSelection();
        }
    }

    void IPropertyObserver.Observe(string property, Action<object?> changed)
    {
        if (string.Equals(property, nameof(SelectedIndex), StringComparison.OrdinalIgnoreCase)) { _changed = changed; }
    }

    protected override void OnRelease()
    {
        StopEveryFrame(UpdateDrop);

        _button.RectTransform.SizeChanged -= ApplyAlignment;
        _button.OnClicked = null;

        if (_list is { } list)
        {
            list.AfterSelected = null;
            list.RemoveFromGUIUpdateList();
            list.RectTransform.Parent = null;
        }

        _open = false;
        _list = null;
        _icon = null;
        _changed = null;
        _view.CloseTransient = null;
    }

    private int VisibleRows => Math.Clamp(_labels.Count, 2, Math.Max(2, _maxRows));

    private int IconInset
        => _icon is null ? 0 : (int)MathF.Round(_button.Rect.Height * IconSize) + UiMetrics.DipInt(IconOffset);

    private void Toggle()
    {
        if (_open) { Close(); }
        else { Open(); }
    }

    private void Open()
    {
        if (_labels.Count == 0 || !EnsureList()) { return; }

        _open = true;

        ApplyAlignment();
        FillList();
        Place(_list!);

        _list!.Visible = true;
        _view.CloseTransient = Close;
    }

    private bool Close()
    {
        if (!_open) { return false; }

        _open = false;
        _view.CloseTransient = null;

        if (_list is not null) { _list.Visible = false; }

        return true;
    }

    private bool EnsureList()
    {
        if (_list is not null) { return true; }
        if (_view.RootElement is not IViewWindow chrome) { return false; }

        GUIListBox list = new(
            new RectTransform(Vector2.Zero, chrome.Frame.RectTransform, Anchor.TopLeft, null, null, null, ScaleBasis.Normal),
            style: null)
        {
            PlaySoundOnSelect = true,
            Spacing = 0,
            Visible = false
        };

        if (_listStyle is not null)
        {
            list.ApplyStyle(_listStyle);
            list.ContentBackground.ApplyStyle(_listStyle);
        }

        list.AfterSelected = OnItemSelected;

        _list = list;

        return true;
    }

    private void FillList()
    {
        if (_list is not { } list) { return; }

        int rows = VisibleRows;
        Rectangle button = _button.Rect;

        Point size = new(Math.Max(1, button.Width), Math.Max(1, button.Height * rows));

        list.RectTransform.MinSize = size;
        list.RectTransform.MaxSize = size;
        list.RectTransform.NonScaledSize = size;
        list.UpdateDimensions();
        list.ScrollBarEnabled = _labels.Count > rows;

        list.ClearChildren();

        Rectangle content = list.Content.Rect;
        Point itemSize = new(Math.Max(1, content.Width), Math.Max(1, content.Height / rows));

        for (int i = 0; i < _labels.Count; i++)
        {
            _ = new GUITextBlock(
                new RectTransform(itemSize, list.Content.RectTransform, Anchor.TopLeft, null, ScaleBasis.Normal, isFixedSize: false),
                _labels[i],
                textColor: null,
                font: GUIStyle.Font,
                textAlignment: Alignment.CenterLeft,
                wrap: false,
                style: ItemStyleName,
                color: null)
            {
                UserData = i
            };
        }

        SyncSelection();
    }

    private void SyncSelection()
    {
        if (_list is not { } list || _labels.Count == 0) { return; }

        _syncing = true;

        try
        {
            list.Select(
                Math.Clamp(_selected, 0, _labels.Count - 1),
                GUIListBox.Force.No,
                GUIListBox.AutoScroll.Disabled,
                GUIListBox.TakeKeyBoardFocus.No,
                GUIListBox.PlaySelectSound.No,
                false);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Place(GUIListBox list)
    {
        if (_view.RootElement is not IViewWindow chrome) { return; }

        Rectangle frame = chrome.Frame.Rect;
        Rectangle button = _button.Rect;

        Point offset = new(button.Left - frame.Left, button.Bottom - frame.Top);

        if (list.RectTransform.AbsoluteOffset != offset) { list.RectTransform.AbsoluteOffset = offset; }
    }

    private void UpdateDrop()
    {
        if (!_open || _list is not { } list) { return; }

        Place(list);

        if (!PlayerInput.PrimaryMouseButtonClicked()) { return; }

        Point mouse = PlayerInput.MousePosition.ToPoint();

        if (_button.Rect.Contains(mouse) || list.Rect.Contains(mouse)) { return; }

        Close();
    }

    private bool OnItemSelected(GUIComponent component, object? data)
    {
        if (_syncing || data is not int index || index < 0 || index >= _labels.Count) { return true; }

        _selected = index;

        ApplyLabel();
        Close();

        _changed?.Invoke(index);

        return true;
    }

    private void ApplyLabel()
    {
        if (_labels.Count == 0) { _button.Text = LocalizedString.EmptyString; }
        else { _button.Text = _labels[Math.Clamp(_selected, 0, _labels.Count - 1)].NestedStr; }

        ApplyAlignment();
    }

    private void ApplyAlignment()
        => _button.TextBlock.Padding = new Vector4(_padLeft, 0f, _padRight + IconInset, 0f);

    private void AttachSkinIcon()
    {
        if (_button.Style is not { } style) { return; }
        if (!style.ChildStyles.TryGetValue("dropdownicon".ToIdentifier(), out GUIComponentStyle? iconStyle)) { return; }

        _icon = new GUIImage(
            new RectTransform(new Vector2(IconSize, IconSize), _button.RectTransform, Anchor.CenterRight, null, null, null, ScaleBasis.BothHeight)
            {
                AbsoluteOffset = new Point(UiMetrics.DipInt(IconOffset), 0)
            },
            style: null,
            scaleToFit: true)
        {
            CanBeFocused = false
        };

        _icon.ApplyStyle(iconStyle);
    }
}
