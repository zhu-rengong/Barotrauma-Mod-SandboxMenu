using Barotrauma.Networking;

namespace SandboxMenu;

internal static class ServerSpawnHandler
{
    private const string CooldownField = $"{ModInfo.Prefix}.spawncooldown";

    internal static void Handle(SpawnRequest request, Client client)
        => ServerPolicy.Guard("Handling a spawn request", () => Respond(request, client));

    private static void Respond(SpawnRequest request, Client client)
    {
        if (client is null) { return; }

        if (!ServerPolicy.IsAllowed(client))
        {
            Services.DebugConsole.NewMessage($"{client.Name} asked to spawn items without the permission to do so.");
            Reply(client, request.RequestId, SpawnStatus.Denied);
            return;
        }

        if (request.ProtocolVersion != Protocol.Version)
        {
            Services.DebugConsole.AddWarning(
                $"{client.Name} runs a spawn protocol version this server does not speak ({request.ProtocolVersion} against {Protocol.Version}).");
            Reply(client, request.RequestId, SpawnStatus.BadPayload);
            return;
        }

        if (ServerPolicy.IsThrottled(client, CooldownField))
        {
            Reply(client, request.RequestId, SpawnStatus.Throttled);
            return;
        }

        if (!SpawnMessageCodec.TryDecode(request.Payload, out SpawnMessage payload))
        {
            Reply(client, request.RequestId, SpawnStatus.BadPayload);
            return;
        }

        if (client.Character is not { } character)
        {
            Reply(client, request.RequestId, SpawnStatus.NoCharacter);
            return;
        }

        if (Entity.Spawner is null)
        {
            Reply(client, request.RequestId, SpawnStatus.NoSpawner);
            return;
        }

        if (request.CharacterId != character.ID)
        {
            Services.DebugConsole.NewMessage($"{client.Name} spawned for character {request.CharacterId}, but this server sees {character.ID}.");
        }

        SpawnTarget? target = request.Target switch
        {
            SpawnTargetKind.World => new SpawnTarget.AtWorld(request.Position, character),
            _ => character.Inventory is { } inventory ? new SpawnTarget.IntoInventory(inventory, character) : null
        };

        if (target is null)
        {
            Reply(client, request.RequestId, SpawnStatus.NoCharacter);
            return;
        }

        SpawnExecutor executor = new(target, payload.ResolveTemplate);
        executor.Run(payload.Entries);

        Reply(client, request.RequestId, SpawnStatus.Ok, executor.Result);
    }

    private static void Reply(Client client, uint requestId, SpawnStatus status, SpawnResult? result = null)
    {
        SpawnResponse response = new(
            requestId,
            status,
            result?.QueuedCount ?? 0,
            SpawnMessageCodec.LimitProblems(result?.Problems ?? []));

        Services.GameNetwork.SendToClient(client, NetworkHeaders.SpawnResponse, response);
    }
}
