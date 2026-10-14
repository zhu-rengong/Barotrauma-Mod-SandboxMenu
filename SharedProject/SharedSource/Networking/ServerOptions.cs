using Barotrauma.Networking;

namespace SandboxMenu.Networking;

[RunConstructorOnInit]
internal static class ServerOptions
{
    internal const string AllowAllClientsIdentifier = "allowallclients";

    private static readonly BooleanSetting AllowAllClientsSetting;

    static ServerOptions()
    {
        AllowAllClientsSetting = new BooleanSetting(
            AllowAllClientsIdentifier.ToIdentifier(),
            defaultValue: false,
            label: TextManager.Get($"{ModInfo.Prefix}.{AllowAllClientsIdentifier}.displayname"))
        {
            ShowInUI = true,
            ToolTip = TextManager.Get($"{ModInfo.Prefix}.{AllowAllClientsIdentifier}.tooltip"),
            SyncMode = SettingSyncMode.ServerAuthority,
            IsAllowedToSet = static (_, client) => client is null || client.Permissions.HasFlag(ClientPermissions.ManageSettings)
        };

        Services.Settings.RegisterSetting(AllowAllClientsSetting);
    }

    internal static bool AllowAllClients => AllowAllClientsSetting.Value;
}
