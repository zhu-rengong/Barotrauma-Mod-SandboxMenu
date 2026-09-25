using Microsoft.Xna.Framework;

namespace SandboxMenu.UI.Framework;

internal interface IPopupWindow
{
    bool IsOpen { get; }

    Action? Closing { get; set; }

    Rectangle Rect { get; }

    void Open();

    void Register();

    void Update();

    void Dispose();

    T? Find<T>(string name) where T : GUIComponent;
}
