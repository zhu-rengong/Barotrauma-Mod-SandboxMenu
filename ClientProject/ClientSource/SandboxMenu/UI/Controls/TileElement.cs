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

        // The glow a slot wears when the game fills it is drawn from a control's own Draw, and that runs under the slot
        // the row paints; so the glow is given a box of its own, over the slot, with nothing else in it and no colour of
        // its own — a colour would be painted as a plain rectangle, and the flash is all this box is for.
        _glow = new GUIFrame(new RectTransform(Vector2.One, Control.RectTransform, Anchor.Center), null, null)
        {
            CanBeFocused = false,
            Color = Color.Transparent,
            HoverColor = Color.Transparent,
            PressedColor = Color.Transparent,
            SelectedColor = Color.Transparent,
            DisabledColor = Color.Transparent
        };

        // The slot the game keeps an icon in is a square, and a cell of the grid is rarely one: the icon box asks for a
        // different fraction of each side so that it comes out square, and the slot is drawn around the box.
        Control.RectTransform.SizeChanged += LayoutSlot;

        // The slot swells while the mouse is on the tile, the way a slot does in the inventory when an item leaves it or
        // arrives in it: the box grows over a moment and settles back the same way. The icon and the shadow sit inside
        // the box, so they come along, and the slot is taken from the box, so it grows with them.
        //
        // The animation rides on the row's own update: a row is updated every frame anyway, while only the rows the
        // list has built exist at all — a list of a thousand items still holds a screenful of rows.
        _tile.Animate = Swell;

        LayoutSlot();
    }

    [ElementProperty]
    public Sprite? Icon
    {
        set => _icon.Set(value);
    }

    // How much of its cell the icon takes. What is left over is the room between two icons, so a view sets this where
    // the grid wants more or less air.
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

            // What the right button does is done there and then, so the slot says so the way the game's own slots do when
            // an item leaves or arrives: a glow at the frame that comes and goes. It is not the swell — the mouse is on
            // the tile anyway, and the swell answers the mouse, not the click.
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

    // Asked for every frame the row is updated, and it leaves as soon as the growth has settled where it belongs.
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

        // The glow covers the slot the row draws around the icon — the same box the row takes for it — so the flash comes
        // and goes at the frame the slot is, however the slot is sized.
        int glow = side + 2 * UiMetrics.DipInt(Theme.SlotPadding);

        _glow.RectTransform.RelativeSize = new Vector2(glow / (float)width, glow / (float)height);
    }
}
