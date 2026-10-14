using Microsoft.Xna.Framework.Input;

namespace SandboxMenu;

internal static class KeyBinds
{
    private static readonly KeyBind _defaultToggle = new(Keys.F2);

    private static readonly KeyBind _defaultGive = new(Keys.F1);

    private static KeySetting? _toggle;

    private static KeySetting? _give;

    internal static KeyBind Toggle => _toggle?.Value ?? _defaultToggle;

    internal static KeyBind Give => _give?.Value ?? _defaultGive;

    internal static void Register()
    {
        _toggle = new KeySetting("togglekey".ToIdentifier(), _defaultToggle,
            label: TextManager.Get($"{ModInfo.Prefix}.togglekey.displayname"))
        {
            ShowInUI = true,
            ToolTip = TextManager.Get($"{ModInfo.Prefix}.togglekey.tooltip")
        };

        _give = new KeySetting("givekey".ToIdentifier(), _defaultGive,
            label: TextManager.Get($"{ModInfo.Prefix}.givekey.displayname"))
        {
            ShowInUI = true,
            ToolTip = TextManager.Get($"{ModInfo.Prefix}.givekey.tooltip")
        };

        Services.Settings.RegisterSetting(_toggle);
        Services.Settings.RegisterSetting(_give);
    }
}
