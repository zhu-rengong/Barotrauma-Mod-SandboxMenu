#if CLIENT
using Microsoft.Xna.Framework.Input;

namespace SandboxMenu.Settings;

internal readonly record struct KeyBind
{
    public KeyBind(Keys key)
    {
        Key = key;
        Mouse = MouseButton.None;
    }

    public KeyBind(MouseButton mouse)
    {
        Key = Keys.None;
        Mouse = mouse;
    }

    public Keys Key { get; init; }

    public MouseButton Mouse { get; init; }

    public static KeyBind FromName(string? name, KeyBind fallback)
    {
        if (string.IsNullOrEmpty(name)) { return fallback; }

        if (Enum.TryParse(name, out MouseButton mouse) && mouse != MouseButton.None)
        {
            return new KeyBind(mouse);
        }

        if (Enum.TryParse(name, out Keys key) && key != Keys.None)
        {
            return new KeyBind(key);
        }

        return fallback;
    }

    public bool IsHit() => Mouse switch
    {
        MouseButton.None => Key != Keys.None && PlayerInput.KeyHit(Key),
        MouseButton.PrimaryMouse => PlayerInput.PrimaryMouseButtonClicked(),
        MouseButton.SecondaryMouse => PlayerInput.SecondaryMouseButtonClicked(),
        MouseButton.MiddleMouse => PlayerInput.MidButtonClicked(),
        MouseButton.MouseButton4 => PlayerInput.Mouse4ButtonClicked(),
        MouseButton.MouseButton5 => PlayerInput.Mouse5ButtonClicked(),
        MouseButton.MouseWheelUp => PlayerInput.MouseWheelUpClicked(),
        MouseButton.MouseWheelDown => PlayerInput.MouseWheelDownClicked(),
        _ => false
    };

    public override string ToString() => Mouse != MouseButton.None ? Mouse.ToString() : Key.ToString();
}
#endif
