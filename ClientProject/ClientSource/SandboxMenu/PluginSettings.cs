using Microsoft.Xna.Framework.Input;

namespace SandboxMenu;

public sealed partial class Plugin
{
    private static readonly KeyBind _defaultToggleKey = new(Keys.F2);
    private static readonly KeyBind _defaultGiveKey = new(Keys.F1);

    private static KeySetting? _toggleKeySetting;
    private static KeySetting? _giveKeySetting;

    internal static KeyBind ToggleKey => _toggleKeySetting?.Value ?? _defaultToggleKey;

    internal static KeyBind GiveKey => _giveKeySetting?.Value ?? _defaultGiveKey;

    private void CreateSettings()
    {
        _toggleKeySetting = new KeySetting(
            "togglekey".ToIdentifier(),
            _defaultToggleKey,
            label: TextManager.Get($"{ModPrefix}.togglekey.displayname"))
        {
            ShowInUI = true,
            ToolTip = TextManager.Get($"{ModPrefix}.togglekey.tooltip")
        };

        _giveKeySetting = new KeySetting(
            "givekey".ToIdentifier(),
            _defaultGiveKey,
            label: TextManager.Get($"{ModPrefix}.givekey.displayname"))
        {
            ShowInUI = true,
            ToolTip = TextManager.Get($"{ModPrefix}.givekey.tooltip")
        };

        SettingsService.RegisterSetting(_toggleKeySetting);
        SettingsService.RegisterSetting(_giveKeySetting);
    }
}
