using Barotrauma.Networking;
using SandboxMenu.Domain.Afflictions;

namespace SandboxMenu;

internal static class ServerAfflictionHandler
{
    private const string CooldownField = $"{ModInfo.Prefix}.afflictioncooldown";

    internal static void Handle(AfflictionRequest request, Client client)
        => ServerPolicy.Guard("Handling an affliction request", () => Respond(request, client));

    private static void Respond(AfflictionRequest request, Client client)
    {
        if (client is null) { return; }

        if (!ServerPolicy.IsAllowed(client))
        {
            Services.DebugConsole.NewMessage($"{client.Name} asked to apply an affliction without the permission to do so.");
            Reply(client, request.RequestId, AfflictionStatus.Denied);
            return;
        }

        if (request.ProtocolVersion != Protocol.Version)
        {
            Services.DebugConsole.AddWarning(
                $"{client.Name} runs an affliction protocol version this server does not speak ({request.ProtocolVersion} against {Protocol.Version}).");
            Reply(client, request.RequestId, AfflictionStatus.Denied);
            return;
        }

        if (ServerPolicy.IsThrottled(client, CooldownField))
        {
            Reply(client, request.RequestId, AfflictionStatus.Throttled);
            return;
        }

        if (FindAffliction(request.AfflictionId) is not { } affliction)
        {
            Reply(client, request.RequestId, AfflictionStatus.UnknownAffliction);
            return;
        }

        if (Entity.FindEntityByID(request.CharacterId) is not Character character || character.Removed)
        {
            Reply(client, request.RequestId, AfflictionStatus.NoCharacter);
            return;
        }

        int[] limbs = request.Limbs ?? [];

        if ((AfflictionMode)request.Mode == AfflictionMode.Remove)
        {
            AfflictionApplier.Remove(character.CharacterHealth, affliction, limbs);

            Reply(client, request.RequestId, AfflictionStatus.Ok);
            return;
        }

        AfflictionApplyResult applied = AfflictionApplier.Apply(
            character, affliction, request.Strength, limbs, request.MultiplyByMaxVitality, client.Character);

        Reply(client, request.RequestId, applied switch
        {
            AfflictionApplyResult.Applied => AfflictionStatus.Ok,
            AfflictionApplyResult.Blocked => AfflictionStatus.Blocked,
            _ => AfflictionStatus.NoCharacter
        });
    }

    private static AfflictionPrefab? FindAffliction(uint id)
    {
        if (_resolved is { } cached && _resolvedId == id
            && AfflictionPrefab.Prefabs.TryGet(cached.Identifier, out AfflictionPrefab? active)
            && ReferenceEquals(active, cached))
        {
            return cached;
        }

        AfflictionPrefab? found = AfflictionPrefab.Prefabs.Find(prefab => prefab.UintIdentifier == id);

        _resolved = found;
        _resolvedId = id;

        return found;
    }

    private static AfflictionPrefab? _resolved;
    private static uint _resolvedId;

    private static void Reply(Client client, uint requestId, AfflictionStatus status)
    {
        AfflictionResponse response = new(requestId, (byte)status);

        Services.GameNetwork.SendToClient(client, NetworkHeaders.AfflictionResponse, response);
    }
}
