using Barotrauma.Networking;

namespace SandboxMenu.Domain.Networking;

internal static class ClientRequestPolicy
{
    internal static bool CanRequest
        => GameMain.Client is not { } client || client.HasPermission(ClientPermissions.ConsoleCommands);

    internal static bool CanTarget(Character? character)
        => CanRequest || character is not { IsRemotePlayer: true };
}
