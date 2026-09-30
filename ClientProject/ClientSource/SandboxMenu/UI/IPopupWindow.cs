using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI;

internal interface IPopupWindow
{
    bool IsOpen { get; }

    Action? Closing { get; set; }

    Rectangle Rect { get; }

    void Open();

    void Register();

    void Update();

    void Dispose();

    // Draws the window into whatever the batch is drawing to, moved by the given offset: the screenshot puts the menu
    // on an offscreen target, so it cannot wait for the game's own draw pass — and it moves the windows instead of
    // translating the batch, because parts of the host's GUI restart the batch and would ignore a transform.
    void DrawInto(SpriteBatch spriteBatch, Point shift);

    T? Find<T>(string name) where T : GUIComponent;
}
