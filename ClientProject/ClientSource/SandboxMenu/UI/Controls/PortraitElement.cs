using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI.Controls;

[Element("Portrait")]
public sealed class PortraitElement : ViewElement, IDisposable
{
    private CharacterDisplay? _display;

    public PortraitElement(ElementContext context)
        : base(new GUICustomComponent(context.Rect(context.Parent, 1f, 1f), null, null)
        {
            CanBeFocused = false
        })
    {
        ((GUICustomComponent)Control).OnDraw = Draw;
    }

    [ElementProperty]
    public CharacterDisplay? Character { set => _display = value; }

    public void Dispose()
    {
        ((GUICustomComponent)Control).OnDraw = null;
        _display = null;
    }

    private void Draw(SpriteBatch spriteBatch, GUICustomComponent component)
        => PortraitDraw.Draw(spriteBatch, _display?.Info, component.Rect);
}

internal static class PortraitDraw
{
    internal static void Draw(SpriteBatch spriteBatch, CharacterInfo? info, Rectangle rect)
    {
        if (info is null || rect.Width <= 0 || rect.Height <= 0) { return; }

        info.DrawIcon(spriteBatch, rect.Center.ToVector2(), rect.Size.ToVector2());
    }
}
