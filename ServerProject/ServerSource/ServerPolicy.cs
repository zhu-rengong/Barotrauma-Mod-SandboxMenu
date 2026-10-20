using Barotrauma.Networking;

namespace SandboxMenu;

internal static class ServerPolicy
{
    internal static bool HasPermission(Client client)
        => client.Permissions.HasFlag(ClientPermissions.ConsoleCommands);

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
