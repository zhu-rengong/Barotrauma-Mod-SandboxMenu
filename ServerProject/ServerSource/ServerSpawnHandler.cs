using Barotrauma.Networking;

namespace SandboxMenu;

internal static class ServerSpawnHandler
{
    internal static void Handle(SpawnRequest request, Client client)
        => ServerPolicy.Guard("Handling a spawn request", () => Respond(request, client));

    private static void Respond(SpawnRequest request, Client client)
    {
        if (client is null) { return; }

        if (!ServerPolicy.HasPermission(client))
        {
            Services.DebugConsole.NewMessage($"{client.Name} asked to spawn items without the permission to do so.");
            Reply(client, request.RequestId, SpawnStatus.Denied);
            return;
        }

        if (!ModCompatibility.Matches(request.ProtocolVersion, request.ModVersion, request.ModHash, client.Name, "spawn"))
        {
            Reply(client, request.RequestId, SpawnStatus.Incompatible);
            return;
        }

        if (!SpawnMessageCodec.TryDecode(request.Payload, out SpawnMessage payload))
        {
            Reply(client, request.RequestId, SpawnStatus.BadPayload);
            return;
        }

        if (!SpawnMessageValidator.TryValidate(request, payload, out string problem))
        {
            Services.DebugConsole.AddWarning($"{client.Name} sent a spawn request the mod rejects: {problem}.");
            Reply(client, request.RequestId, SpawnStatus.BadPayload);
            return;
        }

        Character? character = null;

        if (request.CharacterId != 0)
        {
            if (Entity.FindEntityByID(request.CharacterId) is not Character found || found.Removed)
            {
                Reply(client, request.RequestId, SpawnStatus.NoCharacter);
                return;
            }

            character = found;
        }

        if (Entity.Spawner is null)
        {
            Reply(client, request.RequestId, SpawnStatus.NoSpawner);
            return;
        }

        SpawnTarget? target = (SpawnTargetKind)request.Target switch
        {
            SpawnTargetKind.World => new SpawnTarget.AtWorld(request.Position, character),
            _ => character?.Inventory is { } inventory ? new SpawnTarget.IntoInventory(inventory, character) : null
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
            (byte)status,
            result?.QueuedCount ?? 0,
            SpawnMessageCodec.LimitProblems(result?.Problems ?? []));

        Services.GameNetwork.SendToClient(client, NetworkHeaders.SpawnResponse, response);
    }
}
