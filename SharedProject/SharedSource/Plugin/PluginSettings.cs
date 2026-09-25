#if CLIENT
using Microsoft.Xna.Framework.Input;

namespace SandboxMenu;

public partial class Plugin
{
    private static KeySetting? _toggleKeySetting;

    public static KeyBind ToggleKey => _toggleKeySetting?.Value ?? new KeyBind(Keys.F5);

    private void CreateSettings()
    {
        RegisterKeySetting("togglekey", new KeyBind(Keys.F5), out _toggleKeySetting);
    }

    private static void RegisterKeySetting(string identifier, KeyBind defaultValue, out KeySetting setting)
    {
        setting = new KeySetting(
            identifier.ToIdentifier(),
            defaultValue,
            label: TextManager.Get($"{ModPrefix}.{identifier}.displayname"))
        {
            ShowInUI = true,
            ToolTip = TextManager.Get($"{ModPrefix}.{identifier}.tooltip")
        };
        SettingsService.RegisterSetting(setting);
    }
}
#endif
