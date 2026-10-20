using Barotrauma.Networking;
using SandboxMenu.Domain.Afflictions;

namespace SandboxMenu;

internal static class ServerAfflictionHandler
{
    internal static void Handle(AfflictionRequest request, Client client)
        => ServerPolicy.Guard("Handling an affliction request", () => Respond(request, client));

    private static void Respond(AfflictionRequest request, Client client)
    {
        if (client is null) { return; }

        if (!ServerPolicy.HasPermission(client))
        {
            Services.DebugConsole.NewMessage($"{client.Name} asked to apply an affliction without the permission to do so.");
            Reply(client, request.RequestId, AfflictionStatus.Denied);
            return;
        }

        if (!ModCompatibility.Matches(request.ProtocolVersion, request.ModVersion, request.ModHash, client.Name, "affliction"))
        {
            Reply(client, request.RequestId, AfflictionStatus.Incompatible);
            return;
        }

        if (!SpawnMessageValidator.TryValidate(request, out string problem))
        {
            Services.DebugConsole.AddWarning($"{client.Name} sent an affliction request the mod rejects: {problem}.");
            Reply(client, request.RequestId, AfflictionStatus.BadPayload);
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
