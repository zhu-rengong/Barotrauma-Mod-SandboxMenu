using System.Windows.Input;
using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Controls;

[Element("Row")]
internal sealed class RowElement : ViewElement, IDisposable
{
    private readonly MenuRow _row;
    private readonly ViewLoadContext _view;
    private float _scale;

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

    private GUIImage? _icon;
    private GUITextBlock? _subText;
    private float _indent = UiMetrics.Pad;
    private bool _hasCommand;
    private int _laidOutHeight = -1;

    public RowElement(ElementContext context)
        : base(new MenuRow(
            context.Rect(context.Parent, 1f, UiMetrics.RowHeight),
            string.Empty,
            ViewMarkup.AlignmentOf(context.Text("TextAlign"), Alignment.CenterLeft)))
    {
        _row = (MenuRow)Control;
        _view = context.View;
        _scale = ViewMarkup.TextScaleOf(context.Text("FontSize"), _row.TextBlock.Font);

        MenuText.MakeRow(_row);
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
                _row.HoverColor = Color.Transparent;
                _row.PressedColor = Color.Transparent;
                _row.SelectedColor = Color.Transparent;
                _row.HoverCursor = CursorState.Default;
            }
        });
    }

    [ElementProperty(KeyText = true)]
    public RichString Text
    {
        set
        {
            _row.TextBlock.Text = value;
            _row.TextBlock.TextScale = _scale;
        }
    }

    [ElementProperty]
    public RichString Literal
    {
        set
        {
            _row.TextBlock.Text = value;
            _row.TextBlock.TextScale = _scale;
        }
    }

    [ElementProperty]
    public Sprite? Icon
    {
        set
        {
            if (_icon is null) { BuildIcon(); }

            _icon!.Sprite = value;
            _icon.Scale = FitScale(_icon);
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

    [ElementProperty]
    public string TextColor { set => MenuText.Apply(_row.TextBlock, ViewMarkup.ColorOf(value, UiMetrics.Text)); }

    [ElementProperty]
    public bool Selected { set => _row.Selected = value; }

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
        int left = UiMetrics.DipInt(_indent + (_icon is null ? 0f : UiMetrics.IconBox + UiMetrics.IconGap));
        int right = UiMetrics.DipInt(UiMetrics.Pad);

        _row.TextBlock.Padding = new Vector4(left, 0f, right, _subText is null ? 0f : _row.Rect.Height * UiMetrics.SubTextRatio);
        if (_subText is not null) { _subText.Padding = new Vector4(left, 0f, right, 0f); }
        if (_icon is not null) { _icon.RectTransform.AbsoluteOffset = new Point(UiMetrics.DipInt(_indent), 0); }
    }

    private void BuildIcon()
    {
        int box = UiMetrics.DipInt(UiMetrics.IconBox);

        _icon = new GUIImage(
            new RectTransform(new Point(box, box), Control.RectTransform, Anchor.CenterLeft, null, ScaleBasis.Normal, isFixedSize: true),
            style: null,
            scaleToFit: GUIImage.ScalingMode.ScaleToFitSmallestExtent)
        {
            CanBeFocused = false
        };

        _icon.RectTransform.SizeChanged += RescaleIcon;
        ForceLayout();
    }

    private void RescaleIcon() => _icon!.Scale = FitScale(_icon);

    private void BuildSubText()
    {
        _subText = new GUITextBlock(
            new RectTransform(new Vector2(1f, UiMetrics.SubTextRatio), Control.RectTransform, Anchor.BottomLeft),
            string.Empty,
            font: GUIStyle.Font,
            textAlignment: Alignment.CenterLeft)
        {
            AutoScaleHorizontal = false,
            OverflowClip = true,
            CanBeFocused = false,
            TextScale = UiMetrics.TextScale
        };

        _row.RectTransform.SizeChanged += ApplyLayout;
        ForceLayout();
    }

    private static float FitScale(GUIImage image)
    {
        if (image.Sprite is not { } sprite) { return 1f; }

        Rectangle source = sprite.SourceRect;
        Rectangle rect = image.RectTransform.Rect;
        if (source.Width <= 0 || source.Height <= 0 || rect.Width <= 0 || rect.Height <= 0) { return 1f; }

        return Math.Min((float)rect.Width / source.Width, (float)rect.Height / source.Height);
    }

    public void Dispose()
    {
        if (_icon is { } icon) { icon.RectTransform.SizeChanged -= RescaleIcon; }
        if (_subText is not null) { _row.RectTransform.SizeChanged -= ApplyLayout; }
    }
}
