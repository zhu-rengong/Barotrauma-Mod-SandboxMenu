using Barotrauma.Networking;

namespace SandboxMenu.Networking;

internal static class ServerOptions
{
    internal const string AllowAllClientsIdentifier = "allowallclients";

    private static BooleanSetting? _allowAllClients;

    static ServerOptions() => ModLifetime.Unloading += () => _allowAllClients = null;

    internal static bool AllowAllClients => _allowAllClients?.Value ?? false;

    internal static void Register(ISettingsService settings)
    {
        if (_allowAllClients is not null) { return; }

        _allowAllClients = new BooleanSetting(
            AllowAllClientsIdentifier.ToIdentifier(),
            defaultValue: false,
            label: TextManager.Get($"{Plugin.ModPrefix}.{AllowAllClientsIdentifier}.displayname"))
        {
            ShowInUI = true,
            ToolTip = TextManager.Get($"{Plugin.ModPrefix}.{AllowAllClientsIdentifier}.tooltip"),
            SyncMode = SettingSyncMode.ServerAuthority,
            IsAllowedToSet = static (_, client) => client is null || client.Permissions.HasFlag(ClientPermissions.ManageSettings)
        };

        settings.RegisterSetting(_allowAllClients);
    }
}
