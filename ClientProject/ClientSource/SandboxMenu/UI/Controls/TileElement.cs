using System.Windows.Input;
using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Controls;

[Element("Tile")]
internal sealed class TileElement : ViewElement
{
    private const float IconExtent = 0.86f;

    private readonly ViewLoadContext _view;
    private readonly MenuRow _tile;
    private readonly GUIImage _icon;

    public TileElement(ElementContext context)
        : base(new MenuRow(context.Rect(context.Parent, 1f, 1f), string.Empty, Alignment.Center))
    {
        _view = context.View;
        _tile = (MenuRow)Control;

        MenuText.MakeRow(_tile);
        _tile.TextBlock.Visible = false;

        _icon = new GUIImage(
            new RectTransform(new Vector2(IconExtent, IconExtent), Control.RectTransform, Anchor.Center),
            style: null,
            scaleToFit: GUIImage.ScalingMode.ScaleToFitSmallestExtent)
        {
            CanBeFocused = false
        };
    }

    [ElementProperty]
    public Sprite? Icon
    {
        set
        {
            _icon.Sprite = value;
            _icon.Visible = value is not null;
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
}
