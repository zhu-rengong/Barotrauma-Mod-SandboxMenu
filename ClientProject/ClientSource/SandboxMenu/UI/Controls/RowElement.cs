using System.Windows.Input;
using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Controls;

[Element("Row")]
public sealed class RowElement : ViewElement
{
    private readonly RowControl _row;
    private readonly ViewContext _view;
    private RichString _text = string.Empty;
    private RichString? _shortcut;
    private float _scale;

    private float _iconBox = Theme.IconBox;
    private bool _iconSlot;
    private float _slotPadding = Theme.SlotPadding;

    [ElementProperty]
    public float FontSize
    {
        set
        {
            _scale = UiMetrics.FontScale(value, _row.TextBlock.Font);
            _row.TextBlock.TextScale = _scale;
        }
    }

    [ElementProperty]
    public string TextAlign { set => _row.TextBlock.TextAlignment = ViewMarkup.AlignmentOf(value, Alignment.CenterLeft); }

    [ElementProperty]
    public bool Scale
    {
        set
        {
            _row.TextBlock.AutoScaleHorizontal = value && !_itemRow;
            ForceLayout();
        }
    }

    private DeferredSprite? _icon;
    private Color _iconColor = Color.White;
    private bool? _iconShadow;
    private Color? _iconShadowColor;
    private GUITextBlock? _subText;
    private float _indent = Theme.Pad;
    private bool _hasCommand;
    private bool _itemRow;
    private int _laidOutHeight = -1;

    private DipValue _labelLeftDip;
    private DipValue _padDip;
    private DipValue _iconOffsetDip;
    private DipValue _iconBoxDip;
    private Color? _subTextColor;

    public RowElement(ElementContext context)
        : base(new RowControl(
            context.Rect(context.Parent, 1f, Theme.RowHeight),
            string.Empty,
            ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.CenterLeft)))
    {
        _row = (RowControl)Control;
        _view = context.View;
        _scale = ViewMarkup.TextScaleOf(context.Text("FontSize"), _row.TextBlock.Font);

        _row.TextBlock.TextScale = _scale;

        _row.TextBlock.ForceUpperCase = ForceUpperCase.No;

        bool itemRow = context.Text("Icon") is not null || context.Text("SubText") is not null;
        _itemRow = itemRow;
        _row.TextBlock.AutoScaleHorizontal = !itemRow && ViewMarkup.ToBool(context.Text("Scale"), true);
        _row.TextBlock.OverflowClip = itemRow;

        if (context.Text("Icon") is not null) { BuildIcon(); }
        if (context.Text("SubText") is not null) { BuildSubText(); }

        ForceLayout();

        bool styled = false;
        context.View.Once(() =>
        {
            if (styled) { return; }

            styled = true;
            if (!_hasCommand)
            {
                _row.CanBeSelected = false;
                _row.Highlight = false;
                _row.HoverCursor = CursorState.Default;
            }
        });
    }

    [ElementProperty(KeyText = true)]
    public RichString Text
    {
        set
        {
            _text = value;
            ApplyText();
        }
    }

    [ElementProperty]
    public RichString Literal
    {
        set
        {
            _text = value;
            ApplyText();
        }
    }

    [ElementProperty(KeyText = true)]
    public RichString? Shortcut
    {
        set
        {
            _shortcut = value;
            ApplyText();
        }
    }

    private void ApplyText()
    {
        _row.TextBlock.Text = ViewMarkup.WithShortcut(_text, _shortcut, ViewMarkup.HintKey);
        _row.TextBlock.TextScale = _scale;
    }

    [ElementProperty]
    public Sprite? Icon
    {
        set
        {
            if (_icon is null && value is null) { return; }
            if (_icon is null) { BuildIcon(); }

            _icon!.Set(value);
        }
    }

    [ElementProperty]
    public Color IconColor
    {
        set
        {
            _iconColor = value;

            if (_icon is not null) { _icon.Image.Color = value; }
        }
    }

    [ElementProperty]
    public bool IconShadow
    {
        get => _iconShadow ?? true;
        set
        {
            _iconShadow = value;

            if (_icon is not null) { _icon.Shadow = value; }
        }
    }

    [ElementProperty]
    public Color IconShadowColor
    {
        set
        {
            _iconShadowColor = value;

            if (_icon is not null) { _icon.ShadowColor = value; }
        }
    }

    [ElementProperty]
    public float IconSize
    {
        set
        {
            _iconBox = value;
            ResizeIcon();
            ForceLayout();
        }
    }

    [ElementProperty]
    public bool IconSlot
    {
        set
        {
            _iconSlot = value;
            ForceLayout();
        }
    }

    [ElementProperty]
    public float SlotPadding
    {
        set
        {
            _slotPadding = value;
            _row.SlotPadding = value;
            ForceLayout();
        }
    }

    [ElementProperty]
    public RichString SubText
    {
        set
        {
            if (_subText is null) { BuildSubText(); }

            _subText!.Text = value;
        }
    }

    [ElementProperty]
    public RichString ToolTip { set => _row.ToolTip = value; }

    [ElementProperty]
    public Color Fill
    {
        get => _row.Fill;
        set => _row.Fill = value;
    }

    [ElementProperty]
    public Color SelectedBarFill
    {
        get => _row.SelectedBarFill;
        set => _row.SelectedBarFill = value;
    }

    [ElementProperty]
    public float SelectedBarWidth
    {
        get => _row.SelectedBarWidth;
        set => _row.SelectedBarWidth = value;
    }

    [ElementProperty]
    public bool Highlight
    {
        get => _row.Highlight;
        set => _row.Highlight = value;
    }

    [ElementProperty]
    public Color SlotFill
    {
        get => _row.SlotFill;
        set => _row.SlotFill = value;
    }

    [ElementProperty]
    public Color SlotLine
    {
        get => _row.SlotLine;
        set => _row.SlotLine = value;
    }

    [ElementProperty]
    public Color SubTextColor
    {
        set
        {
            _subTextColor = value;

            if (_subText is not null) { Theme.ApplyLabel(_subText, value); }
        }
    }

    [ElementProperty]
    public float Indent
    {
        set
        {
            _indent = value;
            ForceLayout();
        }
    }

    [ElementProperty]
    public int IndentLevel
    {
        set => Indent = Theme.TreeIndentStart + value * Theme.TreeIndentStep;
    }

    [ElementProperty]
    public Color TextColor
    {
        get => _row.TextBlock.TextColor;
        set => Theme.ApplyLabel(_row.TextBlock, value);
    }

    [ElementProperty]
    public bool Selected { set => _row.Selected = value; }

    [ElementProperty]
    public string SelectedBar { set => _row.SelectedBar = ViewMarkup.AnchorOf(value, Anchor.CenterLeft); }

    [ElementProperty]
    public ICommand? Command
    {
        set
        {
            _hasCommand |= value is not null;
            _row.OnClicked = (_, _) =>
            {
                RaiseEvent("Click");
                _view.Dispatch(() => value?.Execute(null));
                return false;
            };
        }
    }

    [ElementProperty]
    public ICommand? SecondaryCommand
    {
        set
        {
            _hasCommand |= value is not null;
            _row.OnSecondaryClicked = (_, _) =>
            {
                _view.Dispatch(() => value?.Execute(null));
                return false;
            };
        }
    }

    private void ApplyLayout()
    {
        int height = _row.Rect.Height;
        if (height == _laidOutHeight) { return; }

        _laidOutHeight = height;
        ApplyAll();
    }

    private void ForceLayout()
    {
        _laidOutHeight = -1;
        ApplyLayout();
    }

    private void ApplyAll()
    {
        float inset = _iconSlot ? _slotPadding : 0f;
        int left = _labelLeftDip.Pixels(_indent + (_icon is null ? 0f : _iconBox + 2f * inset + Theme.IconGap));
        int right = _padDip.Pixels(Theme.Pad);

        if (_icon is not null)
        {
            _icon.Transform.AbsoluteOffset = new Point(_iconOffsetDip.Pixels(_indent + inset), 0);

            _row.SlotIcon = _iconSlot ? _icon.Transform : null;
        }
        else { _row.SlotIcon = null; }

        _row.TextBlock.Padding = new Vector4(left, 0f, right, _subText is null ? 0f : _row.Rect.Height * Theme.SubTextRatio);
        if (_subText is not null) { _subText.Padding = new Vector4(left, 0f, right, 0f); }
    }

    private void BuildIcon()
    {
        int box = _iconBoxDip.Pixels(_iconBox);

        _icon = new DeferredSprite(
            new RectTransform(new Point(box, box), Control.RectTransform, Anchor.CenterLeft, null, ScaleBasis.Normal, isFixedSize: true),
            GUIImage.ScalingMode.ScaleToFitSmallestExtent);

        _icon.Image.Color = _iconColor;

        if (_iconShadow is { } shadow) { _icon.Shadow = shadow; }
        if (_iconShadowColor is { } shadowColor) { _icon.ShadowColor = shadowColor; }

        ForceLayout();
    }

    private void ResizeIcon()
    {
        if (_icon is null) { return; }

        int box = _iconBoxDip.Pixels(_iconBox);
        _icon.Transform.NonScaledSize = new Point(box, box);
    }

    private void BuildSubText()
    {
        _subText = new GUITextBlock(
            new RectTransform(new Vector2(1f, Theme.SubTextRatio), Control.RectTransform, Anchor.BottomLeft),
            string.Empty,
            font: GUIStyle.Font,
            textAlignment: Alignment.CenterLeft)
        {
            AutoScaleHorizontal = false,
            OverflowClip = true,
            CanBeFocused = false,
            TextScale = UiMetrics.TextScale,

            HoverColor = Color.Transparent,
            SelectedColor = Color.Transparent
        };

        if (_subTextColor is { } color) { Theme.ApplyLabel(_subText, color); }

        _row.RectTransform.SizeChanged += ApplyLayout;
        ForceLayout();
    }

    protected override void OnRelease()
    {
        _icon?.Dispose();
        if (_subText is not null) { _row.RectTransform.SizeChanged -= ApplyLayout; }
    }
}
