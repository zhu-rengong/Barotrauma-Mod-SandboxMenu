using System.Windows.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Controls;

[Element("Tile")]
public sealed class TileElement : ViewElement, ITileCell
{
    private const float DefaultGrowExtent = 0.36f;

    private float _extent = 0.82f;
    private float _slotPadding = Theme.SlotPadding;
    private float _labelHeight = Theme.TileLabelHeight;
    private float _valueHeight = Theme.TileValueHeight;
    private float _labelInset = 4f;
    private float _grow;
    private float _growExtent = DefaultGrowExtent;
    private Color _glowFill = Color.Transparent;
    private Color? _labelColor;
    private Color? _valueColor;

    private DipValue _labelHeightDip;
    private DipValue _valueHeightDip;
    private DipValue _labelInsetDip;

    private readonly ViewContext _view;
    private readonly RowControl _tile;
    private readonly DeferredSprite _icon;
    private GUITextBlock? _label;
    private GUITextBlock? _value;
    private int _cellWidth;
    private int _cellHeight;

    public TileElement(ElementContext context)
        : base(new RowControl(context.Rect(context.Parent, 1f, 1f), string.Empty, Alignment.Center))
    {
        _view = context.View;
        _tile = (RowControl)Control;

        _tile.TextBlock.Visible = false;

        _icon = new DeferredSprite(
            new RectTransform(Vector2.One, Control.RectTransform, Anchor.Center),
            GUIImage.ScalingMode.ScaleToFitSmallestExtent);

        _tile.SlotIcon = _icon.Transform;
        _tile.SlotOnly = true;

        _ = new GUICustomComponent(new RectTransform(Vector2.One, Control.RectTransform, Anchor.Center), DrawGlow)
        {
            CanBeFocused = false
        };

        Control.RectTransform.SizeChanged += LayoutSlot;

        LayoutSlot();
    }

    void ITileCell.SetCellSize(int width, int height)
    {
        _cellWidth = width;
        _cellHeight = height;

        LayoutSlot();
    }

    [ElementProperty]
    public Sprite? Icon
    {
        set
        {
            _icon.Set(value);
            LayoutSlot();
        }
    }

    [ElementProperty]
    public float IconExtent
    {
        set
        {
            _extent = value;
            LayoutSlot();
        }
    }

    [ElementProperty]
    public RichString Text
    {
        set
        {
            if (_label is null)
            {
                _label = CreateLabel();

                LayoutSlot();
            }

            _label.Text = value;

            AlignBottom(CellWidth);
        }
    }

    [ElementProperty]
    public RichString Value
    {
        set
        {
            if (_value is null)
            {
                _value = CreateValue();

                LayoutSlot();
            }

            _value.Text = value;

            AlignBottom(CellWidth);
        }
    }

    [ElementProperty]
    public Color IconColor { set => _icon.Image.Color = value; }

    [ElementProperty]
    public bool IconShadow
    {
        get => _icon.Shadow;
        set => _icon.Shadow = value;
    }

    [ElementProperty]
    public Color IconShadowColor { set => _icon.ShadowColor = value; }

    [ElementProperty]
    public Color SlotFill
    {
        get => _tile.SlotFill;
        set => _tile.SlotFill = value;
    }

    [ElementProperty]
    public Color SlotLine
    {
        get => _tile.SlotLine;
        set => _tile.SlotLine = value;
    }

    [ElementProperty]
    public float SlotPadding
    {
        set
        {
            _slotPadding = value;
            _tile.SlotPadding = value;

            LayoutSlot();
        }
    }

    [ElementProperty]
    public float Grow
    {
        get => _grow;
        set
        {
            if (Math.Abs(_grow - value) < 0.0001f) { return; }

            _grow = value;

            LayoutSlot();
        }
    }

    [ElementProperty]
    public float GrowExtent
    {
        set
        {
            if (Math.Abs(_growExtent - value) < 0.0001f) { return; }

            _growExtent = value;

            LayoutSlot();
        }
    }

    [ElementProperty]
    public float LabelHeight
    {
        set
        {
            _labelHeight = value;

            LayoutSlot();
        }
    }

    [ElementProperty]
    public float ValueHeight
    {
        set
        {
            _valueHeight = value;

            LayoutSlot();
        }
    }

    [ElementProperty]
    public float LabelInset
    {
        set
        {
            _labelInset = value;

            AlignBottom(CellWidth);
        }
    }

    [ElementProperty]
    public RichString ToolTip { set => _tile.ToolTip = value; }

    [ElementProperty]
    public ICommand? Command
    {
        set => _tile.OnClicked = (_, _) =>
        {
            RaiseEvent("Click");
            _view.Dispatch(() => value?.Execute(null));
            return false;
        };
    }

    [ElementProperty]
    public ICommand? SecondaryCommand
    {
        set => _tile.OnSecondaryClicked = (_, _) =>
        {
            RaiseEvent("SecondaryPressed");
            _view.Dispatch(() => value?.Execute(null));

            return false;
        };
    }

    [ElementProperty]
    public Color Fill
    {
        get => _tile.Fill;
        set => _tile.Fill = value;
    }

    [ElementProperty]
    public Color SelectedBarFill
    {
        get => _tile.SelectedBarFill;
        set => _tile.SelectedBarFill = value;
    }

    [ElementProperty]
    public float SelectedBarWidth
    {
        get => _tile.SelectedBarWidth;
        set => _tile.SelectedBarWidth = value;
    }

    [ElementProperty]
    public bool Highlight
    {
        get => _tile.Highlight;
        set => _tile.Highlight = value;
    }

    [ElementProperty]
    public Color GlowFill
    {
        get => _glowFill;
        set => _glowFill = value;
    }

    [ElementProperty]
    public Color LabelColor { set => ApplyLabelColor(value); }

    [ElementProperty]
    public Color ValueColor { set => ApplyValueColor(value); }

    protected override void OnRelease()
    {
        Control.RectTransform.SizeChanged -= LayoutSlot;

        _icon.Dispose();
    }

    private GUITextBlock CreateLabel()
    {
        GUITextBlock label = new(
            new RectTransform(new Vector2(1f, Theme.TileTextRatio), Control.RectTransform, Anchor.BottomCenter),
            string.Empty,
            font: GUIStyle.SmallFont,
            textAlignment: Alignment.Center)
        {
            CanBeFocused = false,
            AutoScaleHorizontal = false,
            OverflowClip = true,
            TextScale = UiMetrics.TextScale,

            HoverColor = Color.Transparent,
            PressedColor = Color.Transparent,
            SelectedColor = Color.Transparent
        };

        Theme.ApplyLabel(label, _labelColor ?? Theme.Text);

        return label;
    }

    private GUITextBlock CreateValue()
    {
        GUITextBlock value = new(
            new RectTransform(new Vector2(1f, Theme.TileTextRatio), Control.RectTransform, Anchor.BottomCenter),
            string.Empty,
            font: GUIStyle.SmallFont,
            textAlignment: Alignment.Center)
        {
            CanBeFocused = false,
            AutoScaleHorizontal = false,
            OverflowClip = true,
            TextScale = UiMetrics.TextScale,

            HoverColor = Color.Transparent,
            PressedColor = Color.Transparent,
            SelectedColor = Color.Transparent
        };

        Theme.ApplyLabel(value, _valueColor ?? UiMetrics.TextDim);

        return value;
    }

    private void DrawGlow(SpriteBatch spriteBatch, GUICustomComponent component)
    {
        if (_glowFill.A == 0) { return; }

        Rectangle rect = _tile.SlotBox() ?? component.Rect;

        if (rect.Width <= 0 || rect.Height <= 0) { return; }

        try
        {
            GUIStyle.UIGlow.Draw(spriteBatch, rect, _glowFill, SpriteEffects.None);
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"Drawing a tile glow failed: {e}");
        }
    }

    private void ApplyLabelColor(Color color)
    {
        _labelColor = color;

        if (_label is not null) { Theme.ApplyLabel(_label, color); }
    }

    private void ApplyValueColor(Color color)
    {
        _valueColor = color;

        if (_value is not null) { Theme.ApplyLabel(_value, color); }
    }

    private int CellWidth => Math.Max(1, _cellWidth > 0 ? _cellWidth : Control.Rect.Width);

    private void LayoutSlot()
    {
        Rectangle rect = Control.Rect;
        int width = CellWidth;
        int height = Math.Max(1, _cellHeight > 0 ? _cellHeight : rect.Height);
        int labelHeight = _label is null ? 0 : Math.Min((int)(height * 0.4f), _labelHeightDip.Pixels(_labelHeight));
        int valueHeight = _value is null ? 0 : Math.Min(labelHeight, _valueHeightDip.Pixels(_valueHeight));
        int bottom = labelHeight + valueHeight;
        int area = Math.Max(1, height - bottom);
        float grown = 1f + _grow * _growExtent;
        int side = Math.Max(1, (int)MathF.Round(Math.Min(width, area) * _extent * grown));
        Point offset = new(0, -bottom / 2);

        Place(_icon.Transform, side, offset);

        if (_label is not null) { Place(_label.RectTransform, new Point(width, labelHeight), new Point(0, valueHeight), Anchor.BottomCenter); }
        if (_value is not null) { Place(_value.RectTransform, new Point(width, valueHeight), Point.Zero, Anchor.BottomCenter); }

        AlignBottom(width);

        _icon.Refit();
    }

    private void AlignBottom(int width)
    {
        int inset = _labelInsetDip.Pixels(_labelInset);
        bool clipped = _label is not null && _label.TextSize.X > Math.Max(1, width - 2 * inset);

        if (_label is not null)
        {
            _label.TextAlignment = clipped ? Alignment.CenterLeft : Alignment.Center;
            _label.Padding = new Vector4(clipped ? inset : 0f, 0f, 0f, 0f);
        }

        if (_value is not null) { _value.TextAlignment = Alignment.Center; }
    }

    private static void Place(RectTransform transform, int side, Point offset)
        => Place(transform, new Point(side, side), offset, Anchor.Center);

    private static void Place(RectTransform transform, Point size, Point offset, Anchor anchor)
    {
        transform.SetPosition(anchor);
        transform.IsFixedSize = true;
        transform.NonScaledSize = new Point(Math.Max(1, size.X), Math.Max(1, size.Y));
        transform.AbsoluteOffset = offset;
    }
}
