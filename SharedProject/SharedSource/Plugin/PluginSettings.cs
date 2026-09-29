#if CLIENT
using Microsoft.Xna.Framework.Input;

namespace SandboxMenu;

public sealed partial class Plugin
{
    private static readonly KeyBind _defaultToggleKey = new(Keys.F2);

    private static KeySetting? _toggleKeySetting;

    internal static KeyBind ToggleKey => _toggleKeySetting?.Value ?? _defaultToggleKey;

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

        SettingsService.RegisterSetting(_toggleKeySetting);
    }
}
#endif
