using System.Windows.Input;
using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Controls;

[Element("Tile")]
public sealed class TileElement : ViewElement, IDisposable
{
    private float _extent = 0.82f;

    private readonly ViewContext _view;
    private readonly RowControl _tile;
    private readonly DeferredSprite _icon;
    private readonly GUIFrame _glow;
    private float _growth;

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

        _glow = new GUIFrame(new RectTransform(Vector2.One, Control.RectTransform, Anchor.Center), null, null)
        {
            CanBeFocused = false,
            Color = Color.Transparent,
            HoverColor = Color.Transparent,
            PressedColor = Color.Transparent,
            SelectedColor = Color.Transparent,
            DisabledColor = Color.Transparent
        };

        Control.RectTransform.SizeChanged += LayoutSlot;

        _tile.Animate = Swell;

        LayoutSlot();
    }

    [ElementProperty]
    public Sprite? Icon
    {
        set => _icon.Set(value);
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
    public RichString ToolTip { set => _tile.ToolTip = value; }

    [ElementProperty]
    public ICommand? Command
    {
        set => _tile.OnClicked = (_, _) =>
        {
            _view.Dispatch(() => value?.Execute(null));
            return false;
        };
    }

    [ElementProperty]
    public ICommand? SecondaryCommand
    {
        set => _tile.OnSecondaryClicked = (_, _) =>
        {
            _view.Dispatch(() => value?.Execute(null));

            _glow.Flash(Theme.Accent, Theme.FlashSeconds);

            return false;
        };
    }

    public void Dispose()
    {
        Control.RectTransform.SizeChanged -= LayoutSlot;
        _tile.Animate = null;
        _icon.Dispose();
    }

    private void Swell(float delta)
    {
        float target = _tile.Hovered ? 1f : 0f;
        if (Math.Abs(_growth - target) < 0.001f) { return; }

        float step = Math.Max(0f, delta) / Theme.IconSwellSeconds;
        _growth = _growth < target ? Math.Min(target, _growth + step) : Math.Max(target, _growth - step);

        LayoutSlot();
    }

    private void LayoutSlot()
    {
        Rectangle cell = Control.Rect;
        float grown = 1f + _growth * Theme.IconSwell;
        int side = Math.Max(1, (int)MathF.Round(Math.Min(cell.Width, cell.Height) * _extent * grown));
        int width = Math.Max(1, cell.Width);
        int height = Math.Max(1, cell.Height);

        _icon.Transform.RelativeSize = new Vector2(side / (float)width, side / (float)height);

        int glow = side + 2 * UiMetrics.DipInt(Theme.SlotPadding);

        _glow.RectTransform.RelativeSize = new Vector2(glow / (float)width, glow / (float)height);
    }
}
