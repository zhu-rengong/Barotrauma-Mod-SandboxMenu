using System.Windows.Input;
using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Controls;

[Element("Tile")]
public sealed class TileElement : ViewElement, IDisposable
{
    private const float IconExtent = 0.86f;

    private readonly ViewContext _view;
    private readonly RowControl _tile;
    private readonly DeferredSprite _icon;

    public TileElement(ElementContext context)
        : base(new RowControl(context.Rect(context.Parent, 1f, 1f), string.Empty, Alignment.Center))
    {
        _view = context.View;
        _tile = (RowControl)Control;

        _tile.TextBlock.Visible = false;

        _icon = new DeferredSprite(
            new RectTransform(new Vector2(IconExtent, IconExtent), Control.RectTransform, Anchor.Center),
            GUIImage.ScalingMode.ScaleToFitSmallestExtent);
    }

    [ElementProperty]
    public Sprite? Icon
    {
        set => _icon.Set(value);
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

    public void Dispose() => _icon.Dispose();
}
