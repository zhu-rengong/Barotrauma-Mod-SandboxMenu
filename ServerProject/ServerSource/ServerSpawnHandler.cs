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
            DebugConsole.NewMessage($"{client.Name} asked to spawn items without the permission to do so.");
            Reply(client, request.RequestId, SpawnStatus.Denied);
            return;
        }

        if (ServerPolicy.IsThrottled(client, CooldownField))
        {
            Reply(client, request.RequestId, SpawnStatus.Throttled);
            return;
        }

        if (!SpawnPayloadCodec.TryDecode(request.Payload, out SpawnPayload payload))
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
            DebugConsole.NewMessage($"{client.Name} spawned for character {request.CharacterId}, but this server sees {character.ID}.");
        }

        SpawnTarget? target = (SpawnTargetKind)request.Target switch
        {
            SpawnTargetKind.World => new SpawnTarget.AtWorld(request.Position, character),
            _ => ItemSpawnService.InventoryTarget(character)
        };

        if (target is null)
        {
            Reply(client, request.RequestId, SpawnStatus.NoCharacter);
            return;
        }

        SpawnResult result = ItemSpawnService.Spawn(target, payload.Entries, payload.ResolveTemplate);
        Reply(client, request.RequestId, SpawnStatus.Ok, result);
    }

    private static void Reply(Client client, uint requestId, SpawnStatus status, SpawnResult? result = null)
    {
        SpawnResponse response = new(
            requestId,
            (byte)status,
            result?.QueuedCount ?? 0,
            SpawnPayloadCodec.LimitProblems(result?.Problems ?? []));

        Services.GameNetwork.SendToClient(client, NetworkHeaders.SpawnResponse, response);
    }
}
