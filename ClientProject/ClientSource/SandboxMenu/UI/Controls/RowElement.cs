using System.Windows.Input;
using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Controls;

[Element("Row")]
public sealed class RowElement : ViewElement, IDisposable
{
    private readonly RowControl _row;
    private readonly ViewContext _view;
    private RichString _text = string.Empty;
    private RichString? _shortcut;
    private float _scale;

    // The box the icon is drawn in and whether it sits in a slot of its own: both are the view's call, so a list that
    // shows items can size and frame them the way an inventory does without touching the lists that do not.
    private float _iconBox = Theme.IconBox;
    private bool _iconSlot;

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

    private DeferredSprite? _icon;
    private GUITextBlock? _subText;
    private float _indent = Theme.Pad;
    private bool _hasCommand;
    private int _laidOutHeight = -1;

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
        // A row draws its own dark fill, so the hint keeps the light dim colour.
        _row.TextBlock.Text = ViewMarkup.WithShortcut(_text, _shortcut, ViewMarkup.HintKey);
        _row.TextBlock.TextScale = _scale;
    }

    [ElementProperty]
    public Sprite? Icon
    {
        set
        {
            // A row bound to an item that has no icon keeps no box either: a box would hold the label off the left edge
            // as if an icon were drawn in it.
            if (_icon is null && value is null) { return; }
            if (_icon is null) { BuildIcon(); }

            _icon!.Set(value);
        }
    }

    // How big the icon is drawn, in DIP, and whether it sits in a slot of its own. Both are the view's call, so a list
    // that shows items can size and frame them the way an inventory does without touching the lists that do not.
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
    public float Indent
    {
        set
        {
            _indent = value;
            ForceLayout();
        }
    }

    // The depth a tree row sits at, turned into the DIP it is drawn at: the view model holds the depth, the view
    // decides how deep that looks.
    [ElementProperty]
    public int IndentLevel
    {
        set => Indent = Theme.TreeIndentStart + value * Theme.TreeIndentStep;
    }

    [ElementProperty]
    public string TextColor { set => Labels.Apply(_row.TextBlock, ViewMarkup.ColorOf(value, UiMetrics.Text)); }

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
        float inset = _iconSlot ? Theme.SlotPadding : 0f;
        int left = UiMetrics.DipInt(_indent + (_icon is null ? 0f : _iconBox + 2f * inset + Theme.IconGap));
        int right = UiMetrics.DipInt(Theme.Pad);

        if (_icon is not null)
        {
            _icon.Transform.AbsoluteOffset = new Point(UiMetrics.DipInt(_indent + inset), 0);

            // The slot is the icon's own box, so the row only has to name the icon it frames.
            _row.SlotIcon = _iconSlot ? _icon.Transform : null;
        }
        else { _row.SlotIcon = null; }

        _row.TextBlock.Padding = new Vector4(left, 0f, right, _subText is null ? 0f : _row.Rect.Height * Theme.SubTextRatio);
        if (_subText is not null) { _subText.Padding = new Vector4(left, 0f, right, 0f); }
    }

    private void BuildIcon()
    {
        int box = UiMetrics.DipInt(_iconBox);

        _icon = new DeferredSprite(
            new RectTransform(new Point(box, box), Control.RectTransform, Anchor.CenterLeft, null, ScaleBasis.Normal, isFixedSize: true),
            GUIImage.ScalingMode.ScaleToFitSmallestExtent);

        ForceLayout();
    }

    private void ResizeIcon()
    {
        if (_icon is null) { return; }

        int box = UiMetrics.DipInt(_iconBox);
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

            // The host hands every child the row's state, and the default text-block style carries a hover bar of its
            // own (the one the mod lists wear): the row paints the highlight, so the subtext must not draw one over its
            // half of it.
            HoverColor = Color.Transparent,
            SelectedColor = Color.Transparent
        };

        _row.RectTransform.SizeChanged += ApplyLayout;
        ForceLayout();
    }

    public void Dispose()
    {
        _icon?.Dispose();
        if (_subText is not null) { _row.RectTransform.SizeChanged -= ApplyLayout; }
    }
}
