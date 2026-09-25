using Barotrauma.Networking;

namespace SandboxMenu.Networking;

// The one setting both halves register: the server owns the value and syncs it down, the client only shows it in
// the plugin's settings tab. With it off, spawning stays with the clients that hold the console commands
// permission — the host and the admins — which is what a server wants unless it is a sandbox for everyone.
internal static class ServerOptions
{
    internal const string AllowAllClientsIdentifier = "allowallclients";

    private static BooleanSetting? _allowAllClients;

    static ServerOptions() => StaticState.Register(() => _allowAllClients = null);

    internal static bool AllowAllClients => _allowAllClients?.Value ?? false;

    internal static void Register(ISettingsService settings)
    {
        // Both halves reach this, and a second registration would leave a row in the settings menu that nothing
        // ever updates again.
        if (_allowAllClients is not null) { return; }

        _allowAllClients = new BooleanSetting(
            AllowAllClientsIdentifier.ToIdentifier(),
            defaultValue: false,
            label: TextManager.Get($"{Plugin.ModPrefix}.{AllowAllClientsIdentifier}.displayname"))
        {
            ShowInUI = true,
            ToolTip = TextManager.Get($"{Plugin.ModPrefix}.{AllowAllClientsIdentifier}.tooltip"),
            SyncMode = SettingSyncMode.ServerAuthority,
            // On the server this runs with a client asking, so only one that may manage settings gets a say.
            IsAllowedToSet = static (_, client) => client is null || client.Permissions.HasFlag(ClientPermissions.ManageSettings)
        };

        settings.RegisterSetting(_allowAllClients);
    }
}
