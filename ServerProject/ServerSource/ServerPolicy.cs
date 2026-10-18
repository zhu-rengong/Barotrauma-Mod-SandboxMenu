using Barotrauma.Networking;

namespace SandboxMenu;

internal static class ServerPolicy
{
    private const double MinInterval = 0.5;

    internal static bool IsAllowed(Client client)
        => ServerOptions.AllowAllClients || client.Permissions.HasFlag(ClientPermissions.ConsoleCommands);

    internal static bool IsThrottled(Client client, string cooldownField)
    {
        double now = Timing.TotalTime;

        if (now < client.GetOrAddExtraField(cooldownField, 0.0)) { return true; }

        client.SetExtraField(cooldownField, now + MinInterval);
        return false;
    }

    internal static void Guard(string action, Action body)
    {
        try
        {
            body();
        }
        catch (Exception e)
        {
            Services.DebugConsole.AddWarning($"{action} failed: {e}");
        }
    }
}
