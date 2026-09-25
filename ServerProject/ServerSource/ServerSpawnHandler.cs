using Barotrauma.Networking;

namespace SandboxMenu;

// Everything in here runs on a client's word, so the order of the checks is the design: first whether that client
// is allowed to ask at all, then whether what it sent survives unpacking, then whether there is anything to spawn
// for. Only past all of that does an item get made.
internal static class ServerSpawnHandler
{
    // A request costs decompression, XML parsing and a spawn queue pass, and the network service has no rate limit
    // of its own to lean on. One per player per half second is more than a person clicks and less than a loop can
    // flood.
    private const double MinInterval = 0.5;

    private const string CooldownField = "sandboxmenu.spawncooldown";

    internal static void Handle(SpawnRequest request, Client client)
        => Guard.Run("Handling a spawn request failed", () => Respond(request, client));

    private static void Respond(SpawnRequest request, Client client)
    {
        if (client is null) { return; }

        if (!IsAllowed(client))
        {
            Log.Info($"{client.Name} asked to spawn items without the permission to do so.");
            Reply(client, request.RequestId, SpawnStatus.Denied);
            return;
        }

        if (IsThrottled(client))
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

        // The id the client sent is never trusted for the spawn itself: it is only useful for telling a client
        // that its idea of the character it controls has drifted from the server's.
        if (request.CharacterId != character.ID)
        {
            Log.Info($"{client.Name} spawned for character {request.CharacterId}, but this server sees {character.ID}.");
        }

        SpawnTarget? target = request.Target switch
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

    private static bool IsAllowed(Client client)
        => ServerOptions.AllowAllClients || client.Permissions.HasFlag(ClientPermissions.ConsoleCommands);

    // The mark is kept on the client itself (a weak table the game owns), so nothing here can outlive a player.
    private static bool IsThrottled(Client client)
    {
        double now = Timing.TotalTime;

        if (now < client.GetOrAddExtraField(CooldownField, 0.0)) { return true; }

        client.SetExtraField(CooldownField, now + MinInterval);
        return false;
    }

    private static void Reply(Client client, uint requestId, SpawnStatus status, SpawnResult? result = null)
    {
        var response = new SpawnResponse(
            requestId,
            status,
            result?.QueuedCount ?? 0,
            SpawnPayloadCodec.LimitProblems(result?.Problems ?? []));

        Plugin.NetworkService.SendToClient(client, SandboxNetworkHeaders.SpawnResponse, response);
    }
}
