using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SandboxMenu.UI;

internal interface IDialogWindow
{
    bool IsOpen { get; }

    Action? Closing { get; set; }

    Rectangle Rect { get; }

    void Open();

    void Register();

    void Update();

    void Dispose();

    void DrawInto(SpriteBatch spriteBatch, Point shift);
}
