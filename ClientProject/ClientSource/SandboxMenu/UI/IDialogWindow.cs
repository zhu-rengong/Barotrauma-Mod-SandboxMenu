using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI;

// What the shell keeps of a dialog it is showing: markup builds the window itself, so nothing is reached into by
// name any more — the shell only drives its lifecycle and puts it in a screenshot.
internal interface IDialogWindow
{
    bool IsOpen { get; }

    Action? Closing { get; set; }

    Rectangle Rect { get; }

    void Open();

    void Register();

    void Update();

    void Dispose();

    // Draws the window into whatever the batch is drawing to, moved by the given offset: the screenshot cannot wait
    // for the game's own draw pass, and the windows move because parts of the host's GUI restart the batch.
    void DrawInto(SpriteBatch spriteBatch, Point shift);
}
